using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Domain.Population;

/// <summary>
/// Règle de départ, en attendant les lois et décisions du joueur qui modifieront les migrations :
/// chaque saison, une part des sans-emploi de chaque rang part vers la commune qui offre le plus d'emplois libres de
/// ce rang. La charte de fondation d'une commune la rend plus attirante pour les classes de son pôle (ses emplois libres
/// comptent double) et moins pour celles du pôle opposé (ils comptent pour moitié). Les migrants quittent leur groupe local pour celui de même rang et de même
/// idéologie dans la commune d'arrivée (créé si besoin), en emportant leur richesse et leurs points d'éducation.
/// </summary>
public static class Migration
{
    public const double SeasonalShareOfUnemployed = 0.05;

    /// <summary>Propositions : une commune de son pôle compte double, une commune du pôle opposé pour moitié.</summary>
    public const double SamePoleAttraction = 2;

    public const double OppositePoleAttraction = 0.5;

    /// <summary>Attrait d'une commune pour une classe, d'après les chartes de fondation de la commune.</summary>
    public static double AttractionOf(string communeId, SocialClass socialClass, IReadOnlyList<Law> laws) => laws
        .Where(law => law.StructureId == communeId && law.Has(LawRule.FoundingCharter))
        .SelectMany(law => law.Orientations)
        .Aggregate(1.0, (attraction, pole) =>
            attraction * (socialClass.Ideology[pole.Axis] == pole.Pole ? SamePoleAttraction : OppositePoleAttraction));

    /// <summary>Calcul saisonnier. Renvoie les groupes locaux nés dans les communes d'arrivée.</summary>
    public static List<SocialClass> RunSeasonal(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<SocialClass> classes,
        IReadOnlyList<Law>? laws = null)
    {
        var finder = new ClassFinder(classes);
        var communes = Communes.All(structures).ToList();
        if (communes.Count < 2)
            return finder.Created;

        var workplacesByCommune = communes.ToDictionary(
            commune => commune.Id,
            commune => structures.Values.Where(s => s.Jobs > 0 && Communes.CommuneOf(s, structures) == commune.Id).ToList());

        foreach (var rank in Enum.GetValues<Rank>())
        {
            var rankClasses = classes.Where(c => c.Rank == rank && c.Population > 0).ToList();

            double FreeJobs(string communeId) => workplacesByCommune[communeId].Sum(s =>
                Staffing.JobsOf(s, rank) - rankClasses.Sum(c => c.MembersWorkingIn(s.Id)));

            var freeJobs = communes.ToDictionary(c => c.Id, c => FreeJobs(c.Id));
            if (freeJobs.Values.All(f => f <= 0))
                continue;

            // Chaque classe va vers la commune où ses emplois libres, pondérés par l'attrait, sont les plus nombreux.
            var movers = new List<(SocialClass Class, string From, string To, double Members)>();
            foreach (var socialClass in rankClasses)
            {
                var target = communes
                    .Where(c => freeJobs[c.Id] > 0)
                    .MaxBy(c => freeJobs[c.Id] * AttractionOf(c.Id, socialClass, laws ?? []))!.Id;
                foreach (var commune in communes.Where(c => c.Id != target))
                {
                    var employed = workplacesByCommune[commune.Id].Sum(s => socialClass.MembersWorkingIn(s.Id));
                    var unemployed = Math.Max(0, socialClass.MembersLivingIn(commune.Id) - employed);
                    if (unemployed > 0)
                        movers.Add((socialClass, commune.Id, target, unemployed * SeasonalShareOfUnemployed));
                }
            }

            // Une commune n'accueille pas plus de monde qu'elle n'a d'emplois libres.
            foreach (var group in movers.GroupBy(m => m.To))
            {
                var total = group.Sum(m => m.Members);
                if (total <= 0)
                    continue;

                var scale = Math.Min(1, freeJobs[group.Key] / total);
                foreach (var (socialClass, from, to, members) in group)
                {
                    var arrival = finder.Get(socialClass.Rank, socialClass.Ideology, to);
                    socialClass.TransferMembersTo(arrival, (long)(members * scale), from, to);
                }
            }
        }

        return finder.Created;
    }
}
