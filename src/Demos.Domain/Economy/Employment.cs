using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

public static class Employment
{
    /// <summary>
    /// Pourvoit les postes de chaque commune avec ses habitants (sauf les dirigeants), au prorata de chaque classe.
    /// Renvoie le nombre de travailleurs de chaque structure. Une commune sans habitants n'a que des ruines.
    /// </summary>
    public static Dictionary<string, double> Assign(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<SocialClass> classes)
    {
        var workers = new Dictionary<string, double>();
        foreach (var socialClass in classes)
            socialClass.ClearEmployment();

        foreach (var commune in Communes.All(structures))
        {
            var workplaces = structures.Values
                .Where(s => s.Jobs > 0 && Communes.CommuneOf(s, structures) == commune.Id)
                .ToList();
            var jobs = workplaces.Sum(s => s.Jobs);

            var workforce = classes
                .Where(c => c.TakesJobs)
                .ToDictionary(c => c, c => c.MembersLivingIn(commune.Id));
            var available = workforce.Values.Sum();
            if (jobs == 0 || available <= 0)
                continue;

            var fillRate = Math.Min(1, available / jobs);
            foreach (var workplace in workplaces)
            {
                var filled = workplace.Jobs * fillRate;
                workers[workplace.Id] = filled;

                foreach (var (socialClass, members) in workforce)
                    socialClass.SetEmployment(workplace.Id, filled * members / available);
            }
        }

        return workers;
    }
}
