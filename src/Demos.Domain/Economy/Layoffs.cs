using Demos.Domain.Politics;

namespace Demos.Domain.Economy;

/// <summary>
/// Licenciements et embauches, au changement de mois. Une entreprise qui ne vend pas (invendus au-delà de 30 jours de
/// production) ou ne paie plus ses salaires (moins de 80 % sur 30 jours) ferme des postes ; une entreprise qui vend tout
/// (invendus sous 10 jours) et paie tous ses salaires en rouvre, sans dépasser sa capacité. La vitesse dépend du
/// statut : l'État 2 % de sa capacité par mois, une SCOP 5 %, le privé 15 % (réglages). Une SCOP baisse d'abord ses
/// salaires, jusqu'à −20 %, avant de licencier, et les remonte avant de réembaucher. Un bâtiment dont le joueur a fixé
/// les postes ne bouge pas, ni un bâtiment qui garde toujours ses postes (le camp de base). Une entreprise privée ou coopérative ne rouvre que les postes que sa caisse peut payer
/// pendant 30 jours (réglage) : sans argent, elle est en faillite et reste fermée tant que personne n'y
/// remet de l'argent (l'État peut la racheter).
/// </summary>
public static class Layoffs
{
    /// <summary>Part des salaires payés en dessous de laquelle une entreprise est en difficulté.</summary>
    public const double StrugglingPaidShare = WageLedger.StrugglingThreshold;

    /// <summary>Part des salaires payés à partir de laquelle une entreprise peut réembaucher (tous payés).</summary>
    public const double HealthyPaidShare = 0.999;

    /// <summary>
    /// Renvoie les bâtiments qui changent ce mois-ci (postes ouverts ou baisse des salaires). <paramref name="keepsJobs"/>
    /// désigne les bâtiments qui ne licencient jamais, même quand leur production ne se vend pas.
    /// </summary>
    public static List<Structure> RunMonthly(IReadOnlyDictionary<string, Structure> structures, Func<string, Market> marketOf, WageLedger wages,
        Func<Structure, Treasury>? companyFundOf = null, Func<Structure, bool>? keepsJobs = null)
    {
        var balance = Balance.Current;
        var changed = new List<Structure>();
        var workplaces = structures.Values.Where(s => s.Kind == StructureKind.Building && s.JobCapacity > 0).ToList();
        foreach (var building in workplaces.Where(b => !b.JobsSetByPlayer && keepsJobs?.Invoke(b) != true))
        {
            var unsoldDays = UnsoldDaysOf(building, structures, workplaces, marketOf);
            // Sans poste ouvert, rien n'est dû : le bâtiment peut rouvrir des postes quand ses invendus sont écoulés.
            var paid = building.Jobs == 0 ? 1 : wages.PaidShare(building.Id) ?? 1;
            var struggling = unsoldDays > balance.LayoffUnsoldDays || paid < StrugglingPaidShare;
            var healthy = (unsoldDays ?? 0) < balance.HireUnsoldDays && paid >= HealthyPaidShare;
            var capacity = building.JobCapacity;
            var step = Math.Max(1, (int)Math.Round(capacity * balance.JobChangePerMonth.GetValueOrDefault(building.Ownership)));
            var cooperative = building.Ownership == Ownership.Cooperative;

            var updated = building;
            if (struggling)
            {
                updated = cooperative && building.WageCut < balance.CooperativeWageCutMax - 1e-9
                    ? building with { WageCut = Math.Min(balance.CooperativeWageCutMax, building.WageCut + balance.CooperativeWageCutStep) }
                    : building with { Jobs = Math.Max(0, building.Jobs - step), Capacity = capacity };
            }
            else if (healthy)
            {
                if (cooperative && building.WageCut > 0)
                    updated = building with { WageCut = Math.Max(0, building.WageCut - balance.CooperativeWageCutStep) };
                else if (building.Jobs < capacity && Math.Min(step, AffordableJobs(building, companyFundOf)) is var hired && hired > 0)
                    updated = building with { Jobs = Math.Min(capacity, building.Jobs + hired), Capacity = capacity };
            }

            if (updated == building)
                continue;

            // Un bâtiment qui ferme tous ses postes repart d'un relevé de salaires vierge quand il rouvre.
            if (updated.Jobs == 0)
                wages.Forget(building.Id);
            changed.Add(updated);
        }

        return changed;
    }

    /// <summary>
    /// Postes qu'une entreprise peut rouvrir : ceux dont sa caisse paie les salaires pendant 30 jours (réglage). Sans
    /// limite pour un bâtiment public, que paient la commune et l'État.
    /// </summary>
    public static int AffordableJobs(Structure building, Func<Structure, Treasury>? companyFundOf)
    {
        if (building.Ownership == Ownership.Public || companyFundOf is null)
            return int.MaxValue;

        var perJob = Staffing.WageBillPerJob(building) * Balance.Current.StartingReserveDays;
        return perJob <= 0 ? int.MaxValue : (int)Math.Floor(companyFundOf(building).Balance / perJob);
    }

    /// <summary>
    /// Invendus d'un bâtiment, en jours de sa production à ses postes ouverts : ce que son entreprise a dans le stockage de
    /// la commune, ou pour un bâtiment public ce que l'État y a de ce produit, rapporté à la production de tous ses
    /// bâtiments publics qui le fabriquent. Null pour un bâtiment qui ne produit rien.
    /// </summary>
    public static double? UnsoldDaysOf(Structure building, IReadOnlyDictionary<string, Structure> structures, IReadOnlyList<Structure> workplaces,
        Func<string, Market> marketOf)
    {
        if (building.Production is not { } recipe || recipe.QuantityPerWorker <= 0)
            return null;

        var communeId = Communes.CommuneOf(building, structures) ?? "";
        var market = marketOf(communeId);
        double unsold, perDay;
        if (building.Ownership == Ownership.Public)
        {
            unsold = market.QuantityOf(recipe.ProductId, Market.StateSellerId(communeId));
            perDay = workplaces
                .Where(w => w.Ownership == Ownership.Public && w.Production?.ProductId == recipe.ProductId
                            && Communes.CommuneOf(w, structures) == communeId)
                .Sum(w => Math.Max(1, w.Jobs) * w.Production!.Value.QuantityPerWorker);
        }
        else
        {
            unsold = market.QuantityOf(recipe.ProductId, Market.CompanySellerId(building.Id));
            perDay = Math.Max(1, building.Jobs) * recipe.QuantityPerWorker;
        }

        return unsold / perDay;
    }
}
