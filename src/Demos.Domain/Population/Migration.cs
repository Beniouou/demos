using Demos.Domain.Politics;

namespace Demos.Domain.Population;

/// <summary>
/// Règle de départ, en attendant les lois et décisions du joueur qui modifieront les migrations :
/// chaque saison, une part des sans-emploi part vers la commune qui offre le plus de postes libres.
/// </summary>
public static class Migration
{
    public const double SeasonalShareOfUnemployed = 0.05;

    public static void RunSeasonal(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<SocialClass> classes,
        IReadOnlyDictionary<string, double> workers)
    {
        var communes = Communes.All(structures).ToList();
        if (communes.Count < 2)
            return;

        var workplacesByCommune = communes.ToDictionary(
            commune => commune.Id,
            commune => structures.Values.Where(s => s.Jobs > 0 && Communes.CommuneOf(s, structures) == commune.Id).ToList());

        double FreeJobs(string communeId) =>
            workplacesByCommune[communeId].Sum(s => s.Jobs - workers.GetValueOrDefault(s.Id));

        var target = communes.MaxBy(commune => FreeJobs(commune.Id))!.Id;
        var freeJobs = FreeJobs(target);
        if (freeJobs <= 0)
            return;

        var movers = new List<(SocialClass Class, string From, double Members)>();
        foreach (var socialClass in classes.Where(c => c.TakesJobs && c.Population > 0))
        {
            foreach (var commune in communes.Where(c => c.Id != target))
            {
                var employed = workplacesByCommune[commune.Id].Sum(s => socialClass.MembersWorkingIn(s.Id));
                var unemployed = Math.Max(0, socialClass.MembersLivingIn(commune.Id) - employed);
                if (unemployed > 0)
                    movers.Add((socialClass, commune.Id, unemployed * SeasonalShareOfUnemployed));
            }
        }

        var total = movers.Sum(m => m.Members);
        if (total <= 0)
            return;

        var scale = Math.Min(1, freeJobs / total);
        foreach (var (socialClass, from, members) in movers)
            socialClass.MoveResidents(from, target, members * scale / socialClass.Population);
    }
}
