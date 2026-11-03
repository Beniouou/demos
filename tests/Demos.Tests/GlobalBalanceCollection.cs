namespace Demos.Tests;

/// <summary>
/// Les tests qui changent les réglages d'équilibrage du jeu (<see cref="Demos.Domain.Economy.Balance.Current"/>, partagés
/// par tout le programme) tournent seuls, pour ne pas fausser les autres tests qui s'exécutent en même temps.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalBalanceCollection
{
    public const string Name = "Réglages d'équilibrage changés";
}
