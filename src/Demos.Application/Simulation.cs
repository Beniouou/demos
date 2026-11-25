using Demos.Domain.Economy;
using Demos.Domain.Map;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Point d'entrée du moteur : il appelle AdvanceDay au rythme choisi par le joueur.
/// La simulation calcule l'économie chaque jour, les variables lentes chaque saison
/// et les bilans chaque année. Chaque commune est une petite économie : son marché et son stockage, où
/// arrive aussi la production de l'État, sa caisse, et des échanges avec les autres communes.
/// </summary>
public sealed partial class Simulation(World world, GameClock clock)
{
    /// <summary>Type du catalogue qui fonde une commune : ses emplois et ses places attirent les premiers habitants.</summary>
    public const string BaseCampTypeId = "camp-de-base";

    /// <summary>Type du bâtiment qui sert de siège à une préfecture.</summary>
    public const string PrefectureTypeId = "prefecture";

    /// <summary>
    /// Proposition : la dotation d'une nouvelle commune couvre 60 jours de salaires de son camp de base, le temps
    /// que ses premiers bâtiments de production produisent.
    /// </summary>
    public static int FoundingGrantDays => Balance.Current.FoundingGrantDays;

    public World World { get; } = WithLegitimacyClock(world, clock);
    public GameClock Clock { get; } = clock;

    /// <summary>Les dépenses de légitimité notées au relevé portent la date du jeu.</summary>
    private static World WithLegitimacyClock(World world, GameClock clock)
    {
        world.Legitimacy.Clock = () => clock.Today;
        return world;
    }

    public event Action? SeasonPassed;
    public event Action? YearPassed;

    /// <summary>Ce que les routes ont changé au dernier bilan du mois ou de l'année (construction, entretien).</summary>
    public IReadOnlyList<RoadChange> RoadChanges { get; private set; } = [];

    public void AdvanceDay()
    {
        var change = Clock.AdvanceDay();

        // Au changement de mois, les lois votées entrent en vigueur, puis les entreprises licencient ou réembauchent,
        // avant la journée de travail.
        if (change.HasFlag(CalendarChange.NewMonth))
            RunMonthlyCalculations();

        // Les dotations du mois arrivent dans les caisses des communes avant la journée de travail.
        PayGrantsIfDue();

        RunDailyCalculations();

        if (change.HasFlag(CalendarChange.NewSeason))
            RunSeasonalCalculations();

        if (change.HasFlag(CalendarChange.NewYear))
            RunYearlyCalculations();
    }

    /// <summary>Un bâtiment dont le chantier vient de finir : il est actif à partir du lendemain.</summary>
    public event Action<Structure>? BuildingCompleted;

    /// <summary>
    /// Action du joueur : lancer le chantier d'un bâtiment. Son coût de construction (celui de son type) est payé en
    /// entier au lancement, avec ce que l'État a produit dans le stockage de sa commune, en l'achetant avec l'argent du
    /// trésor, ou les deux. Le bâtiment rejoint la partie à la fin du chantier. Renvoie false, sans rien lancer ni
    /// dépenser, si c'est impossible (type sans coût de construction, matériaux ou argent insuffisants).
    /// </summary>
    /// <remarks>
    /// Un bâtiment d'extraction (camp de bûcheron, ferme, carrière, mine) se pose à <paramref name="position"/>, sur une
    /// zone de la carte qui correspond, à portée de sa commune (voir <see cref="World.ExtractionSiteProblem"/>) : sans
    /// position permise, il n'est pas construit. Sans carte, la position n'est pas demandée.
    /// </remarks>
    public bool TryBuild(Structure structure, PaymentMode mode, Point2D? position = null) => Build(structure, mode, position).Started;

    /// <summary>
    /// Action du joueur : lancer le chantier d'un bâtiment à une taille choisie (de 10 % à 100 % du type ; décidé le 20
    /// novembre 2026). Les matériaux viennent de la réserve de chantier et du stock de l'État de sa commune, d'un achat à
    /// son marché, puis, s'il en manque, de la commune la plus proche qui en a et avec laquelle elle peut échanger (voir
    /// <see cref="PlanConstruction"/>) : le chantier commence à leur arrivée. Un bâtiment d'extraction se pose sur une
    /// zone qui convient et qui a encore la place pour ses emplois (<see cref="World.ZoneProblem"/>).
    /// </summary>
    public ConstructionOutcome Build(Structure structure, PaymentMode mode, Point2D? position = null, double size = 1)
    {
        if (World.IsIdTaken(structure.Id)
            || structure.TypeId is not { } typeId
            || !World.BuildingTypes.TryGetValue(typeId, out var type)
            || !type.CanBeBuilt
            // Le bâtiment de la préfecture ne se construit qu'en fondant une préfecture (TryFoundPrefecture).
            || typeId == PrefectureTypeId
            || structure.ParentId is not { } parentId
            || !World.Structures.TryGetValue(parentId, out var parent)
            || World.CommuneOf(parent) is not { } communeId)
            return new ConstructionOutcome(false, null, "ce bâtiment ne se construit pas ici");

        structure = type.Sized(structure, size);
        var productId = structure.Production?.ProductId;
        if (World.NeedsExtractionSite(typeId, productId))
        {
            if (position is not { } site)
                return new ConstructionOutcome(false, null, "choisissez son emplacement sur la carte");
            if ((World.ExtractionSiteProblem(typeId, productId, communeId, site) ?? World.ZoneProblem(typeId, productId, site, structure.JobCapacity)) is { } problem)
                return new ConstructionOutcome(false, null, problem);
        }

        var plan = PlanConstruction(type, communeId, size, mode);
        if (!plan.Feasible)
            return new ConstructionOutcome(false, plan, plan.Missing > 1e-6
                ? $"il manque {plan.Missing:N0} matériaux, ici comme dans les communes avec lesquelles elle échange"
                : $"il faut {plan.Money:N0} au trésor et au poste Chantiers du budget pour acheter les matériaux");

        PayConstruction(plan, communeId);
        if (position is { } placed)
            World.SetBuildingPosition(structure.Id, placed);
        // Le chantier commence quand les matériaux venus d'ailleurs arrivent.
        World.AddConstructionSite(new ConstructionSite(structure, type.Construction!.Days + plan.TravelDays));
        return new ConstructionOutcome(true, plan, null);
    }

    /// <summary>
    /// D'où viendraient les matériaux d'un chantier, sans rien dépenser (décidé le 20 novembre 2026) : la réserve de
    /// chantier, le stock de l'État de la commune (sauf achat seul), un achat à son marché ; puis, s'il en manque, la commune
    /// la plus proche avec laquelle elle peut échanger et qui en a : son stock de l'État, puis un achat à son marché, avec la
    /// perte en route (il en part donc un peu plus), dans la limite de sa capacité d'envoi d'une journée.
    /// </summary>
    public ConstructionPlan PlanConstruction(BuildingType type, string communeId, double size = 1, PaymentMode mode = PaymentMode.StockFirst)
    {
        var productId = type.Construction?.ProductId ?? "";
        var quantity = type.ConstructionQuantity(size);
        var market = World.MarketOf(communeId);
        var state = Market.StateSellerId(communeId);
        var reserve = Math.Min(World.ConstructionReserveOf(communeId).QuantityOf(productId), quantity);
        var missing = quantity - reserve;
        var fromState = mode == PaymentMode.StockFirst ? Math.Min(market.QuantityOf(productId, state), missing) : 0;
        missing -= fromState;
        var boughtHere = Math.Min(market.QuantityForSale(productId, state), missing);
        missing -= boughtHere;

        string? other = null;
        double fromOtherState = 0, boughtThere = 0, lost = 0;
        var days = 0;
        if (missing > 1e-6)
        {
            var hasLogistics = World.Communes.ToDictionary(c => c.Id, c => World.HasLogisticsBuilding(c.Id));
            var here = World.CommunePositions.GetValueOrDefault(communeId);
            var candidates = World.Communes
                .Where(c => c.Id != communeId && Trade.CanTrade(c.Id, communeId, id => hasLogistics.GetValueOrDefault(id)))
                .Where(c => World.Markets.TryGetValue(c.Id, out var m) && m.QuantityOf(productId) > 0)
                .OrderBy(c => World.CommunePositions.TryGetValue(c.Id, out var p) ? MapScale.KmBetween(p, here) : double.MaxValue);
            if (candidates.FirstOrDefault() is { } source)
            {
                other = source.Id;
                var otherMarket = World.MarketOf(source.Id);
                var otherState = Market.StateSellerId(source.Id);
                var loss = TransportLossBetween(source.Id, communeId);
                var holder = Trade.CapacityHolder(source.Id, communeId, id => hasLogistics.GetValueOrDefault(id));
                var sendable = Math.Min(missing / Math.Max(0.01, 1 - loss), World.ShippingCapacity(holder));
                fromOtherState = mode == PaymentMode.StockFirst ? Math.Min(otherMarket.QuantityOf(productId, otherState), sendable) : 0;
                boughtThere = Math.Min(otherMarket.QuantityForSale(productId, otherState), sendable - fromOtherState);
                lost = (fromOtherState + boughtThere) * loss;
                missing = Math.Max(0, missing - (fromOtherState + boughtThere - lost));
                days = TravelDaysBetween(source.Id, communeId);
            }
        }

        var money = (boughtHere + boughtThere) * market.PriceOf(productId);
        return new ConstructionPlan(productId, quantity, reserve, fromState, boughtHere, other, fromOtherState, boughtThere, lost, days,
            money, missing, World.EnvelopeOf(BudgetItem.Construction).Balance >= money - 1e-6);
    }

