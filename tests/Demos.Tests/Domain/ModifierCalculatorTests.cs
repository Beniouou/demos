using Demos.Domain.Shared;

namespace Demos.Tests.Domain;

public class ModifierCalculatorTests
{
    private static Modifier Add(double value) => new("test", "production", "national", ModifierOperation.Additive, value);
    private static Modifier Mult(double value) => new("test", "production", "national", ModifierOperation.Multiplicative, value);

    [Fact]
    public void Sans_modificateur_la_valeur_reste_la_meme()
    {
        Assert.Equal(10, ModifierCalculator.Apply(10, []));
    }

    [Fact]
    public void Les_additifs_passent_avant_les_multiplicatifs()
    {
        var result = ModifierCalculator.Apply(10, [Add(5), Mult(0.5)]);

        Assert.Equal(22.5, result, precision: 6);
    }

    [Fact]
    public void Cinq_bonus_de_20_pourcent_donnent_100_pourcent()
    {
        var result = ModifierCalculator.Apply(100, Enumerable.Repeat(Mult(0.2), 5));

        Assert.Equal(200, result, precision: 6);
    }
}
