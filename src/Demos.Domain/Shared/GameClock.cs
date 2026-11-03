namespace Demos.Domain.Shared;

/// <summary>Saisons météorologiques : l'hiver commence le 1er décembre, le printemps le 1er mars…</summary>
public enum Season
{
    Winter,
    Spring,
    Summer,
    Autumn,
}

/// <summary>Ce qui change dans le calendrier quand on avance d'un jour.</summary>
[Flags]
public enum CalendarChange
{
    None = 0,
    NewSeason = 1,
    NewYear = 2,
    NewMonth = 4,
}

/// <summary>
/// Temps de jeu. Il avance jour par jour ; la vitesse et la pause sont gérées par le moteur.
/// </summary>
public sealed class GameClock(DateOnly start)
{
    public DateOnly Today { get; private set; } = start;

    public Season Season => SeasonOf(Today);

    public static Season SeasonOf(DateOnly date) => date.Month switch
    {
        12 or 1 or 2 => Season.Winter,
        3 or 4 or 5 => Season.Spring,
        6 or 7 or 8 => Season.Summer,
        _ => Season.Autumn,
    };

    /// <summary>Avance d'un jour et indique si un nouveau mois, une nouvelle saison ou une nouvelle année commence.</summary>
    public CalendarChange AdvanceDay()
    {
        var previous = Today;
        Today = Today.AddDays(1);

        var change = CalendarChange.None;
        if (Today.Month != previous.Month)
            change |= CalendarChange.NewMonth;
        if (SeasonOf(Today) != SeasonOf(previous))
            change |= CalendarChange.NewSeason;
        if (Today.Year != previous.Year)
            change |= CalendarChange.NewYear;
        return change;
    }
}