    /// <summary>Paie un chantier selon son plan : prend les matériaux, achète le reste avec le trésor, envoie ce qui vient d'ailleurs.</summary>
    private void PayConstruction(ConstructionPlan plan, string communeId)
    {
        var productId = plan.ProductId;
        World.EnvelopeOf(BudgetItem.Construction).TrySpend(plan.Money);
        World.ConstructionReserveOf(communeId).TryRemove(productId, plan.FromReserve);
        var market = World.MarketOf(communeId);
        var state = Market.StateSellerId(communeId);
        market.TryWithdraw(productId, plan.FromState, state);
        market.TryBuy(productId, plan.BoughtHere, exceptSellerId: state);
        if (plan.OtherCommuneId is not { } other || plan.SentFromOther <= 0)
            return;

        // Les matériaux venus d'ailleurs voyagent comme un chargement : ils comptent pour les routes et la perte en route.
        var otherMarket = World.MarketOf(other);
        var otherState = Market.StateSellerId(other);
        otherMarket.TryWithdraw(productId, plan.FromOtherState, otherState);
        otherMarket.TryBuy(productId, plan.BoughtThere, exceptSellerId: otherState);
        otherMarket.CountLostInTransit(productId, plan.LostOnTheWay);
        World.RoadTraffic.Add(other, communeId, plan.SentFromOther);
    }

    /// <summary>Ancienne signature, gardée pour l'interface : le coût passé n'est plus utilisé, c'est celui du type.</summary>
    [Obsolete("Le coût de construction vient du type de bâtiment : utiliser TryBuild(structure, mode).")]
    public bool TryBuild(Structure structure, IReadOnlyList<MaterialCost> cost, PaymentMode mode) => TryBuild(structure, mode);

    /// <summary>
    /// Outil d'administration : remplace les paramètres d'un bâtiment (propriétaire, emplois, salaire, production,
    /// places, loyer). Les effets se voient dès le jour suivant.
    /// </summary>
    public void UpdateBuilding(Structure building) => World.UpdateStructure(building);

    /// <summary>
    /// Crée une ligne d'approvisionnement du fournisseur vers le client (règle dans <see cref="SupplyLines"/>). Renvoie
    /// la ligne, ou le problème qui l'empêche.
    /// </summary>
    public (SupplyLine? Line, string? Problem) TryAddSupplyLine(string supplierId, string clientId, double share = SupplyLines.DefaultShare)
    {
        var problem = SupplyLines.ProblemWith(World.Structures.GetValueOrDefault(supplierId), World.Structures.GetValueOrDefault(clientId), World.SupplyLines);
        return problem is not null ? (null, problem) : (World.AddSupplyLine(supplierId, clientId, Math.Clamp(share, 0.05, 1)), null);
    }

    public bool RemoveSupplyLine(string lineId) => World.RemoveSupplyLine(lineId);

    /// <summary>Fait passer une ligne avant (ou après) une autre ligne du même client : sa priorité d'achat.</summary>
    public bool MoveSupplyLine(string lineId, bool earlier) => World.MoveSupplyLine(lineId, earlier);

    /// <summary>
    /// Change les réglages d'achat et de vente d'un bâtiment public (règle dans <see cref="SupplySettings"/>). Renvoie
    /// le problème qui l'empêche, ou null : seuls les bâtiments publics en ont, et un refus de vendeur que les lois
    /// interdisent ne peut pas être ajouté.
    /// </summary>
    public string? SetSupplySettings(string buildingId, SupplySettings settings)
    {
        if (!World.Structures.TryGetValue(buildingId, out var building) || building.Kind != StructureKind.Building)
            return "bâtiment inconnu";
        if (building.Ownership != Ownership.Public)
            return "seuls les bâtiments publics ont ces réglages : le privé et les SCOP décident seuls";
        var current = World.SupplySettingsOf(buildingId);
        if (settings.RefusePrivate && !current.RefusePrivate && !building.Rules.MayRefusePrivate)
            return "une loi interdit ici de refuser les entreprises privées (Choix des fournisseurs publics)";
        if (settings.RefuseCooperative && !current.RefuseCooperative && !building.Rules.MayRefuseCooperative)
            return "une loi interdit ici de refuser les SCOP (Choix des fournisseurs publics)";
        World.SetSupplySettings(buildingId, settings with { ReserveDays = Math.Clamp(settings.ReserveDays, 0, SupplySettings.MaxReserveDays) });
        return null;
    }

    /// <summary>Change la part de la production du fournisseur que la ligne envoie (de 5 % à 100 %).</summary>
    public bool SetSupplyLineShare(string lineId, double share) => World.SetSupplyLineShare(lineId, Math.Clamp(share, 0.05, 1));

    /// <summary>La perte de transport entre deux communes, réduite par un chemin ou une route (20 % sans carte).</summary>
    public double TransportLossBetween(string from, string to) =>
        World.CommunePositions.Count > 0
            ? GeographicEconomics.TransportLoss(from, to, World.CommunePositions) * World.Roads.LossFactor(from, to)
            : Trade.TransportLoss;

    /// <summary>
    /// La durée d'un trajet entre deux communes, en jours, selon la distance et la liaison (décidé le 20 novembre 2026) ;
    /// 1 jour sans carte.
    /// </summary>
    public int TravelDaysBetween(string from, string to) =>
        World.CommunePositions.TryGetValue(from, out var a) && World.CommunePositions.TryGetValue(to, out var b)
            ? Roads.TravelDays(World.Roads.LevelBetween(from, to), MapScale.KmBetween(a, b), Balance.Current)
            : 1;

    /// <summary>
    /// Action du joueur : le nombre de charrettes voulu pour une commune (décidé le 20 novembre 2026). L'État de la
    /// commune achète ce qui manque, chaque jour. Faux pour une commune inconnue.
    /// </summary>
    public bool SetWantedCarts(string communeId, int wanted)
    {
        if (!World.Communes.Any(c => c.Id == communeId))
            return false;
        World.SetWantedCarts(communeId, Math.Max(0, wanted));
        return true;
    }

    /// <summary>Outil d'administration : règle l'impôt d'État (part de la richesse versée chaque jour).</summary>
    public void SetStateTaxRate(double rate) => World.StateTaxRate = rate;

    /// <summary>Outil d'administration : règle l'impôt d'une commune, versé à sa caisse.</summary>
    public void SetCommuneTaxRate(string communeId, double rate) => World.SetCommuneTaxRate(communeId, rate);

