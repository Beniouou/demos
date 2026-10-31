using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class IdeologyTests
{
    [Theory]
    [InlineData(-50, -50, -50, "Communiste")]
    [InlineData(-50, -50, 50, "Libertaire")]
    [InlineData(50, -50, 50, "Capitaliste")]
    [InlineData(50, 50, 50, "Primitiviste")]
    public void Le_nom_depend_du_pole_de_chaque_axe(double hierarchy, double production, double property, string expected)
    {
        Assert.Equal(expected, new Ideology(hierarchy, production, property).Name);
    }

    [Fact]
    public void Les_valeurs_sont_bornees_a_100()
    {
        var ideology = new Ideology(250, -250, 0);

        Assert.Equal(100, ideology.Hierarchy);
        Assert.Equal(-100, ideology.Production);
    }

    [Fact]
    public void Deux_ideologies_identiques_sont_en_accord_total()
    {
        var ideology = new Ideology(10, 20, 30);

        Assert.Equal(1, ideology.AgreementWith(ideology), precision: 6);
    }

    [Fact]
    public void Deux_ideologies_opposees_sont_en_desaccord_total()
    {
        var a = new Ideology(-100, -100, -100);
        var b = new Ideology(100, 100, 100);

        Assert.Equal(0, a.AgreementWith(b), precision: 6);
    }
}
