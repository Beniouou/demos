using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// Chaque rang a ses postes : production pour les ouvriers, encadrement pour les cadres,
/// direction pour les dirigeants. Les postes d'encadrement et de direction suivent ceux de production.
/// </summary>
public static class Staffing
{
    /// <summary>Proposition : 1 cadre pour 10 postes de production.</summary>
    public const double ExecutivesPerProductionPost = 0.1;

    /// <summary>Proposition : 1 dirigeant pour 100 postes de production.</summary>
    public const double LeadersPerProductionPost = 0.01;

    public static double PostsOf(Structure structure, Rank rank) => rank switch
    {
        Rank.Workers => structure.Jobs,
        Rank.Executives => structure.Jobs * ExecutivesPerProductionPost,
        _ => structure.Jobs * LeadersPerProductionPost,
    };

    /// <summary>Le salaire d'un poste est le salaire de base du bâtiment × ce multiplicateur (proposition).</summary>
    public static double WageMultiplier(Rank rank) => rank switch
    {
        Rank.Workers => 1,
        Rank.Executives => 2,
        _ => 4,
    };
}