    /// <summary>Outil d'administration : fixe le prix d'un produit dans toutes les communes.</summary>
    public void SetPrice(string productId, double price) => World.SetPrice(productId, price);

    /// <summary>Coût de base d'une vente au privé en légitimité LI, pour un bâtiment de référence (réglage).</summary>
    public static double SaleLegitimacyCost => Balance.Current.SaleLegitimacyCost;

    /// <summary>La population de la nation : tous les membres de toutes les classes.</summary>
    public double NationPopulation => World.Classes.Sum(c => (double)c.Population);

    /// <summary>
    /// La taille de la nation par rapport à la population de référence, au moins 1 : les coûts fixes en légitimité
    /// (fonder, rattacher) la suivent (décidé le 23 novembre 2026).
    /// </summary>
    public double NationCostScale => NationCostScaleOf(World);

    /// <summary>La taille d'une nation par rapport à la population de référence, au moins 1.</summary>
    public static double NationCostScaleOf(World world) =>
        Math.Max(1, world.Classes.Sum(c => (double)c.Population) / Balance.Current.LegitimacyCostReferencePopulation);

    /// <summary>
    /// Le coût en légitimité d'une action sur un bâtiment (vendre, nationaliser) : son coût de base, au prorata de ses
    /// emplois par rapport au bâtiment de référence, au moins le coût minimal d'une brique (décidé le 23 novembre 2026).
    /// </summary>
    public static double BuildingLegitimacyCost(double baseCost, Structure building) =>
        Math.Max(Balance.Current.MinimumBrickCost, baseCost * building.JobCapacity / Balance.Current.LegitimacyCostReferenceJobs);

    /// <summary>Ce que coûte la vente de ce bâtiment au privé, en légitimité LI.</summary>
    public double SaleLegitimacyCostOf(Structure building) => BuildingLegitimacyCost(SaleLegitimacyCost, building);

    /// <summary>Pôle d'une vente au privé : libéral (LI).</summary>
    public static readonly Orientation SalePole = new(IdeologicalAxis.Property, Pole.B);

    /// <summary>Bénéfice attendu par jour d'un bâtiment, avec son personnel actuel.</summary>
    public double DailyProfitOf(Structure building) =>
        Privatization.DailyProfitOf(building, World.Workers.GetValueOrDefault(building.Id), World.Classes, World.PriceOf);

    /// <summary>
    /// Action du joueur : vendre un bâtiment public à une classe de sa commune, au prix choisi. L'argent va au trésor,
    /// la classe devient propriétaire (privé ou coopérative), et l'entreprise ouvre sa propre caisse, vide au départ :
    /// ses ventes la remplissent et elle paie désormais les salaires. La vente coûte de la légitimité LI ; ce
    /// n'est pas une loi. Ne change rien si la vente est impossible.
    /// </summary>
    public SaleOutcome TrySellBuilding(string buildingId, string buyerClassId, double price)
    {
        var building = World.Structures[buildingId];
        var buyer = World.ClassesById[buyerClassId];
        var communeId = World.CommuneOf(building);

        var outcome = Privatization.Check(building, communeId, buyer, price, DailyProfitOf(building));
        if (outcome != SaleOutcome.Sold)
            return outcome;
        // Sans loi qui autorise le privé à cet endroit, on ne vend qu'à des ouvriers (en SCOP).
        if (Privatization.OwnershipFor(buyer) == Ownership.Private && !PrivateCompaniesAllowed(building))
            return SaleOutcome.PrivateForbidden;
        if (!World.Legitimacy.TrySpend(SalePole, SaleLegitimacyCostOf(building), LegitimacySpendingKind.Sale, buildingId))
            return SaleOutcome.NotEnoughLegitimacy;

        buyer.Wealth -= price;
        World.Treasury.Deposit(price);
        World.UpdateStructure(building with { Ownership = Privatization.OwnershipFor(buyer), OwnerClassId = buyer.Id });
        World.Wages.Forget(building.Id);
        return SaleOutcome.Sold;
    }

    /// <summary>Vrai si les lois qui couvrent ce bâtiment y autorisent les entreprises privées (interdites par défaut).</summary>
    public bool PrivateCompaniesAllowed(Structure building) => World.LawBook.ValueFor(Bricks.PrivateCompanies, building) >= 0.5;

    /// <summary>
    /// Les entreprises privées que les lois n'autorisent plus sont nationalisées sans indemnité, comme une expropriation :
    /// au début de la partie et quand des lois entrent en vigueur. Renvoie les bâtiments repris.
    /// </summary>
    public IReadOnlyList<Structure> NationalizeForbiddenCompanies()
    {
        var taken = World.Structures.Values
            .Where(b => b.Kind == StructureKind.Building && b.Ownership == Ownership.Private && !PrivateCompaniesAllowed(b))
            .ToList();
        foreach (var building in taken)
            TakeIntoPublicHands(building, NationalizationMode.Expropriation);
        if (taken.Count > 0)
            CompaniesNationalizedByLaw?.Invoke(taken);
        return taken;
    }

    /// <summary>Les entreprises privées nationalisées parce qu'aucune loi ne les autorise plus.</summary>
    public event Action<IReadOnlyList<Structure>>? CompaniesNationalizedByLaw;

    /// <summary>Le bâtiment devient public. Sa caisse ferme : rachetée, son argent revient aux propriétaires ; expropriée, à la commune.</summary>
    private void TakeIntoPublicHands(Structure building, NationalizationMode mode)
    {
        var owner = building.OwnerClassId is { } ownerId ? World.ClassesById.GetValueOrDefault(ownerId) : null;
        var remaining = World.CloseCompanyFund(building.Id);
        if (mode == NationalizationMode.Buyout && owner is not null)
            owner.Wealth += remaining;
        else if (World.CommuneOf(building) is { } communeId)
            World.CommuneFundOf(communeId).Deposit(remaining);

        World.UpdateStructure(building with { Ownership = Ownership.Public, OwnerClassId = null });
        World.Wages.Forget(building.Id);
    }

    /// <summary>Pôle d'une nationalisation : collectiviste (CO).</summary>
    public static readonly Orientation NationalizationPole = new(IdeologicalAxis.Property, Pole.A);

    /// <summary>Une entreprise est en difficulté quand elle a versé moins de 80 % des salaires dus sur 30 jours.</summary>
    public bool IsStruggling(Structure building) => World.Wages.IsStruggling(building.Id);

    public double NationalizationPriceOf(Structure building, NationalizationMode mode) =>
        Nationalization.PriceOf(mode, IsStruggling(building), DailyProfitOf(building));

    public double NationalizationLegitimacyCostOf(Structure building, NationalizationMode mode) =>
        BuildingLegitimacyCost(Nationalization.LegitimacyCostOf(mode, IsStruggling(building)), building);

    /// <summary>
    /// Action du joueur : reprendre un bâtiment privé ou coopératif, par rachat (le trésor paie le propriétaire) ou
    /// par expropriation (sans payer). Le propriétaire ne peut pas refuser. Le bâtiment redevient public ; ce n'est pas une loi.
    /// La caisse de l'entreprise ferme : en cas de rachat, ce qu'il y restait revient aux propriétaires ; en cas
    /// d'expropriation, à la caisse de la commune. Ne change rien si c'est impossible.
    /// </summary>
    public NationalizationOutcome TryNationalize(string buildingId, NationalizationMode mode)
    {
        var building = World.Structures[buildingId];
        if (building.Ownership == Ownership.Public || World.CommuneOf(building) is not { } communeId)
            return NationalizationOutcome.NotPrivate;

        var price = NationalizationPriceOf(building, mode);
        var legitimacyCost = NationalizationLegitimacyCostOf(building, mode);
        var investment = World.EnvelopeOf(BudgetItem.Investment);
        if (investment.Balance < price)
            return NationalizationOutcome.TreasuryTooPoor;
        if (!World.Legitimacy.TrySpend(NationalizationPole, legitimacyCost, LegitimacySpendingKind.Nationalization, buildingId))
            return NationalizationOutcome.NotEnoughLegitimacy;

        investment.TrySpend(price);
        if (building.OwnerClassId is { } ownerId && World.ClassesById.TryGetValue(ownerId, out var owner))
            owner.Wealth += price;

        TakeIntoPublicHands(building, mode);
        return NationalizationOutcome.Done;
    }

