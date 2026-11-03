using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Port d'entrée en lecture seule : ce que l'interface du joueur peut voir de la partie. Chaque valeur est une copie
/// prise au moment de l'appel (nombres, textes, records) : l'interface ne reçoit aucun objet du moteur, elle ne peut
/// donc rien y changer. Lire ne crée rien non plus (ni caisse, ni marché). Les actions du joueur passent par
/// <see cref="Simulation"/>.
/// </summary>
public sealed class GameView(Simulation simulation)
{
    private World World => simulation.World;

    public DateOnly Today => simulation.Clock.Today;
    public Season Season => simulation.Clock.Season;

    public double TreasuryBalance => World.Treasury.Balance;
    public double StateTaxRate => World.StateTaxRate;

    public double LegitimacyOf(Orientation pole) => World.Legitimacy[pole];

    public IReadOnlyList<CommuneView> Communes() => World.Communes.Select(DescribeCommune).ToList();

    private CommuneView DescribeCommune(Structure commune)
    {
        var jobs = World.Structures.Values
            .Where(s => s.Jobs > 0 && World.CommuneOf(s) == commune.Id)
            .ToList();
        var storage = World.StorageOf(commune.Id);

        return new CommuneView(
            commune.Id,
            commune.Name,
            World.Residents(commune.Id),
            World.HousingPlaces(commune.Id),
            jobs.Sum(s => World.Workers.GetValueOrDefault(s.Id)),
            jobs.Sum(s => s.Jobs),
            World.PollutionByCommune.GetValueOrDefault(commune.Id),
            World.IsGhostTown(commune.Id),
            World.CommuneFundBalanceOf(commune.Id),
            World.CommuneTaxRateOf(commune.Id),
            storage.Stored.Values.Sum(),
            World.StorageCapacity(commune.Id),
            new Dictionary<string, double>(World.StockOf(commune.Id)),
            new Dictionary<string, double>(World.StateHoldingsOf(commune.Id)),
            World.Classes.Where(c => c.CommuneId == commune.Id && c.Population > 0).Select(c => new SocialClassView(
                c.Id, c.Name, c.Population, c.Wealth, c.NeedsSatisfaction, c.HousingCoverage, c.Adhesion,
                c.EducationPoints, c.AdvancedEducationPoints)).ToList());
    }
}

/// <summary>Une commune vue par le joueur, à un instant donné.</summary>
public sealed record CommuneView(
    string Id,
    string Name,
    double Residents,
    int HousingPlaces,
    double JobsFilled,
    int Jobs,
    double PollutionPerResident,
    bool IsGhostTown,
    double Fund,
    double TaxRate,
    double Stored,
    int StorageCapacity,
    IReadOnlyDictionary<string, double> Stock,
    IReadOnlyDictionary<string, double> StateStock,
    IReadOnlyList<SocialClassView> Classes);

/// <summary>Un groupe local (une classe dans sa commune) vu par le joueur, à un instant donné.</summary>
public sealed record SocialClassView(
    string Id,
    string Name,
    long Population,
    double Wealth,
    double NeedsSatisfaction,
    double HousingCoverage,
    double Adhesion,
    double EducationPoints,
    double AdvancedEducationPoints);
