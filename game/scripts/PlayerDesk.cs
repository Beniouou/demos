using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Rank = Demos.Domain.Population.Rank;

namespace Demos.Game;

/// <summary>
/// Le bureau du joueur : ce qu'il voit des bâtiments (des fiches, copies figées) et les décisions qu'il peut prendre
/// (vente, nationalisation, postes ouverts, recette, chauffage, impôts). Les écrans n'ont que ce bureau et
/// <see cref="GameView"/> : ils ne touchent jamais le moteur, et chaque décision passe par <see cref="Simulation"/>.
/// </summary>
public sealed class PlayerDesk(Simulation simulation)
{
    private World World => simulation.World;

    public BuildingSheet? SheetOf(string buildingId)
    {
        if (!World.Structures.TryGetValue(buildingId, out var building))
            return null;

        var isPublic = building.Ownership == Ownership.Public;
        var recipes = building.TypeId is { } typeId && World.BuildingTypes.TryGetValue(typeId, out var type)
            ? type.AvailableRecipes.ToList()
            : [];
        var heated = World.TierOf(building) == 1 && Energy.UnitsOf(building) > 0;
        var communeId = World.CommuneOf(building);

        return new BuildingSheet(
            building.Id,
            building.Name,
            InfoOf(building),
            isPublic,
            building.Jobs,
            building.JobCapacity,
            building.JobsSetByPlayer,
            recipes.Select(r => r.Label).ToList(),
            recipes.FindIndex(r => building.Production is { } current && r.Label == current.Label),
            heated ? Energy.HeatingFuels : [],
            Energy.HeatingFuels.ToList().IndexOf(building.HeatingProductId ?? Energy.DefaultHeating),
            World.Classes
                .Where(c => c.CommuneId == communeId && c.Population > 0)
                .Select(c => new BuyerEntry(c.Id, $"{c.Name} ({c.Wealth:N0})"))
                .ToList(),
            Privatization.MaximumPriceOf(simulation.DailyProfitOf(building)),
            World.Legitimacy[Simulation.SalePole],
            isPublic ? null : OfferOf(building, NationalizationMode.Buyout),
            isPublic ? null : OfferOf(building, NationalizationMode.Expropriation),
            World.Legitimacy[Simulation.NationalizationPole],
            simulation.IsStruggling(building));
    }

    public string Sell(string buildingId, string buyerClassId, double price) =>
        simulation.TrySellBuilding(buildingId, buyerClassId, price) switch
        {
            SaleOutcome.Sold => $"Vendu pour {price:N0}.",
            SaleOutcome.NotPublic => "Ce bâtiment n'est pas public.",
            SaleOutcome.BuyerNotInCommune => "L'acheteur ne vit pas dans la commune du bâtiment.",
            SaleOutcome.BuyerTooPoor => "L'acheteur n'a pas assez d'argent.",
            SaleOutcome.PriceAboveProfit => "Trop cher : le prix dépasse un an de bénéfice attendu.",
            _ => $"Pas assez de légitimité LI (il en faut {Simulation.SaleLegitimacyCost}).",
        };

    /// <summary>Reprend une entreprise privée ou coopérative : rachat payé par le trésor, ou expropriation sans payer.</summary>
    public string Nationalize(string buildingId, bool expropriate) =>
        Nationalize(buildingId, expropriate ? NationalizationMode.Expropriation : NationalizationMode.Buyout);

    private string Nationalize(string buildingId, NationalizationMode mode) =>
        simulation.TryNationalize(buildingId, mode) switch
        {
            NationalizationOutcome.Done => mode == NationalizationMode.Buyout ? "Racheté par l'État." : "Exproprié par l'État.",
            NationalizationOutcome.NotPrivate => "Ce bâtiment est déjà public.",
            NationalizationOutcome.TreasuryTooPoor => "Le trésor n'a pas assez d'argent.",
            _ => "Pas assez de légitimité PR.",
        };

    /// <summary>Fixe à la main les postes ouverts d'un bâtiment public, entre 0 et sa capacité.</summary>
    public string SetJobs(string buildingId, int jobs)
    {
        if (World.Structures.GetValueOrDefault(buildingId) is not { Ownership: Ownership.Public } building)
            return "Seuls les postes d'un bâtiment public se règlent : le privé et les SCOP décident seuls.";

        var opened = System.Math.Clamp(jobs, 0, building.JobCapacity);
        simulation.UpdateBuilding(building with { Jobs = opened, Capacity = building.JobCapacity, JobsSetByPlayer = true });
        return $"{opened:N0} postes ouverts, fixés à la main.";
    }