    /// <summary>Fonder une commune dégrade la terre : la légitimité se paie toujours en productivisme (décidé le 23 novembre 2026).</summary>
    public static readonly Orientation FoundingPole = new(IdeologicalAxis.Production, Pole.A);

    /// <summary>
    /// Ce que coûte en légitimité PO une commune fondée depuis cette commune : le coût de base, changé par le défrichement
    /// que votent les lois qui la couvrent.
    /// </summary>
    public double FoundingLegitimacyCostOf(string payingCommuneId) =>
        Balance.Current.BaseCampLegitimacyCost * NationCostScale * World.LawBook.FoundingCostFactor(payingCommuneId);

    /// <summary>Vrai si les lois qui couvrent cette commune autorisent l'expansion (brique « Extension »).</summary>
    public bool AllowsExtension(string communeId) => World.LawBook.AllowsExtension(communeId);

    /// <summary>
    /// Action du joueur : fonder une commune depuis celle qui paie, avec sa charte, pour le coût en légitimité PO que fixent
    /// les lois (voir <see cref="FoundingLegitimacyCostOf"/>).
    /// </summary>
    public Structure? TryFoundCommune(string name, string payingCommuneId, PaymentMode mode, Orientation charter, Point2D? position = null) =>
        TryFoundCommune(name, payingCommuneId, mode, charter, FoundingLegitimacyCostOf(payingCommuneId), position);

    /// <summary>
    /// Action du joueur : bâtir un camp de base, qui fonde une nouvelle commune rattachée à l'État.
    /// Il coûte son coût de construction (celui du type camp de base), pris dans la commune qui l'envoie, de la
    /// légitimité PO (fonder dégrade la terre, décidé le 23 novembre 2026), et la dotation que l'État verse du trésor à
    /// la caisse de la nouvelle commune. Il faut une loi d'extension (brique « Extension ») qui couvre la commune qui paie.
    /// La commune est fondée tout de suite ; son camp de base est en chantier. Elle reçoit sa réserve de chantier de
    /// fondation. La charte, choisie à part, attire les classes du même pôle et repousse celles du pôle opposé. Une
    /// position sur la carte doit être permise (<see cref="World.CommuneSiteProblem"/> : ni l'eau, ni une falaise, ni un
    /// glacier, ni trop près d'une autre commune).
    /// Renvoie la nouvelle commune, ou null, sans rien dépenser, si c'est impossible.
    /// </summary>
    public Structure? TryFoundCommune(
        string name,
        string payingCommuneId,
        PaymentMode mode,
        Orientation charter,
        double legitimacyCost,
        Point2D? position = null)
    {
        var state = World.Structures.Values.Single(s => s.IsNational);
        var number = World.Communes.Count() + 1;
        var commune = new Structure($"commune-{number}", name, StructureKind.PoliticalEntity, state.Id);
        var campType = World.BuildingTypes[BaseCampTypeId];
        var camp = campType.Build($"camp-de-base-{number}", commune.Id, name: $"{campType.Name} ({name})");
        var grant = FoundingGrantOf(campType);

        if (World.IsIdTaken(commune.Id)
            || (position is { } site && World.CommuneSiteProblem(site) is not null)
            || !campType.CanBeBuilt
            || !AllowsExtension(payingCommuneId)
            || World.Legitimacy[FoundingPole] < legitimacyCost
            || !World.EnvelopeOf(BudgetItem.Investment).TrySpend(grant))
            return null;

        if (!Construction.TryPay(campType.Construction!.Materials, mode, Market.StateSellerId(payingCommuneId), World.EnvelopeOf(BudgetItem.Construction),
                World.MarketOf(payingCommuneId), World.ConstructionReserveOf(payingCommuneId)))
        {
            World.EnvelopeOf(BudgetItem.Investment).Deposit(grant);
            return null;
        }

        World.Legitimacy.TrySpend(FoundingPole, legitimacyCost, LegitimacySpendingKind.Founding, commune.Id);
        World.CommuneFundOf(commune.Id).Deposit(grant);
        World.AddStructure(commune);
        if (position.HasValue)
            World.SetCommunePosition(commune.Id, position.Value);
        World.AddConstructionSite(new ConstructionSite(camp, campType.Construction.Days));
        GiveFoundingMaterials(commune.Id);
        World.SetCharter(commune.Id, charter);
        return commune;
    }

    /// <summary>
    /// Ce que coûte une préfecture qui regroupe ces entités : son coût de base, au prorata des habitants qu'elle regroupe
    /// par rapport à la population de référence, au moins le coût de base (décidé le 23 novembre 2026).
    /// </summary>
    public double PrefectureLegitimacyCostOf(IEnumerable<string> memberIds)
    {
        var members = memberIds.Distinct().ToList();
        var inhabitants = World.Communes
            .Where(c => members.Any(m => StructureTree.IsWithin(c, m, World.Structures)))
            .Sum(c => World.Residents(c.Id));
        var settings = Balance.Current;
        return settings.PrefectureLegitimacyCost * Math.Max(1, inhabitants / settings.LegitimacyCostReferencePopulation);
    }

    /// <summary>Ce que coûte un rattachement : son coût de base, qui suit la taille de la nation.</summary>
    public double AttachLegitimacyCost => Balance.Current.AttachLegitimacyCost * NationCostScale;

    /// <summary>
    /// Action du joueur : fonder une préfecture dans <paramref name="parentId"/> (l'État ou une préfecture), qui regroupe
    /// les entités choisies (communes ou préfectures, au moins 2), gratuitement rattachées. Son bâtiment se construit dans
    /// le chef-lieu, une commune contenue dans ces entités, qui paie le chantier ; ses emplois sont ceux du type plus 5 par
    /// entité contenue, payés par le trésor de l'État. Elle coûte de la légitimité dans le pôle choisi (voir
    /// <see cref="PrefectureLegitimacyCostOf"/>), sans charte. Renvoie null, sans rien dépenser, si c'est impossible.
    /// </summary>
    public Structure? TryFoundPrefecture(
        string name,
        string parentId,
        IReadOnlyList<string> memberIds,
        string seatCommuneId,
        PaymentMode mode,
        Orientation legitimacyPole)
    {
        var settings = Balance.Current;
        var structures = World.Structures;
        var members = memberIds.Distinct().Select(id => structures.GetValueOrDefault(id)).ToList();
        if (!structures.TryGetValue(parentId, out var parent)
            || parent.Level is not (EntityLevel.State or EntityLevel.Prefecture)
            || members.Count < settings.PrefectureMinimumMembers
            || members.Any(m => m is not { Level: EntityLevel.Commune or EntityLevel.Prefecture }
                // Pas de boucle : une entité ne peut pas regrouper ce qui la contient.
                || StructureTree.IsWithin(parent, m.Id, structures)
                // Une commune ne quitte pas une préfecture dont elle est le chef-lieu.
                || LeavesSeat(m, parentId))
            || !structures.TryGetValue(seatCommuneId, out var seat)
            || !seat.IsCommune
            || !members.Any(m => StructureTree.IsWithin(seat, m!.Id, structures))
            || !World.BuildingTypes.TryGetValue(PrefectureTypeId, out var type)
            || !type.CanBeBuilt
            || World.Legitimacy[legitimacyPole] < PrefectureLegitimacyCostOf(memberIds))
            return null;

        var number = 1;
        while (World.IsIdTaken($"prefecture-{number}"))
            number++;
        var prefecture = new Structure($"prefecture-{number}", name, StructureKind.PoliticalEntity, parentId,
            EntityType: EntityLevel.Prefecture, SeatId: seatCommuneId);
        var jobs = PrefectureJobsFor(type, members.Count);
        var building = type.Build($"{prefecture.Id}-siege", prefecture.Id, name: $"{type.Name} ({name})") with { Jobs = jobs, Capacity = jobs };
        if (World.IsIdTaken(building.Id)
            || !Construction.TryPay(type.Construction!.Materials, mode, Market.StateSellerId(seatCommuneId), World.EnvelopeOf(BudgetItem.Construction),
                World.MarketOf(seatCommuneId), World.ConstructionReserveOf(seatCommuneId)))
            return null;

        World.Legitimacy.TrySpend(legitimacyPole, PrefectureLegitimacyCostOf(memberIds), LegitimacySpendingKind.Prefecture, prefecture.Id);
        World.AddStructure(prefecture);
        foreach (var member in members)
            World.UpdateStructure(member! with { ParentId = prefecture.Id });
        World.AddConstructionSite(new ConstructionSite(building, type.Construction.Days));
        RefreshPrefectureJobs();
        return prefecture;
    }

