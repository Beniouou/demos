using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Chaque rang a ses emplois : production pour les ouvriers, encadrement pour les professions intermédiaires,
/// direction pour les cadres. Les emplois d'encadrement et de direction suivent ceux de production.
/// À ne pas confondre avec les postes, occupés par des personnages, qui apportent seulement des bonus.
/// </summary>
public static class Staffing
{
    /// <summary>Proposition : 1 profession intermédiaire pour 10 emplois de production.</summary>
    public const double IntermediatesPerProductionJob = 0.1;

    /// <summary>Proposition : 1 cadre pour 100 emplois de production.</summary>
    public const double ExecutivesPerProductionJob = 0.01;

    public static double JobsOf(Structure structure, Rank rank) => rank switch
    {
        Rank.Workers => structure.Jobs,
        Rank.Intermediate => structure.Jobs * IntermediatesPerProductionJob,
        _ => structure.Jobs * ExecutivesPerProductionJob,
    };

    /// <summary>Le salaire d'un emploi est le salaire de base du bâtiment × ce multiplicateur (proposition).</summary>
    public static double WageMultiplier(Rank rank) => rank switch
    {
        Rank.Workers => 1,
        Rank.Intermediate => 2,
        _ => 4,
    };
}