    /// <summary>Les postes ouverts d'un bâtiment public suivent de nouveau seuls la règle de licenciement et d'embauche.</summary>
    public string AutomaticJobs(string buildingId)
    {
        if (World.Structures.GetValueOrDefault(buildingId) is not { Ownership: Ownership.Public } building)
            return "Seuls les postes d'un bâtiment public se règlent.";

        simulation.UpdateBuilding(building with { JobsSetByPlayer = false });
        return "Postes ouverts revenus à l'automatique.";
    }

    /// <summary>Change la recette d'un bâtiment polyvalent parmi celles de son type.</summary>
    public string SetRecipe(string buildingId, int recipeIndex)
    {
        if (!World.Structures.TryGetValue(buildingId, out var building)
            || building.TypeId is not { } typeId
            || !World.BuildingTypes.TryGetValue(typeId, out var type)
            || recipeIndex < 0 || recipeIndex >= type.AvailableRecipes.Count)
            return "Recette inconnue.";

        var recipe = type.AvailableRecipes[recipeIndex];
        simulation.UpdateBuilding(building with { Production = recipe });
        return $"Recette : {recipe.Label}.";
    }

    /// <summary>Change le combustible du chauffage d'un bâtiment de la liste 1.</summary>
    public string SetHeating(string buildingId, int fuelIndex)
    {
        if (!World.Structures.TryGetValue(buildingId, out var building) || fuelIndex < 0 || fuelIndex >= Energy.HeatingFuels.Count)
            return "Combustible inconnu.";

        var fuel = Energy.HeatingFuels[fuelIndex];
        simulation.UpdateBuilding(building with { HeatingProductId = fuel });
        return $"Chauffage au {fuel}.";
    }

    /// <summary>
    /// Les types de bâtiments que le joueur peut construire : ceux qui ont un coût de construction, sauf le camp de base
    /// (il fonde une commune) et la préfecture (elle se fonde dans le menu Structure).
    /// </summary>
    public IReadOnlyList<BuildableType> BuildableTypes() => World.BuildingTypes.Values
        .Where(t => t.CanBeBuilt && t.Id is not (Simulation.BaseCampTypeId or Simulation.PrefectureTypeId))
        .Select(t => new BuildableType(
            t.Id,
            t.Name,
            t.Category,
            t.Level,
            t.Tier,
            t.Description,
            t.Construction!.ProductId,
            ProductName(t.Construction.ProductId),
            t.Construction.Quantity,
            t.Construction.Days,
            t.Jobs,
            t.HousingPlaces,
            t.SchoolPlaces,
            t.StorageCapacity,
            t.AvailableRecipes.Select(r => r.Label).ToList(),
            t.IsFunctional))
        .ToList();

    /// <summary>
    /// Ce qui peut payer un matériau dans une commune, dans l'ordre où le chantier le prend : sa réserve de chantier, le
    /// stock de l'État, puis ce qui est en vente, acheté avec le trésor.
    /// </summary>
    public MaterialAvailability AvailabilityOf(string communeId, string productId)
    {
        var market = World.MarketOf(communeId);
        var stateSeller = Market.StateSellerId(communeId);
        return new MaterialAvailability(
            World.ConstructionReserveOf(communeId).QuantityOf(productId),
            market.QuantityOf(productId, stateSeller),
            market.QuantityOfOthers(productId, stateSeller),
            market.PriceOf(productId),
            World.Treasury.Balance);
    }

    /// <summary>
    /// Lance le chantier d'un bâtiment public dans une commune. Son coût est payé tout de suite (réserve de chantier,
    /// stock de l'État, puis achat avec le trésor) ; le bâtiment arrive à la fin du chantier.
    /// </summary>
    public string Build(string typeId, string communeId)
    {
        if (!World.BuildingTypes.TryGetValue(typeId, out var type) || type.Construction is not { } cost
            || BuildableTypes().All(t => t.Id != typeId))
            return "Ce type de bâtiment ne se construit pas ici (le camp de base fonde une commune, la préfecture se fonde dans Structure).";

        var number = 1;
        while (World.IsIdTaken($"{typeId}-{number}"))
            number++;
        var building = type.Build($"{typeId}-{number}", communeId);
        if (simulation.TryBuild(building, PaymentMode.StockFirst))
            return $"Chantier lancé : {building.Name}, {cost.Days} jours. Les matériaux sont payés.";

        var available = AvailabilityOf(communeId, cost.ProductId);
        return $"Construction impossible : il faut {cost.Quantity:N0} {ProductName(cost.ProductId)}. Il y a {available.Reserve:N0} en réserve "
            + $"de chantier, {available.StateStock:N0} dans le stock de l'État et {available.OnSale:N0} en vente, à acheter avec le trésor.";
    }

