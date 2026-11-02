using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// La pollution vient de l'activité des bâtiments : chacun en produit selon ses travailleurs de production.
/// Elle pèse pleinement sur la commune où se trouve le bâtiment, et peu sur les autres communes
/// (en attendant la carte, « loin » veut dire « dans une autre commune »). Une grande ville pollue ainsi les petites
/// communes autour, mais la pollution venue d'une commune ne dépasse jamais, par habitant, celle de la commune où
/// elle est produite.
/// </summary>
public static class Pollution
{
    /// <summary>Proposition : une commune reçoit 10 % de la pollution produite dans les autres communes.</summary>
    public static double DistantShare => Balance.Current.DistantPollutionShare;

    /// <summary>Proposition : une commune est polluée au-delà de 0,1 point de pollution par habitant et par jour.</summary>
    public static double PollutedThreshold => Balance.Current.PollutedThreshold;

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

        var residents = communes.ToDictionary(c => c.Id, c => classes.Sum(s => s.MembersLivingIn(c.Id)));
        var local = communes.ToDictionary(c => c.Id, c => residents[c.Id] > 0 ? produced[c.Id] / residents[c.Id] : 0);
        var result = new Dictionary<string, double>();
        foreach (var commune in communes)
        {
            if (residents[commune.Id] <= 0)
            {
                result[commune.Id] = 0;
                continue;
            }

            // Ce qui vient de chaque autre commune, plafonné à la pollution par habitant de cette commune.
            var distant = communes
                .Where(o => o.Id != commune.Id && produced[o.Id] > 0)
                .Sum(o =>
                {
                    var perResident = DistantShare * produced[o.Id] / residents[commune.Id];
                    return residents[o.Id] > 0 ? Math.Min(perResident, local[o.Id]) : perResident;
                });
            result[commune.Id] = local[commune.Id] + distant;
        }

        return result;
    }
}