    /// <summary>
    /// Action du joueur : rattacher une commune ou une préfecture à une autre entité (l'État ou une préfecture), contre de
    /// la légitimité dans le pôle choisi. Refusé si cela crée une boucle, si l'entité y est déjà, ou si une commune
    /// quitterait une préfecture dont elle est le chef-lieu.
    /// </summary>
    public bool TryAttach(string entityId, string newParentId, Orientation legitimacyPole)
    {
        if (!CanAttach(entityId, newParentId)
            || !World.Legitimacy.TrySpend(legitimacyPole, AttachLegitimacyCost, LegitimacySpendingKind.Attach, entityId))
            return false;

        World.UpdateStructure(World.Structures[entityId] with { ParentId = newParentId });
        RefreshPrefectureJobs();
        return true;
    }

    /// <summary>
    /// Vrai si l'entité (commune ou préfecture) peut être rattachée à <paramref name="newParentId"/> (l'État ou une
    /// préfecture), sans compter la légitimité : pas de boucle, pas déjà là, et pas de chef-lieu qui quitte sa préfecture.
    /// </summary>
    public bool CanAttach(string entityId, string newParentId)
    {
        var structures = World.Structures;
        return structures.TryGetValue(entityId, out var entity)
            && entity.Level is EntityLevel.Commune or EntityLevel.Prefecture
            && structures.TryGetValue(newParentId, out var parent)
            && parent.Level is EntityLevel.State or EntityLevel.Prefecture
            && entity.ParentId != newParentId
            && !StructureTree.IsWithin(parent, entity.Id, structures)
            && !LeavesSeat(entity, newParentId);
    }

    /// <summary>Emplois du bâtiment d'une préfecture : ceux du type, plus quelques-uns par entité contenue.</summary>
    public static int PrefectureJobsFor(BuildingType type, int entities) => type.Jobs + Balance.Current.PrefectureJobsPerEntity * entities;

    /// <summary>
    /// Remet les emplois du bâtiment de chaque préfecture à la taille de ce qu'elle contient (après une fondation, un
    /// rattachement ou la fin du chantier). Un bâtiment dont le joueur a fixé les emplois à la main n'est pas touché.
    /// </summary>
    public void RefreshPrefectureJobs()
    {
        if (!World.BuildingTypes.TryGetValue(PrefectureTypeId, out var type))
            return;

        foreach (var prefecture in World.Structures.Values.Where(s => s.IsPrefecture).ToList())
        {
            var entities = World.Structures.Values.Count(s => s.ParentId == prefecture.Id && s.Kind == StructureKind.PoliticalEntity);
            var jobs = PrefectureJobsFor(type, entities);
            foreach (var building in World.Structures.Values
                         .Where(s => s.ParentId == prefecture.Id && s.TypeId == PrefectureTypeId && !s.JobsSetByPlayer && s.JobCapacity != jobs)
                         .ToList())
                World.UpdateStructure(building with { Jobs = jobs, Capacity = jobs });
        }
    }

    /// <summary>Vrai si l'entité, placée dans <paramref name="newParentId"/>, sortirait d'une préfecture dont elle contient le chef-lieu.</summary>
    private bool LeavesSeat(Structure entity, string newParentId)
    {
        var structures = World.Structures;
        for (var current = entity.ParentId is { } p ? structures.GetValueOrDefault(p) : null; current is not null;
             current = current.ParentId is { } parentId ? structures.GetValueOrDefault(parentId) : null)
        {
            if (!current.IsPrefecture || current.SeatId is not { } seatId || !structures.TryGetValue(seatId, out var seat))
                continue;
            var stillInside = newParentId == current.Id
                || (structures.TryGetValue(newParentId, out var target) && StructureTree.IsWithin(target, current.Id, structures));
            if (!stillInside && StructureTree.IsWithin(seat, entity.Id, structures))
                return true;
        }

        return false;
    }

    /// <summary>Ancienne signature, gardée pour l'interface : le coût passé n'est plus utilisé, c'est celui du camp de base.</summary>
    [Obsolete("Le coût de construction vient du type camp de base : utiliser TryFoundCommune(name, payingCommuneId, mode, pole, legitimacyCost).")]
    public Structure? TryFoundCommune(string name, string payingCommuneId, IReadOnlyList<MaterialCost> cost, PaymentMode mode,
        Orientation legitimacyPole, double legitimacyCost) =>
        TryFoundCommune(name, payingCommuneId, mode, legitimacyPole, legitimacyCost);

    /// <summary>
    /// Les changements de lois entrés en vigueur au 1er du mois. Le jeu se met alors en pause et les montre au joueur
    /// (lois votées, changées ou abrogées).
    /// </summary>
    public event Action<IReadOnlyList<LawDraft>>? LawsEnacted;

    /// <summary>
    /// Commission des lois : copie un modèle dans un nouveau brouillon, à son niveau par défaut ou à celui donné.
    /// Renvoie l'identifiant du brouillon, ou null si le modèle n'existe pas.
    /// </summary>
    public string? DraftFromTemplate(string templateId, string? level = null)
    {
        if (World.LawCatalogue.TemplateOf(templateId) is not { } template)
            return null;

        var draft = new LawDraft(World.LawCommission.NewId("brouillon"), LawChangeKind.Enact,
            template.ToLaw(World.LawCommission.NewId("loi"), level));
        World.LawCommission.Add(draft);
        return draft.Id;
    }

    /// <summary>
    /// Commission des lois, éditeur libre (décidé le 12 novembre 2026) : un brouillon de loi vierge, nommé, dans un domaine
    /// et à un niveau, sans brique ; il ne se vote qu'une fois qu'il en a une. Renvoie l'identifiant du brouillon, ou null
    /// si le domaine ou le niveau n'existe pas.
    /// </summary>
    public string? DraftBlank(string name, string domainId, string levelId)
    {
        if (World.LawCatalogue.DomainOf(domainId) is null || !World.Structures.ContainsKey(levelId))
            return null;

        var draft = new LawDraft(World.LawCommission.NewId("brouillon"), LawChangeKind.Enact,
            new Law(World.LawCommission.NewId("loi"), name, domainId, levelId, []));
        World.LawCommission.Add(draft);
        return draft.Id;
    }

    /// <summary>Commission des lois : un brouillon qui change les curseurs d'une loi en vigueur (au départ, les mêmes).</summary>
    public string? DraftAmendment(string lawId) => DraftChange(lawId, LawChangeKind.Amend);

    /// <summary>Commission des lois : un brouillon qui abroge une loi en vigueur.</summary>
    public string? DraftRepeal(string lawId) => DraftChange(lawId, LawChangeKind.Repeal);

    private string? DraftChange(string lawId, LawChangeKind kind)
    {
        if (World.Laws.FirstOrDefault(l => l.Id == lawId) is not { } law)
            return null;

        var draft = new LawDraft(World.LawCommission.NewId("brouillon"), kind, law);
        World.LawCommission.Add(draft);
        return draft.Id;
    }

