using Demos.Domain.Politics;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Point d'entrée du moteur : il appelle AdvanceDay au rythme choisi par le joueur.
/// La simulation calcule l'économie chaque jour, les variables lentes chaque saison
/// et les bilans chaque année.
/// </summary>
public sealed class Simulation(World world, GameClock clock)
{
    public World World { get; } = world;
    public GameClock Clock { get; } = clock;

    public event Action? SeasonPassed;
    public event Action? YearPassed;

    public void AdvanceDay()
    {
        var change = Clock.AdvanceDay();

        RunDailyCalculations();

        if (change.HasFlag(CalendarChange.NewSeason))
            RunSeasonalCalculations();

        if (change.HasFlag(CalendarChange.NewYear))
            YearPassed?.Invoke();
    }

    // Ordre prévu par le document de règles : décisions, production, consommation,
    // fiscalité, légitimité, événements. Seule la légitimité existe pour l'instant.
    private void RunDailyCalculations() =>
        LawEffects.ApplyDaily(World.Classes, World.Laws, World.Structures, World.Legitimacy);

    // Prévu : climat, démographie, transferts de classe, revendications, recherche.
    // Seule la démographie existe pour l'instant.
    private void RunSeasonalCalculations()
    {
        foreach (var socialClass in World.Classes)
            socialClass.Grow();

        SeasonPassed?.Invoke();
    }
}
