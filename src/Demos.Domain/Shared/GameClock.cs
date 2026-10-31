namespace Demos.Domain.Shared;

/// <summary>
/// Temps de jeu. Il avance jour par jour ; la vitesse et la pause sont gérées par le moteur.
/// </summary>
public sealed class GameClock(DateOnly start)
{
    public DateOnly Today { get; private set; } = start;

    /// <summary>Avance d'un jour. Renvoie true si un nouveau mois commence.</summary>
    public bool AdvanceDay()
    {
        var previousMonth = Today.Month;
        Today = Today.AddDays(1);
        return Today.Month != previousMonth;
    }
}