    /// <summary>
    /// Commission des lois : remplace la loi d'un brouillon (curseurs, sujets, cases « verrouillée », niveau d'une
    /// nouvelle loi). Une loi en vigueur garde son niveau et son domaine. Renvoie false si le brouillon n'existe pas.
    /// </summary>
    public bool UpdateDraft(string draftId, Law law)
    {
        if (World.LawCommission.DraftOf(draftId) is not { } draft)
            return false;

        if (draft.Kind != LawChangeKind.Enact)
            law = law with { StructureId = draft.Law.StructureId, Domain = draft.Law.Domain };
        return World.LawCommission.Update(draftId, law);
    }

    public bool DeleteDraft(string draftId) => World.LawCommission.Remove(draftId);

    /// <summary>Coût d'un brouillon s'il était voté maintenant, par pôle : il suit les membres concernés du moment.</summary>
    public IReadOnlyDictionary<Orientation, double> CostOf(string draftId) =>
        World.LawCommission.DraftOf(draftId) is { } draft ? CostOf(draft) : new Dictionary<Orientation, double>();

    private Dictionary<Orientation, double> CostOf(LawDraft draft)
    {
        var current = World.Laws.FirstOrDefault(l => l.Id == draft.Law.Id);
        return draft.Kind switch
        {
            LawChangeKind.Enact => LawCost.Of(World.LawBook, World.Classes, null, draft.Law),
            LawChangeKind.Amend => LawCost.Of(World.LawBook, World.Classes, current, draft.Law),
            _ => LawCost.Of(World.LawBook, World.Classes, current, null),
        };
    }

    /// <summary>
    /// Commission des lois : vote un brouillon. La légitimité est payée tout de suite et le coût est figé ; le changement
    /// entre en vigueur le 1er du mois suivant. Rien ne change si le vote est impossible (loi non valable, niveau inconnu,
    /// brique verrouillée plus haut, loi déjà en attente, légitimité insuffisante).
    /// </summary>
    public LawVoteOutcome TryVote(string draftId)
    {
        if (World.LawCommission.DraftOf(draftId) is not { } draft)
            return LawVoteOutcome.UnknownDraft;
        if (!World.Structures.ContainsKey(draft.Law.StructureId))
            return LawVoteOutcome.UnknownLevel;
        if (draft.Kind != LawChangeKind.Repeal && World.LawCatalogue.ProblemWith(draft.Law) is not null)
            return LawVoteOutcome.Invalid;
        if (draft.Kind != LawChangeKind.Enact && World.Laws.All(l => l.Id != draft.Law.Id))
            return LawVoteOutcome.UnknownDraft;
        if (World.LawCommission.Voted.Any(v => v.Draft.Law.Id == draft.Law.Id))
            return LawVoteOutcome.AlreadyVoted;

        var book = World.LawBook;
        if (draft.Kind != LawChangeKind.Repeal && draft.Law.Bricks.Any(s => book.IsLockedAbove(draft.Law, s)))
            return LawVoteOutcome.LockedAbove;

        // Voter à crédit (décidé le 23 novembre 2026) : un pôle peut s'endetter jusqu'au plafond ; les classes qui
        // acceptent la loi remboursent ensuite la dette.
        var cost = CostOf(draft);
        var maxDebt = Balance.Current.LegitimacyMaxDebt;
        if (cost.Any(c => !World.Legitimacy.CanSpendOnCredit(c.Key, c.Value, maxDebt)))
            return LawVoteOutcome.NotEnoughLegitimacy;

        foreach (var (pole, amount) in cost)
            World.Legitimacy.TrySpendOnCredit(pole, amount, maxDebt, LegitimacySpendingKind.LawVote, draft.Law.Name);
        World.LawCommission.MarkVoted(draft, cost);
        return LawVoteOutcome.Voted;
    }

    /// <summary>Commission des lois : annule un vote avant le 1er du mois. La légitimité est rendue et la loi redevient un brouillon.</summary>
    public bool CancelVote(string draftId)
    {
        if (World.LawCommission.Unvote(draftId) is not { } voted)
            return false;

        foreach (var (pole, amount) in voted.Paid)
            World.Legitimacy.Add(pole, amount, LegitimacySpendingKind.VoteCancelled, voted.Draft.Law.Name);
        return true;
    }

    /// <summary>
    /// Met en vigueur une loi tout de suite, sans la payer : les lois choisies au début de la partie. Renvoie false si
    /// la loi n'est pas valable.
    /// </summary>
    public bool EnactAtStart(Law law)
    {
        if (!World.Structures.ContainsKey(law.StructureId) || World.LawCatalogue.ProblemWith(law) is not null)
            return false;

        World.AddLaw(law);
        return true;
    }

    /// <summary>Au 1er du mois : les changements votés entrent en vigueur, avant les licenciements et les embauches.</summary>
    private void EnactVotedLaws()
    {
        var enacted = new List<LawDraft>();
        foreach (var voted in World.LawCommission.TakeVoted())
        {
            var draft = voted.Draft;
            switch (draft.Kind)
            {
                case LawChangeKind.Enact:
                    World.AddLaw(draft.Law);
                    break;
                case LawChangeKind.Amend:
                    World.ReplaceLaw(draft.Law);
                    break;
                default:
                    World.RemoveLaw(draft.Law.Id);
                    break;
            }

            enacted.Add(draft);
        }

        if (enacted.Count == 0)
            return;

        LawsEnacted?.Invoke(enacted);
        NationalizeForbiddenCompanies();
    }

    /// <summary>
    /// Donne à une commune sa réserve de chantier de fondation : des matériaux de construction (ceux du camp de base),
    /// en quantité réglée dans l'équilibrage.
    /// </summary>
    public void GiveFoundingMaterials(string communeId)
    {
        if (World.BuildingTypes.GetValueOrDefault(BaseCampTypeId)?.Construction is { } cost)
            World.ConstructionReserveOf(communeId).Add(cost.ProductId, Balance.Current.FoundingMaterials);
    }

    /// <summary>Dotation versée à la fondation : 60 jours de salaires de production du camp de base.</summary>
    public static double FoundingGrantOf(BuildingType camp) =>
        FoundingGrantDays * camp.Jobs * Staffing.WageOf(camp.DailyWage, camp.Wages, camp.MainRank);

