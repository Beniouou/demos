using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Population;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Les chiffres de population d'une ou plusieurs communes, tirés de la vue : habitants, logements, emplois, chômeurs,
/// membres par rang et par pôle, et les moyennes (pondérées par les membres) des besoins couverts, des logés et de
/// l'adhésion. Sert à la vue d'ensemble et à l'évolution du menu Population ; aucun calcul du jeu ici.
/// </summary>
public sealed record PopulationStats(
    double Residents,
    int HousingPlaces,
    double JobsFilled,
    int Jobs,
    double Unemployed,
    long Members,
    IReadOnlyDictionary<Rank, long> ByRank,
    IReadOnlyDictionary<Orientation, long> ByPole,
    double Needs,
    double Housed,
    double Adhesion,
    double Wealth,
    int Groups)
{
    public double WealthPerMember => Members > 0 ? Wealth / Members : 0;

    /// <summary>La part des membres à ce pôle (sur son axe, les deux pôles font 100 %).</summary>
    public double Share(Orientation pole) => Members > 0 ? (double)ByPole.GetValueOrDefault(pole) / Members : 0;

    public static PopulationStats Of(IEnumerable<CommuneView> communes)
    {
        var list = communes.ToList();
        var groups = list.SelectMany(c => c.Classes).ToList();
        long members = groups.Sum(g => g.Population);
        double Average(System.Func<SocialClassView, double> value) =>
            members > 0 ? groups.Sum(g => value(g) * g.Population) / members : 0;

        var byRank = System.Enum.GetValues<Rank>().ToDictionary(rank => rank, rank => groups.Where(g => g.Rank == rank).Sum(g => g.Population));
        var byPole = Orientation.All.ToDictionary(pole => pole,
            pole => groups.Where(g => g.Poles?.Contains(pole) == true).Sum(g => g.Population));

        return new PopulationStats(
            list.Sum(c => c.Residents),
            list.Sum(c => c.HousingPlaces),
            list.Sum(c => c.JobsFilled),
            list.Sum(c => c.Jobs),
            groups.Sum(g => g.Unemployed),
            members,
            byRank,
            byPole,
            Average(g => g.NeedsSatisfaction),
            Average(g => g.HousingCoverage),
            Average(g => g.Adhesion),
            groups.Sum(g => g.Wealth),
            groups.Count);
    }
}
