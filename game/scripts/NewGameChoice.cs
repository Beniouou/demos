using Demos.Application;

namespace Demos.Game;

/// <summary>
/// Ce que le joueur a choisi avant la partie (décidé le 27 novembre 2026) : la carte, le départ d'exil (null pour le
/// départ riche du bac à sable ; avec ou sans Promesse) et les lois de départ (l'acte fondateur en mode avec Promesse).
/// </summary>
public sealed record NewGameChoice(string? MapName, ExileStart? Exile, StartingLaws? Laws)
{
    /// <summary>Vrai en mode avec La Promesse.</summary>
    public bool WithPromise => Exile?.Promise is not null;
}