    // Ordre du document de règles : emplois, production et salaires, énergie des bâtiments, échanges entre communes,
    // logement, école, consommation, ventes, bénéfices des entreprises versés aux propriétaires, impôts, légitimité,
    // puis perte de ce qui déborde du stockage ou se périme (y compris dans les stocks gardés hors des bâtiments de
    // stockage), et enfin les chantiers. L'énergie prise après la production d'un jour règle la
    // production du lendemain, et le logement et l'école du jour même.
    // Les décisions du joueur et les événements restent à écrire.
    private void RunDailyCalculations()
    {
        var communeIds = World.Communes.Select(c => c.Id).ToList();
        // Charge et efficacité de chaque entité politique, d'après les habitants et les bâtiments du matin.
        World.UpdateExtension();
        // Ce que les lois imposent à chaque bâtiment, lu une fois pour la journée.
        var laws = World.LawBook;
        World.ApplyLaws(laws);

        // Les chargements en route avancent d'un jour ; ceux qui arrivent entrent au marché, ou dans la réserve du client
        // d'une ligne (décidé le 20 novembre 2026).
        if (World.ShipmentsInTransit.Count > 0)
        {
            var onTheWay = Trade.Advance(World.ShipmentsInTransit.ToList(), World.Products, World.MarketOf, World.ReserveOf);
            World.ShipmentsInTransit.Clear();
            World.ShipmentsInTransit.AddRange(onTheWay);
        }

        // Deux communes n'échangent que si l'une a un bâtiment logistique ; lu une fois pour la journée.
        var hasLogistics = communeIds.ToDictionary(c => c, World.HasLogisticsBuilding);
        bool CanTrade(string from, string to) => Trade.CanTrade(from, to, c => hasLogistics.GetValueOrDefault(c));
        // La capacité d'envoi de chaque commune pour la journée, partagée entre les lignes puis les échanges du soir.
        var capacity = communeIds.ToDictionary(c => c, World.ShippingCapacity);
        var capacityLeft = new Dictionary<string, double>(capacity);
        string CapacityHolder(string from, string to) => Trade.CapacityHolder(from, to, c => hasLogistics.GetValueOrDefault(c));

        World.Workers = Employment.Assign(World.Structures, World.Classes);
        World.PollutionByCommune = Pollution.PerResidentByCommune(
            World.Structures, World.Workers, World.Classes,
            World.CommunePositions.Count > 0
                ? (from, to) => GeographicEconomics.DistantPollutionShare(from, to, World.CommunePositions)
                : null);
        // La caisse de chaque entreprise au début de la journée : son bénéfice du jour décide de ce qu'elle verse.
        var fundsAtDayStart = World.Structures.Values.Where(Companies.HasCompanyFund).ToDictionary(b => b.Id, b => World.CompanyFundOf(b).Balance);
        var supplyShares = new Dictionary<string, double>();
        // Les lignes d'approvisionnement : une part de la production du jour part d'abord aux clients. Dans une même
        // commune, après chaque étape de la chaîne, pour servir le jour même ; vers une autre commune, après la journée
        // de travail, pour servir le lendemain (décidé le 20 novembre 2026).
        var (localLines, distantLines) = SplitSupplyLines();
        var deliveries = new Dictionary<string, double>();
        void Deliver(IReadOnlyList<SupplyLine> lines, IReadOnlyDictionary<string, double> produced)
        {
            if (lines.Count == 0)
                return;
            foreach (var (lineId, quantity) in SupplyLines.RunDaily(lines, World.Structures, produced, World.Workers,
                         World.MarketOf, World.ReserveOf, World.Treasury, World.CommuneFundOf, TransportLossBetween, World.RoadTraffic.Add, CanTrade,
                         World.ShipmentsInTransit.Add, TravelDaysBetween,
                         (clientId, productId) => World.ShipmentsInTransit.Where(s => s.ClientId == clientId && s.ProductId == productId).Sum(s => s.Quantity),
                         CapacityHolder, capacityLeft))
                deliveries[lineId] = deliveries.GetValueOrDefault(lineId) + quantity;
        }

        World.Produced = WorkDay.Run(World.Structures, World.Workers, World.Classes, World.Treasury, World.MarketOf, World.CommuneFundOf, World.Wages,
            World.CompanyFundOf, World.EnergyEffectOf, World.PublicEfficiencyOf, supplyShares,
            // La réserve d'un bâtiment, s'il en a une, ou s'il peut en recevoir une dans la journée par une ligne de sa commune.
            id => World.ReserveContentsOf(id).Count > 0 || localLines.Any(l => l.ClientId == id) ? World.ReserveOf(id) : null,
            // Les réglages d'achat des bâtiments publics (n'acheter que par ses lignes, vendeurs refusés).
            World.HasSupplySettings ? building => World.SupplySettingsOf(building.Id).PurchaseRulesFor(building, World.Structures) : null,
            stageOutput => Deliver(localLines, stageOutput),
            World.ToolEffectOf);
        World.SupplyShares = supplyShares;
        Deliver(distantLines, World.Produced);
        if (World.SupplyLines.Count > 0)
            World.SupplyLineDeliveries = deliveries;
        // Les réglages de vente : réserve hors vente et refus d'envoi, sur la production du jour.
        if (World.HasSupplySettings || World.AppliedSales.Count > 0)
            World.AppliedSales = SupplySettings.ApplySales(World.Structures, World.SupplySettingsOf, World.Produced, World.MarketOf, World.AppliedSales);
        AddBuildingTraffic();
        CountProduction();
        World.EnergyShares = Energy.RunDaily(World.Structures, World.TierOf, Clock.Season, World.MarketOf, World.Treasury, World.CommuneFundOf,
            World.CompanyFundOf, World.ClassesById, World.FuelStockOf, World.HeatsAllYear);
        // Les outils s'usent ; chaque bâtiment rachète ce qui lui manque (décidé le 20 novembre 2026).
        Tools.RunDaily(World.Structures, World.Products, World.MarketOf, World.Treasury, World.CommuneFundOf, World.CompanyFundOf,
            World.ClassesById, World.ToolStockOf);
        // Les charrettes s'usent ; l'État de chaque commune achète ce qui manque au nombre voulu.
        Carts.RunDaily(World.Communes, World.Products, World.WantedCartsOf, World.CartStockOf, World.MarketOf, World.Treasury, World.CommuneFundOf);
        Housing.RunDaily(World.Structures, World.Classes, World.Treasury, World.CompanyFundOf, World.EnergyEffectOf, laws.HasHousingPriority);
        Education.RunDaily(World.Structures, World.Classes, World.Treasury, World.CompanyFundOf, World.EnergyEffectOf);

        foreach (var communeId in communeIds)
            Consumption.RunDaily(World.Classes.Where(c => c.CommuneId == communeId).ToList(), World.Products, World.MarketOf(communeId));

        // Les échanges entre communes, le soir, sur ce qui reste (décidé le 20 novembre 2026) : tous les produits qui se
        // stockent, au-delà de ce qu'il faudra demain aux habitants et aux bâtiments de la commune. Ils arrivent le
        // lendemain matin : partis avant les repas, ils compteraient deux fois (le jour même et pour le lendemain).
        var buildingDemand = Trade.BuildingDemand(World.Structures, World.Workers, World.TierOf, Clock.Season, World.HeatsAllYear,
            World.ToolsOf, World.Products.FirstOrDefault(p => p.Id == Tools.ProductId)?.UsageDays);
        // Ce qui est déjà en route vers chaque commune (hors lignes), lu avant les départs du soir.
        var comingTo = new Dictionary<(string, string), double>();
        foreach (var shipment in World.ShipmentsInTransit.Where(s => s.ClientId is null))
            comingTo[(shipment.Destination, shipment.ProductId)] = comingTo.GetValueOrDefault((shipment.Destination, shipment.ProductId)) + shipment.Quantity;
        var producedToday = new Dictionary<(string, string), double>();
        foreach (var (buildingId, quantity) in World.Produced)
        {
            if (quantity > 0 && World.Structures.TryGetValue(buildingId, out var building) && building.Production is { } production
                && World.CommuneOf(building) is { } communeId)
                producedToday[(communeId, production.ProductId)] = producedToday.GetValueOrDefault((communeId, production.ProductId)) + quantity;
        }
        Trade.RunDaily(
            communeIds, World.Classes, World.Products, World.MarketOf,
            (communeId, productId) => World.StorageOf(communeId).FreeFor(productId),
            // La perte de transport selon la distance, réduite par un chemin ou une route ; le trafic crée les routes.
            World.CommunePositions.Count > 0
                ? (from, to) => GeographicEconomics.TransportLoss(from, to, World.CommunePositions) * World.Roads.LossFactor(from, to)
                : null,
            World.RoadTraffic.Add,
            (communeId, productId) => buildingDemand.TryGetValue(communeId, out var byProduct) ? byProduct.GetValueOrDefault(productId) : 0,
            CanTrade,
            shipment =>
            {
                if (shipment.Quantity > 0)
                    World.ShipmentsInTransit.Add(shipment);
            },
            (communeId, productId) => producedToday.TryGetValue((communeId, productId), out var quantity) ? quantity : 0,
            TravelDaysBetween,
            (communeId, productId) => comingTo.GetValueOrDefault((communeId, productId)),
            CapacityHolder, capacityLeft);
        World.ShippingCapacityUsed = capacity.ToDictionary(c => c.Key, c => c.Value - capacityLeft.GetValueOrDefault(c.Key));

        PaySellers();
        Companies.PayDividends(World.Structures, World.ClassesById, World.CompanyFundOf, World.PriceOf, b => fundsAtDayStart.GetValueOrDefault(b.Id));
        Taxation.CollectDaily(World.Classes, World.StateTaxRate, World.CommuneTaxRateOf, World.Treasury, World.CommuneFundOf, laws.TaxFactorOf,
            World.StateCollectionIn, World.CommuneCollectionIn);
        World.Legitimacy.Ledger.BeginDay(Clock.Today);
        LawEffects.ApplyDaily(World.Classes, laws, World.Legitimacy);
        Storage.ApplyDaily(communeIds, World.Structures, World.Products, World.MarketOf);
        SpoilOutsideStorage();

        // Les chantiers finis en fin de journée : leurs bâtiments travaillent dès le lendemain.
        var completed = World.AdvanceConstructionSites();
        // Un rattachement pendant le chantier d'une préfecture a pu changer ce qu'elle contient.
        if (completed.Any(b => b.TypeId == PrefectureTypeId))
            RefreshPrefectureJobs();
        foreach (var building in completed)
        {
            // Un nouveau bâtiment reçoit son stock d'outils visé au complet (décidé le 20 novembre 2026).
            if (World.Structures.TryGetValue(building.Id, out var placed))
                Tools.Equip(placed, World.ToolStockOf(placed));
            BuildingCompleted?.Invoke(building);
        }
    }

