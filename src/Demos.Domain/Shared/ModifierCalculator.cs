namespace Demos.Domain.Shared;

public static class ModifierCalculator
{
    /// <summary>
    /// V_finale = (V_base + Σ additifs) × (1 + Σ multiplicatifs).
    /// Les multiplicatifs s'additionnent entre eux : cinq bonus de +20 % donnent +100 %.
    /// </summary>
    public static double Apply(double baseValue, IEnumerable<Modifier> modifiers)
    {
        double additive = 0;
        double multiplicative = 0;

        foreach (var modifier in modifiers)
        {
            if (modifier.Operation == ModifierOperation.Additive)
                additive += modifier.Value;
            else
                multiplicative += modifier.Value;
        }

        return (baseValue + additive) * (1 + multiplicative);
    }
}
