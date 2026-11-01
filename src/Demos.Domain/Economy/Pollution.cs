using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// La pollution vient de l'activité des bâtiments : chacun en produit selon ses travailleurs de production.
/// Elle pèse pleinement sur la commune où se trouve le bâtiment, et peu sur les autres communes
/// (en attendant la carte, « loin » veut dire « dans une autre commune »).
/// </summary>
public static class Pollution
{
    /// <summary>Proposition : une commune reçoit 10 % de la pollution produite dans les autres communes.</summary>
    public const double DistantShare = 0.1;

    /// <summary>Proposition : une commune est polluée au-delà de 0,1 point de pollution par habitant et par jour.</summary>
    public const double PollutedThreshold = 0.1;

    /// <summary>Pollution de chaque commune par habitant et par jour.</summary>
    public static Dictionary<string, double> PerResidentByCommune(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, double> productionWorkers,
        IReadOnlyList<SocialClass> classes)
    {
        var communes = Communes.All(structures).ToList();
        var produced = communes.ToDictionary(c => c.Id, _ => 0.0);

        foreach (var structure in structures.Values.Where(s => s.PollutionPerWorker > 0))
        {
            if (Communes.CommuneOf(structure, structures) is { } communeId && produced.ContainsKey(communeId))
                produced[communeId] += structure.PollutionPerWorker * productionWorkers.GetValueOrDefault(structure.Id);
        }

        var total = produced.Values.Sum();
        var result = new Dictionary<string, double>();
        foreach (var commune in communes)
        {
            var residents = classes.Sum(c => c.MembersLivingIn(commune.Id));
            var received = produced[commune.Id] + DistantShare * (total - produced[commune.Id]);
            result[commune.Id] = residents > 0 ? received / residents : 0;
        }

        return result;
    }
}