    private void PaySellers()
    {
        foreach (var market in World.Markets.Values)
        {
            foreach (var (sellerId, amount) in market.CollectEarnings())
            {
                // Ce que l'État a produit et vendu dans une commune revient à la caisse de cette commune.
                if (Market.CommuneOfStateSeller(sellerId) is { } communeId)
                    World.CommuneFundOf(communeId).Deposit(amount);
                // Une entreprise touche ses ventes dans sa caisse ; si elle est devenue publique entre-temps, elles
                // vont à la caisse de sa commune.
                else if (Market.BuildingOfCompanySeller(sellerId) is { } buildingId && World.Structures.TryGetValue(buildingId, out var building))
                {
                    if (Companies.HasCompanyFund(building))
                    {
                        // La TVA sur sa valeur ajoutée part d'abord, à l'État et à la commune (les lois peuvent la baisser ou en
                        // exonérer) ; le reste entre dans la caisse.
                        var commune = World.CommuneOf(building);
                        var valueAdded = amount * Taxation.ValueAddedShare(building, World.PriceOf) * building.Rules.CompanyTaxFactor;
                        var toState = valueAdded * Balance.Current.VatStateRate;
                        var toCommune = commune is null ? 0 : valueAdded * Balance.Current.VatCommuneRate;
                        World.Treasury.Deposit(toState);
                        if (commune is not null)
                            World.CommuneFundOf(commune).Deposit(toCommune);
                        World.CompanyFundOf(building).Deposit(amount - toState - toCommune);
                    }
                    else if (World.CommuneOf(building) is { } buildingCommune)
                        World.CommuneFundOf(buildingCommune).Deposit(amount);
                }
                else if (World.ClassesById.TryGetValue(sellerId, out var seller))
                    seller.ReceiveIncome(amount);
            }
        }
    }

    /// <summary>
    /// Routes des bâtiments (décidé le 12 novembre 2026) : ce que produit un bâtiment posé sur la carte fait du trafic
    /// entre lui et sa commune, qui crée sentier, chemin et route comme entre deux communes (mêmes seuils, payés par la
    /// commune). Ce trafic ne donne pas de perte de transport : la production entre toujours entière au marché.
    /// </summary>
    private void AddBuildingTraffic()
    {
        foreach (var (buildingId, quantity) in World.Produced)
        {
            if (quantity <= 0 || !World.BuildingPositions.ContainsKey(buildingId)
                || !World.Structures.TryGetValue(buildingId, out var building) || World.CommuneOf(building) is not { } communeId)
                continue;
            World.RoadTraffic.Add(buildingId, communeId, quantity);
        }
    }

    /// <summary>
    /// Les lignes d'approvisionnement entre deux bâtiments d'une même commune, et les autres (entre deux communes, ou
    /// dont un bâtiment n'existe plus), chacune dans l'ordre de la liste.
    /// </summary>
    private (List<SupplyLine> Local, List<SupplyLine> Distant) SplitSupplyLines()
    {
        string? CommuneOfBuilding(string id) => World.Structures.TryGetValue(id, out var building) ? World.CommuneOf(building) : null;
        var local = new List<SupplyLine>();
        var distant = new List<SupplyLine>();
        foreach (var line in World.SupplyLines)
        {
            var origin = CommuneOfBuilding(line.SupplierId);
            if (origin is not null && origin == CommuneOfBuilding(line.ClientId))
                local.Add(line);
            else
                distant.Add(line);
        }

        return (local, distant);
    }

    /// <summary>
    /// Fin de journée (décidé le 20 novembre 2026) : les stocks gardés hors des bâtiments de stockage se périment comme
    /// dans un stockage général ; ce qui s'y perd compte comme perdu au stockage dans leur commune.
    /// </summary>
    private void SpoilOutsideStorage()
    {
        foreach (var (communeId, stock) in World.StocksOutsideStorage())
        {
            var lost = Storage.SpoilOutsideStorage(stock, World.Products);
            if (communeId is null || lost.Count == 0 || !World.Markets.TryGetValue(communeId, out var market))
                continue;
            foreach (var (productId, quantity) in lost)
                market.CountLost(productId, quantity);
        }
    }

    /// <summary>Ajoute la production du jour aux totaux par commune et par produit (statistiques ; aucune règle).</summary>
    private void CountProduction()
    {
        foreach (var (buildingId, quantity) in World.Produced)
        {
            if (quantity <= 0 || !World.Structures.TryGetValue(buildingId, out var building)
                || building.Production is not { } production || World.CommuneOf(building) is not { } communeId)
                continue;
            World.CountProduced(communeId, production.ProductId, quantity);
        }
    }

    /// <summary>La position d'un lieu relié par les routes : une commune, ou un bâtiment posé sur la carte.</summary>
    private Point2D? PlaceOf(string placeId) =>
        World.CommunePositions.TryGetValue(placeId, out var commune) ? commune
        : World.BuildingPositions.TryGetValue(placeId, out var building) ? building
        : null;

    // Promotions par l'éducation, puis bilan fiscal et statistique (à écrire).
    private void RunYearlyCalculations()
    {
        // L'entretien des routes ; une route mal entretenue descend d'un niveau.
        // Le 1er janvier est aussi un 1er du mois : les changements du mois restent, l'entretien s'y ajoute.
        RoadChanges = [.. RoadChanges, .. Roads.RunYearly(World.Roads, World.FundOf, Balance.Current)];

        foreach (var created in ClassTransitions.RunYearly(World.Classes))
            World.AddClass(created);

        YearPassed?.Invoke();
    }

    private void RunMonthlyCalculations()
    {
        EnactVotedLaws();
        RunBudgetMonthly();

        // La légitimité s'use (décidé le 23 novembre 2026) : un douzième de l'usure annuelle, chaque mois.
        World.Legitimacy.Erode(1 - Math.Pow(1 - Balance.Current.LegitimacyYearlyErosion, 1.0 / 12));

        // Le trafic du mois fini (entre communes, et entre un bâtiment posé et sa commune) crée ou améliore les routes,
        // si la structure qui les paie en a les moyens.
        RoadChanges = Roads.RunMonthly(World.Roads, World.RoadTraffic.EndMonth(), PlaceOf, World.RoadPlanner, World.RoadPayerOf, World.FundOf, Balance.Current);

        foreach (var building in Layoffs.RunMonthly(World.Structures, World.MarketOf, World.Wages, World.CompanyFundOf))
            World.UpdateStructure(building);
    }

    // Passages entre classes, commune par commune, puis migration. Prévu : climat, revendications, recherche.
    private void RunSeasonalCalculations()
    {
        foreach (var socialClass in World.Classes)
            socialClass.Grow();

        foreach (var created in ClassTransitions.RunSeasonal(World.Classes, World.Structures, World.PollutionByCommune, World.Wages.IsStruggling, World.Promise))
            World.AddClass(created);

        foreach (var created in Migration.RunSeasonal(World.Structures, World.Classes, World.CharterOf))
            World.AddClass(created);

        SeasonPassed?.Invoke();
    }
}