    /// <summary>Le nom lisible d'un produit (« matériaux de construction »), ou son identifiant s'il est inconnu.</summary>
    public string ProductName(string productId) =>
        World.Products.FirstOrDefault(p => p.Id == productId)?.Name ?? productId;

    /// <summary>
    /// Fonde une préfecture qui regroupe les entités choisies, dans l'État ou une préfecture ; son bâtiment est payé par
    /// le chef-lieu (sa réserve de chantier, le stock de l'État, puis le marché), la légitimité dans le pôle choisi.
    /// </summary>
    public string FoundPrefecture(string name, string parentId, IReadOnlyList<string> memberIds, string seatCommuneId, Orientation pole)
    {
        var settings = Balance.Current;
        if (string.IsNullOrWhiteSpace(name))
            return "Donnez un nom à la préfecture.";
        if (memberIds.Count < settings.PrefectureMinimumMembers)
            return $"Une préfecture regroupe au moins {settings.PrefectureMinimumMembers} entités.";
        if (World.Legitimacy[pole] < settings.PrefectureLegitimacyCost)
            return $"Pas assez de légitimité {pole.Code} (il en faut {settings.PrefectureLegitimacyCost:0.#}).";

        return simulation.TryFoundPrefecture(name.Trim(), parentId, memberIds, seatCommuneId, PaymentMode.StockFirst, pole) is { } prefecture
            ? $"{prefecture.Name} fondée : son bâtiment est en chantier dans le chef-lieu."
            : "Fondation impossible : le chef-lieu doit être une commune regroupée, aucune entité ne peut regrouper ce qui la "
              + "contient, et le chef-lieu doit pouvoir payer le chantier (réserve, stock de l'État ou marché).";
    }

    /// <summary>Rattache une commune ou une préfecture à l'État ou à une préfecture, contre de la légitimité.</summary>
    public string Attach(string entityId, string newParentId, Orientation pole)
    {
        if (!simulation.CanAttach(entityId, newParentId))
            return "Rattachement impossible : déjà là, boucle, ou chef-lieu qui quitterait sa préfecture.";
        return simulation.TryAttach(entityId, newParentId, pole)
            ? "Rattachement fait."
            : $"Pas assez de légitimité {pole.Code} (il en faut {Balance.Current.AttachLegitimacyCost:0.#}).";
    }

    private NationalizationOffer OfferOf(Structure building, NationalizationMode mode) =>
        new(simulation.NationalizationPriceOf(building, mode), simulation.NationalizationLegitimacyCostOf(building, mode));

    private string CommuneName(string? communeId) =>
        communeId is not null && World.Structures.TryGetValue(communeId, out var commune) ? commune.Name : "—";

    /// <summary>Les informations en direct d'un bâtiment : personnel, production, coûts, caisse, difficultés.</summary>
    private List<string> InfoOf(Structure building)
    {
        var world = World;
        var workers = world.Workers.GetValueOrDefault(building.Id);
        var staff = world.Classes.Sum(c => c.MembersWorkingIn(building.Id));
        var lines = new List<string>
        {
            $"Commune : {CommuneName(world.CommuneOf(building))}",
            $"Type : {(building.TypeId is { } typeId && world.BuildingTypes.TryGetValue(typeId, out var type) ? $"{type.Name} ({type.Category}, niveau {type.Level})" : "—")}",
            $"Propriétaire : {OwnerName(building)}",
            $"Personnel : {staff:N0} dont {workers:N0} aux emplois principaux"
                + (building.MainRank == Rank.Intermediate ? " (professions intermédiaires)" : ""),
        };

        if (building.JobCapacity > 0)
            lines.Add($"Postes ouverts : {building.Jobs:N0} sur {building.JobCapacity:N0}"
                + (building.JobsSetByPlayer ? " (fixés à la main)" : building.Ownership == Ownership.Public ? " (automatique)" : ""));
        if (building.WageCut > 0)
            lines.Add($"Salaires baissés de {building.WageCut:P0} (SCOP en difficulté)");

        if (building.Production is { } production)
        {
            var perDay = production.QuantityPerWorker * workers;
            lines.Add($"Production : {perDay:N1} {production.ProductId} par jour, valeur {perDay * world.PriceOf(production.ProductId):N0}");
            if (production.Materials.Count > 0)
                lines.Add("Matières par jour : " + string.Join(", ", production.Materials.Select(m => $"{m.ProductId} {perDay * m.Quantity:N1}")));
        }

        var wages = world.Classes.Sum(c => c.MembersWorkingIn(building.Id) * Staffing.WageOf(building, c.Rank));
        if (wages > 0)
            lines.Add($"Salaires dus : {wages:N0} par jour");
        if (building.HousingPlaces > 0)
            lines.Add($"Loyers possibles : {building.HousingPlaces * building.DailyRent:N0} par jour ({building.HousingPlaces:N0} places)");
        if (building.SchoolPlaces > 0)
            lines.Add(
                $"Enseignement {(building.SchoolLevel == EducationLevel.Basic ? "de base" : "avancé")} : " +
                $"jusqu'à {building.SchoolPlaces * building.PointsPerPupil:N0} points par jour, frais possibles {building.SchoolPlaces * building.DailyTuition:N0}");

        if (building.StorageCapacity > 0)
            lines.Add(DescribeStorage(building));

        if (world.TierOf(building) == 1 && Energy.UnitsOf(building) > 0)
            lines.Add($"Énergie (chauffage au {building.HeatingProductId ?? Energy.DefaultHeating}, eau) : {world.EnergyShareOf(building):P0} des besoins couverts");

        if (world.Wages.PaidShare(building.Id) is { } paidShare)
            lines.Add($"Salaires versés sur 30 jours : {paidShare:P0}{(simulation.IsStruggling(building) ? " — EN DIFFICULTÉ" : "")}");
        lines.Add($"Bénéfice attendu : {simulation.DailyProfitOf(building):N0} par jour");
        if (Companies.HasCompanyFund(building))
            lines.Add($"Caisse de l'entreprise : {world.CompanyFundOf(building).Balance:N0} (réserve gardée : {Companies.ReserveOf(building, world.PriceOf):N0}, le reste va aux propriétaires)");

        lines.Add(building.Ownership == Ownership.Public
            ? "Payé par la caisse de la commune, puis le trésor"
            : "Payé par la caisse de l'entreprise");
        return lines;
    }

