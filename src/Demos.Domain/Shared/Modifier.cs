namespace Demos.Domain.Shared;

public enum ModifierOperation
{
    Additive,
    Multiplicative,
}

/// <summary>
/// Changement d'une valeur du jeu, produit par une source (loi, poste, structure…).
/// </summary>
/// <param name="Source">Qui le produit, par exemple « loi:journee-8h ».</param>
/// <param name="Target">Quelle valeur il change, par exemple « production ».</param>
/// <param name="Scope">Où il s'applique, par exemple « national » ou « region:nord ».</param>
/// <param name="Operation">Additif ou multiplicatif.</param>
/// <param name="Value">De combien : 5 en additif, -0.10 pour -10 % en multiplicatif.</param>
public sealed record Modifier(
    string Source,
    string Target,
    string Scope,
    ModifierOperation Operation,
    double Value);
