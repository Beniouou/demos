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
    // Les besoins (décidé le 21 novembre 2026) : pour chaque besoin et chaque rang (null = tous les rangs), la moyenne
    // des parts couvertes et des causes du manque, pondérée par les membres des groupes qui ont ce besoin.
    public IReadOnlyDictionary<(string Need, Rank? Rank), Demos.Domain.Population.NeedRecord> NeedCells { get; init; } =
        new Dictionary<(string, Rank?), Demos.Domain.Population.NeedRecord>();

    // Les chômeurs de chaque rang.
    public IReadOnlyDictionary<Rank, double> UnemployedByRank { get; init; } = new Dictionary<Rank, double>();

    // L'adhésion moyenne des membres de chaque pôle (pondérée par les membres).
    public IReadOnlyDictionary<Orientation, double> AdhesionByPole { get; init; } = new Dictionary<Orientation, double>();

    // La satisfaction moyenne des besoins de chaque rang.
    public IReadOnlyDictionary<Rank, double> NeedsByRank { get; init; } = new Dictionary<Rank, double>();

    /// <summary>La part de chômeurs d'un rang (null : tous les rangs).</summary>
    public double UnemploymentRate(Rank? rank) =>
        rank is { } r
            ? ByRank.GetValueOrDefault(r) is > 0 and var members ? UnemployedByRank.GetValueOrDefault(r) / members : 0
            : Members > 0 ? Unemployed / Members : 0;

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
            groups.Count)
        {
            NeedCells = NeedCellsOf(groups),
            UnemployedByRank = byRank.Keys.ToDictionary(rank => rank, rank => groups.Where(g => g.Rank == rank).Sum(g => g.Unemployed)),
            AdhesionByPole = Orientation.All.ToDictionary(pole => pole, pole =>
            {
                var atPole = groups.Where(g => g.Poles?.Contains(pole) == true).ToList();
                var count = atPole.Sum(g => (double)g.Population);
                return count > 0 ? atPole.Sum(g => g.Adhesion * g.Population) / count : double.NaN;
            }),
            NeedsByRank = byRank.Keys.ToDictionary(rank => rank, rank =>
            {
                var inRank = groups.Where(g => g.Rank == rank).ToList();
                var count = inRank.Sum(g => (double)g.Population);
                return count > 0 ? inRank.Sum(g => g.NeedsSatisfaction * g.Population) / count : double.NaN;
            }),
        };
    }

    private static Dictionary<(string, Rank?), Demos.Domain.Population.NeedRecord> NeedCellsOf(IReadOnlyList<SocialClassView> groups)
    {
        var cells = new Dictionary<(string, Rank?), Demos.Domain.Population.NeedRecord>();
        var needs = groups.SelectMany(g => g.NeedDetails?.Keys ?? []).Distinct().ToList();
        foreach (var need in needs)
        {
            foreach (var rank in System.Enum.GetValues<Rank>().Cast<Rank?>().Append(null))
            {
                var concerned = groups.Where(g => (rank is null || g.Rank == rank) && g.NeedDetails?.ContainsKey(need) == true && g.Population > 0).ToList();
                var weight = concerned.Sum(g => (double)g.Population);
                if (weight <= 0)
                    continue;
                double Mean(System.Func<Demos.Domain.Population.NeedRecord, double> value) =>
                    concerned.Sum(g => value(g.NeedDetails![need]) * g.Population) / weight;
                cells[(need, rank)] = new(Mean(r => r.Coverage), Mean(r => r.MoneyShortfall), Mean(r => r.SupplyShortage));
            }
        }
        return cells;
    }
}
