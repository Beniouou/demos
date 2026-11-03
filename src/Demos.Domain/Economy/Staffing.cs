using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Chaque bâtiment emploie les trois rangs, dans ses propres proportions et avec son propre salaire pour chacun. Ses
/// emplois productifs sont ceux de son rang productif (les ouvriers d'une ferme, les professions intermédiaires d'une
/// mairie) : sa production en dépend. Les autres rangs encadrent et dirigent, en nombre proportionnel aux emplois
/// productifs (par exemple 0,1 profession intermédiaire et 0,01 cadre par ouvrier dans une ferme).
/// Un bâtiment sans proportions ni salaires propres prend les réglages par défaut : 0,1 au rang au-dessus, 0,01 deux
/// rangs au-dessus, et un salaire de base × 1, 2 ou 4 selon le rang.
/// À ne pas confondre avec les postes, occupés par des personnages, qui apportent seulement des bonus.
/// </summary>
public static class Staffing
{
    /// <summary>Emplois d'encadrement par emploi productif, par défaut (réglage d'équilibrage : 0,1).</summary>
    public static double IntermediatesPerProductionJob => Balance.Current.SupervisorsPerJob;

    /// <summary>Emplois de direction par emploi productif, par défaut (réglage d'équilibrage : 0,01).</summary>
    public static double ExecutivesPerProductionJob => Balance.Current.DirectorsPerJob;

    public static double JobsOf(Structure structure, Rank rank) =>
        structure.Jobs * RatioOf(structure.MainRank, structure.StaffRatios, rank);

    /// <summary>Emplois d'un rang pour un emploi productif : 1 pour le rang productif, sinon la proportion du bâtiment.</summary>
    public static double RatioOf(Rank mainRank, IReadOnlyDictionary<Rank, double>? ratios, Rank rank) =>
        rank == mainRank ? 1
        : ratios is not null ? ratios.GetValueOrDefault(rank)
        : ShareOf(mainRank, rank);

    /// <summary>
    /// Proportions par défaut : 1 pour le rang productif, 0,1 pour le rang au-dessus (encadrement), 0,01 pour celui
    /// encore au-dessus (direction), rien en dessous.
    /// </summary>
    public static double ShareOf(Rank mainRank, Rank rank) => ((int)rank - (int)mainRank) switch
    {
        0 => 1,
        1 => IntermediatesPerProductionJob,
        2 => ExecutivesPerProductionJob,
        _ => 0,
    };

    /// <summary>Multiplicateur du salaire de base, pour un bâtiment sans salaires propres (1, 2 ou 4 selon le rang).</summary>
    public static double WageMultiplier(Rank rank) => Balance.Current.WageMultipliers.GetValueOrDefault(rank, 1);

    /// <summary>Salaire par jour d'un emploi de ce rang : celui du bâtiment, sinon le salaire de base × multiplicateur.</summary>
    public static double WageOf(double baseWage, IReadOnlyDictionary<Rank, double>? wages, Rank rank) =>
        wages is not null && wages.TryGetValue(rank, out var wage) ? wage : baseWage * WageMultiplier(rank);

    /// <summary>Salaire d'un rang dans un bâtiment, après la baisse des salaires d'une SCOP en difficulté.</summary>
    public static double WageOf(Structure structure, Rank rank) => WageOf(structure.DailyWage, structure.Wages, rank) * (1 - structure.WageCut);

    /// <summary>Salaires d'un emploi productif, avec l'encadrement et la direction qui l'accompagnent.</summary>
    public static double WageBillPerJob(Rank mainRank, IReadOnlyDictionary<Rank, double>? ratios, double baseWage,
        IReadOnlyDictionary<Rank, double>? wages) =>
        Enum.GetValues<Rank>().Sum(rank => RatioOf(mainRank, ratios, rank) * WageOf(baseWage, wages, rank));

    public static double WageBillPerJob(Structure structure) =>
        WageBillPerJob(structure.MainRank, structure.StaffRatios, structure.DailyWage, structure.Wages) * (1 - structure.WageCut);

    public static double WageBillPerJob(BuildingType type) =>
        WageBillPerJob(type.MainRank, type.StaffRatios, type.DailyWage, type.Wages);
}
