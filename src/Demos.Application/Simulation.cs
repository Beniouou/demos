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
public sealed class Simulation(World world, GameClock clock)
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

    public World World { get; } = world;
    public GameClock Clock { get; } = clock;

    public event Action? SeasonPassed;
    public event Action? YearPassed;

    public void AdvanceDay()
    {
        var change = Clock.AdvanceDay();

        // Au changement de mois, les lois votées entrent en vigueur, puis les entreprises licencient ou réembauchent,
        // avant la journée de travail.
        if (change.HasFlag(CalendarChange.NewMonth))
            RunMonthlyCalculations();

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
    public bool TryBuild(Structure structure, PaymentMode mode, Point2D? position = null)
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
            return false;

        var productId = structure.Production?.ProductId;
        if (World.NeedsExtractionSite(typeId, productId)
            && (position is not { } site || World.ExtractionSiteProblem(typeId, productId, communeId, site) is not null))
            return false;

        if (!Construction.TryPay(type.Construction!.Materials, mode, Market.StateSellerId(communeId), World.Treasury, World.MarketOf(communeId),
                World.ConstructionReserveOf(communeId)))
            return false;

        if (position is { } placed)
            World.SetBuildingPosition(structure.Id, placed);
        World.AddConstructionSite(new ConstructionSite(structure, type.Construction.Days));
        return true;
    }

    /// <summary>Ancienne signature, gardée pour l'interface : le coût passé n'est plus utilisé, c'est celui du type.</summary>
    [Obsolete("Le coût de construction vient du type de bâtiment : utiliser TryBuild(structure, mode).")]
    public bool TryBuild(Structure structure, IReadOnlyList<MaterialCost> cost, PaymentMode mode) => TryBuild(structure, mode);

    /// <summary>
    /// Outil d'administration : remplace les paramètres d'un bâtiment (propriétaire, emplois, salaire, production,
    /// places, loyer). Les effets se voient dès le jour suivant.
    /// </summary>
    public void UpdateBuilding(Structure building) => World.UpdateStructure(building);

    /// <summary>Outil d'administration : règle l'impôt d'État (part de la richesse versée chaque jour).</summary>
    public void SetStateTaxRate(double rate) => World.StateTaxRate = rate;

    /// <summary>Outil d'administration : règle l'impôt d'une commune, versé à sa caisse.</summary>
    public void SetCommuneTaxRate(string communeId, double rate) => World.SetCommuneTaxRate(communeId, rate);

    /// <summary>Outil d'administration : fixe le prix d'un produit dans toutes les communes.</summary>
    public void SetPrice(string productId, double price) => World.SetPrice(productId, price);

    /// <summary>Proposition : vendre un bâtiment public coûte 10 de légitimité libérale (LI).</summary>
    public const double SaleLegitimacyCost = 10;

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
        if (!World.Legitimacy.TrySpend(SalePole, SaleLegitimacyCost))
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

    /// <summary>Pôle d'une nationalisation : protectionniste (PR).</summary>
    public static readonly Orientation NationalizationPole = new(IdeologicalAxis.Property, Pole.A);

    /// <summary>Une entreprise est en difficulté quand elle a versé moins de 80 % des salaires dus sur 30 jours.</summary>
    public bool IsStruggling(Structure building) => World.Wages.IsStruggling(building.Id);

    public double NationalizationPriceOf(Structure building, NationalizationMode mode) =>
        Nationalization.PriceOf(mode, IsStruggling(building), DailyProfitOf(building));

    public double NationalizationLegitimacyCostOf(Structure building, NationalizationMode mode) =>
        Nationalization.LegitimacyCostOf(mode, IsStruggling(building));

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
        if (World.Treasury.Balance < price)
            return NationalizationOutcome.TreasuryTooPoor;
        if (!World.Legitimacy.TrySpend(NationalizationPole, legitimacyCost))
            return NationalizationOutcome.NotEnoughLegitimacy;

        World.Treasury.TrySpend(price);
        if (building.OwnerClassId is { } ownerId && World.ClassesById.TryGetValue(ownerId, out var owner))
            owner.Wealth += price;

        TakeIntoPublicHands(building, mode);
        return NationalizationOutcome.Done;
    }

    /// <summary>
    /// Action du joueur : bâtir un camp de base, qui fonde une nouvelle commune rattachée à l'État.
    /// Il coûte son coût de construction (celui du type camp de base), pris dans la commune qui l'envoie, de la
    /// légitimité (dans le pôle choisi), et la dotation que l'État verse du trésor à la caisse de la nouvelle commune.
    /// La commune est fondée tout de suite ; son camp de base est en chantier. Elle reçoit sa réserve de chantier de
    /// fondation. Le pôle choisi devient la charte de fondation de la commune : il attire les classes du même pôle
    /// et repousse celles du pôle opposé.
    /// Renvoie la nouvelle commune, ou null, sans rien dépenser, si c'est impossible.
    /// </summary>
    public Structure? TryFoundCommune(
        string name,
        string payingCommuneId,
        PaymentMode mode,
        Orientation legitimacyPole,
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
            || !campType.CanBeBuilt
            || World.Legitimacy[legitimacyPole] < legitimacyCost
            || !World.Treasury.TrySpend(grant))
            return null;

        if (!Construction.TryPay(campType.Construction!.Materials, mode, Market.StateSellerId(payingCommuneId), World.Treasury,
                World.MarketOf(payingCommuneId), World.ConstructionReserveOf(payingCommuneId)))
        {
            World.Treasury.Deposit(grant);
            return null;
        }

        World.Legitimacy.TrySpend(legitimacyPole, legitimacyCost);
        World.CommuneFundOf(commune.Id).Deposit(grant);
        World.AddStructure(commune);
        if (position.HasValue)
            World.SetCommunePosition(commune.Id, position.Value);
        World.AddConstructionSite(new ConstructionSite(camp, campType.Construction.Days));
        GiveFoundingMaterials(commune.Id);
        World.SetCharter(commune.Id, legitimacyPole);
        return commune;
    }

    /// <summary>
    /// Action du joueur : fonder une préfecture dans <paramref name="parentId"/> (l'État ou une préfecture), qui regroupe
    /// les entités choisies (communes ou préfectures, au moins 2), gratuitement rattachées. Son bâtiment se construit dans
    /// le chef-lieu, une commune contenue dans ces entités, qui paie le chantier ; ses emplois sont ceux du type plus 5 par
    /// entité contenue, payés par le trésor de l'État. Elle coûte de la légitimité dans le pôle choisi, sans charte.
    /// Renvoie null, sans rien dépenser, si c'est impossible.
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
            || World.Legitimacy[legitimacyPole] < settings.PrefectureLegitimacyCost)
            return null;

        var number = 1;
        while (World.IsIdTaken($"prefecture-{number}"))
            number++;
        var prefecture = new Structure($"prefecture-{number}", name, StructureKind.PoliticalEntity, parentId,
            EntityType: EntityLevel.Prefecture, SeatId: seatCommuneId);
        var jobs = PrefectureJobsFor(type, members.Count);
        var building = type.Build($"{prefecture.Id}-siege", prefecture.Id, name: $"{type.Name} ({name})") with { Jobs = jobs, Capacity = jobs };
        if (World.IsIdTaken(building.Id)
            || !Construction.TryPay(type.Construction!.Materials, mode, Market.StateSellerId(seatCommuneId), World.Treasury,
                World.MarketOf(seatCommuneId), World.ConstructionReserveOf(seatCommuneId)))
            return null;

        World.Legitimacy.TrySpend(legitimacyPole, settings.PrefectureLegitimacyCost);
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
            || !World.Legitimacy.TrySpend(legitimacyPole, Balance.Current.AttachLegitimacyCost))
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

        var cost = CostOf(draft);
        if (cost.Any(c => World.Legitimacy[c.Key] < c.Value))
            return LawVoteOutcome.NotEnoughLegitimacy;

        foreach (var (pole, amount) in cost)
            World.Legitimacy.TrySpend(pole, amount);
        World.LawCommission.MarkVoted(draft, cost);
        return LawVoteOutcome.Voted;
    }

    /// <summary>Commission des lois : annule un vote avant le 1er du mois. La légitimité est rendue et la loi redevient un brouillon.</summary>
    public bool CancelVote(string draftId)
    {
        if (World.LawCommission.Unvote(draftId) is not { } voted)
            return false;

        foreach (var (pole, amount) in voted.Paid)
            World.Legitimacy.Add(pole, amount);
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
    // puis perte de ce qui déborde du stockage ou se périme, et enfin les chantiers. L'énergie prise après la production d'un jour règle la
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


        World.Workers = Employment.Assign(World.Structures, World.Classes);
        World.PollutionByCommune = Pollution.PerResidentByCommune(
            World.Structures, World.Workers, World.Classes,
            World.CommunePositions.Count > 0
                ? (from, to) => GeographicEconomics.DistantPollutionShare(from, to, World.CommunePositions)
                : null);
        // La caisse de chaque entreprise au début de la journée : son bénéfice du jour décide de ce qu'elle verse.
        var fundsAtDayStart = World.Structures.Values.Where(Companies.HasCompanyFund).ToDictionary(b => b.Id, b => World.CompanyFundOf(b).Balance);
        World.Produced = WorkDay.Run(World.Structures, World.Workers, World.Classes, World.Treasury, World.MarketOf, World.CommuneFundOf, World.Wages,
            World.CompanyFundOf, World.EnergyEffectOf, World.PublicEfficiencyOf);
        World.EnergyShares = Energy.RunDaily(World.Structures, World.TierOf, Clock.Season, World.MarketOf, World.Treasury, World.CommuneFundOf,
            World.CompanyFundOf, World.ClassesById, World.FuelStockOf, World.HeatsAllYear);
        Trade.RunDaily(
            communeIds, World.Classes, World.Products, World.MarketOf,
            (communeId, productId) => World.StorageOf(communeId).FreeFor(productId),
            World.CommunePositions.Count > 0
                ? (from, to) => GeographicEconomics.TransportLoss(from, to, World.CommunePositions)
                : null);
        Housing.RunDaily(World.Structures, World.Classes, World.Treasury, World.CompanyFundOf, World.EnergyEffectOf, laws.HasHousingPriority);
        Education.RunDaily(World.Structures, World.Classes, World.Treasury, World.CompanyFundOf, World.EnergyEffectOf);

        foreach (var communeId in communeIds)
            Consumption.RunDaily(World.Classes.Where(c => c.CommuneId == communeId).ToList(), World.Products, World.MarketOf(communeId));

        PaySellers();
        Companies.PayDividends(World.Structures, World.ClassesById, World.CompanyFundOf, World.PriceOf, b => fundsAtDayStart.GetValueOrDefault(b.Id));
        Taxation.CollectDaily(World.Classes, World.StateTaxRate, World.CommuneTaxRateOf, World.Treasury, World.CommuneFundOf, laws.TaxFactorOf,
            World.StateCollectionIn, World.CommuneCollectionIn);
        LawEffects.ApplyDaily(World.Classes, laws, World.Legitimacy);
        Storage.ApplyDaily(communeIds, World.Structures, World.Products, World.MarketOf);

        // Les chantiers finis en fin de journée : leurs bâtiments travaillent dès le lendemain.
        var completed = World.AdvanceConstructionSites();
        // Un rattachement pendant le chantier d'une préfecture a pu changer ce qu'elle contient.
        if (completed.Any(b => b.TypeId == PrefectureTypeId))
            RefreshPrefectureJobs();
        foreach (var building in completed)
            BuildingCompleted?.Invoke(building);
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

    // Promotions par l'éducation, puis bilan fiscal et statistique (à écrire).
    private void RunYearlyCalculations()
    {
        foreach (var created in ClassTransitions.RunYearly(World.Classes))
            World.AddClass(created);

        YearPassed?.Invoke();
    }

    private void RunMonthlyCalculations()
    {
        EnactVotedLaws();

        foreach (var building in Layoffs.RunMonthly(World.Structures, World.MarketOf, World.Wages, World.CompanyFundOf))
            World.UpdateStructure(building);
    }

    // Passages entre classes, commune par commune, puis migration. Prévu : climat, revendications, recherche.
    private void RunSeasonalCalculations()
    {
        foreach (var socialClass in World.Classes)
            socialClass.Grow();

        foreach (var created in ClassTransitions.RunSeasonal(World.Classes, World.Structures, World.PollutionByCommune))
            World.AddClass(created);

        foreach (var created in Migration.RunSeasonal(World.Structures, World.Classes, World.CharterOf))
            World.AddClass(created);

        SeasonPassed?.Invoke();
    }
}