    private string OwnerName(Structure building) => building.Ownership switch
    {
        Ownership.Public => "l'État",
        _ when building.OwnerClassId is { } classId && World.ClassesById.TryGetValue(classId, out var owner)
            => $"{owner.Name}{(building.Ownership == Ownership.Cooperative ? " (coopérative)" : " (privé)")}",
        _ => "personne",
    };

    private string DescribeStorage(Structure building)
    {
        var accepts = building.StorageKind switch
        {
            StorageKind.Food => "l'alimentaire",
            StorageKind.NonFood => "le non alimentaire",
            StorageKind.Specific => building.StorageProductId ?? "aucun produit choisi",
            _ => "tous les produits",
        };
        var contents = World.CommuneOf(building) is { } communeId ? World.StorageOf(communeId).In(building.Id) : new Dictionary<string, double>();
        var detail = contents.Where(c => c.Value >= 0.5).Select(c => $"{c.Key} {c.Value:N0}");
        return $"Stockage ({accepts}) : {contents.Values.Sum():N0} / {building.StorageCapacity:N0}"
            + (detail.Any() ? $"  [{string.Join(", ", detail)}]" : "");
    }
}

/// <summary>Fiche d'un bâtiment à un instant donné : ses informations et ce que le joueur peut y décider.</summary>
/// <summary>
/// Un type de bâtiment constructible : son coût (un matériau, sa quantité, la durée du chantier), ce qu'il apporte
/// (emplois, places, stockage, recettes) et s'il a déjà un effet dans le jeu.
/// </summary>
public sealed record BuildableType(
    string Id,
    string Name,
    string Category,
    int Level,
    int Tier,
    string Description,
    string MaterialId,
    string MaterialName,
    double MaterialQuantity,
    int Days,
    int Jobs,
    int HousingPlaces,
    int SchoolPlaces,
    int StorageCapacity,
    IReadOnlyList<string> Recipes,
    bool IsFunctional);

/// <summary>Ce qui peut payer un matériau dans une commune : réserve de chantier, stock de l'État, en vente (et son prix), trésor.</summary>
public sealed record MaterialAvailability(double Reserve, double StateStock, double OnSale, double Price, double Treasury);

public sealed record BuildingSheet(
    string Id,
    string Name,
    IReadOnlyList<string> Info,
    bool IsPublic,
    int Jobs,
    int JobCapacity,
    bool JobsSetByPlayer,
    IReadOnlyList<string> Recipes,
    int RecipeIndex,
    IReadOnlyList<string> HeatingFuels,
    int HeatingIndex,
    IReadOnlyList<BuyerEntry> Buyers,
    double MaximumSalePrice,
    double SaleLegitimacyAvailable,
    NationalizationOffer? Buyout,
    NationalizationOffer? Expropriation,
    double NationalizationLegitimacyAvailable,
    bool IsStruggling);

public sealed record BuyerEntry(string ClassId, string Label);

public sealed record NationalizationOffer(double Price, double LegitimacyCost);
