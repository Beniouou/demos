using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Point d'entrée du moteur : il appelle AdvanceDay au rythme choisi par le joueur,
/// et la simulation lance le cycle mensuel à chaque changement de mois.
/// </summary>
public sealed class Simulation(World world, GameClock clock)
{
    public World World { get; } = world;
    public GameClock Clock { get; } = clock;

    public event Action? MonthPassed;

    public void AdvanceDay()
    {
        if (Clock.AdvanceDay())
            RunMonthlyCycle();
    }

    // Ordre prévu par le document de règles : production, consommation, fiscalité,
    // opinion, démographie, recherche, événements. Seules l'opinion et la démographie existent pour l'instant.
    private void RunMonthlyCycle()
    {
        foreach (var socialClass in World.Classes)
            socialClass.UpdateApproval(socialClass.Ideology.AgreementWith(World.RegimeIdeology));

        foreach (var socialClass in World.Classes)
            socialClass.Grow();

        MonthPassed?.Invoke();
    }
}
