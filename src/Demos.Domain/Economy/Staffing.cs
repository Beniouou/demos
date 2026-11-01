using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Chaque rang a ses emplois : production pour les ouvriers, encadrement pour les cadres,
/// direction pour les dirigeants. Les emplois d'encadrement et de direction suivent ceux de production.
/// À ne pas confondre avec les postes, occupés par des personnages, qui apportent seulement des bonus.
/// </summary>
public static class Staffing
{
    /// <summary>Proposition : 1 cadre pour 10 emplois de production.</summary>
    public const double ExecutivesPerProductionJob = 0.1;

    /// <summary>Proposition : 1 dirigeant pour 100 emplois de production.</summary>
    public const double LeadersPerProductionJob = 0.01;

    public static double JobsOf(Structure structure, Rank rank) => rank switch
    {
        Rank.Workers => structure.Jobs,
        Rank.Executives => structure.Jobs * ExecutivesPerProductionJob,
        _ => structure.Jobs * LeadersPerProductionJob,
    };

    /// <summary>Le salaire d'un emploi est le salaire de base du bâtiment × ce multiplicateur (proposition).</summary>
    public static double WageMultiplier(Rank rank) => rank switch
    {
        Rank.Workers => 1,
        Rank.Executives => 2,
        _ => 4,
    };
}
