using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

public static class Employment
{
    /// <summary>
    /// Pourvoit les postes de chaque commune avec ses habitants : chaque rang occupe les postes de son rang,
    /// au prorata de ses classes. Renvoie le nombre de travailleurs de production de chaque structure.
    /// Une commune sans habitants n'a que des ruines.
    /// </summary>
    public static Dictionary<string, double> Assign(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<SocialClass> classes)
    {
        var productionWorkers = new Dictionary<string, double>();
        foreach (var socialClass in classes)
            socialClass.ClearEmployment();

        foreach (var commune in Communes.All(structures))
        {
            var workplaces = structures.Values
                .Where(s => s.Jobs > 0 && Communes.CommuneOf(s, structures) == commune.Id)
                .ToList();

            foreach (var rank in Enum.GetValues<Rank>())
            {
                var posts = workplaces.Sum(s => Staffing.PostsOf(s, rank));
                var candidates = classes
                    .Where(c => c.Rank == rank)
                    .ToDictionary(c => c, c => c.MembersLivingIn(commune.Id));
                var available = candidates.Values.Sum();
                if (posts <= 0 || available <= 0)
                    continue;

                var fillRate = Math.Min(1, available / posts);
                foreach (var workplace in workplaces)
                {
                    var filled = Staffing.PostsOf(workplace, rank) * fillRate;
                    if (rank == Rank.Workers)
                        productionWorkers[workplace.Id] = filled;

                    foreach (var (socialClass, members) in candidates)
                        socialClass.SetEmployment(workplace.Id, filled * members / available);
                }
            }
        }

        return productionWorkers;
    }
}
