using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Chaque rang a ses emplois : production pour les ouvriers, encadrement pour les professions intermédiaires,
/// direction pour les cadres. Les emplois d'encadrement et de direction suivent ceux de production. Dans un bâtiment
/// de services (mairie, caserne…), les emplois principaux sont pour les professions intermédiaires, encadrées par des
/// cadres : le rang des emplois principaux décale l'encadrement d'un cran vers le haut.
/// À ne pas confondre avec les postes, occupés par des personnages, qui apportent seulement des bonus.
/// </summary>
public static class Staffing
{
    /// <summary>Proposition : 1 profession intermédiaire pour 10 emplois de production.</summary>
    public const double IntermediatesPerProductionJob = 0.1;

    /// <summary>Proposition : 1 cadre pour 100 emplois de production.</summary>
    public const double ExecutivesPerProductionJob = 0.01;

    public static double JobsOf(Structure structure, Rank rank) => structure.Jobs * ShareOf(structure.MainRank, rank);

    /// <summary>
    /// Emplois d'un rang pour un emploi principal : 1 pour le rang principal, 0,1 pour le rang au-dessus (encadrement),
    /// 0,01 pour celui encore au-dessus (direction), rien en dessous.
    /// </summary>
    public static double ShareOf(Rank mainRank, Rank rank) => ((int)rank - (int)mainRank) switch
    {
        0 => 1,
        1 => IntermediatesPerProductionJob,
        2 => ExecutivesPerProductionJob,
        _ => 0,
    };

    /// <summary>Le salaire d'un emploi est le salaire de base du bâtiment × ce multiplicateur (proposition).</summary>
    public static double WageMultiplier(Rank rank) => rank switch
    {
        Rank.Workers => 1,
        Rank.Intermediate => 2,
        _ => 4,
    };
}
