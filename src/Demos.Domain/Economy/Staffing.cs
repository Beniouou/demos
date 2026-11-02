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
    /// <summary>Emplois d'encadrement pour un emploi principal (réglage d'équilibrage : 0,1).</summary>
    public static double IntermediatesPerProductionJob => Balance.Current.SupervisorsPerJob;

    /// <summary>Emplois de direction pour un emploi principal (réglage d'équilibrage : 0,01).</summary>
    public static double ExecutivesPerProductionJob => Balance.Current.DirectorsPerJob;

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
    public static double WageMultiplier(Rank rank) => Balance.Current.WageMultipliers.GetValueOrDefault(rank, 1);
}
