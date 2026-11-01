using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class IdeologyTests
{
    [Theory]
    [InlineData(Pole.A, Pole.A, Pole.A, "Communiste")]
    [InlineData(Pole.A, Pole.A, Pole.B, "Libertaire")]
    [InlineData(Pole.B, Pole.A, Pole.B, "Capitaliste")]
    [InlineData(Pole.B, Pole.B, Pole.B, "Primitiviste")]
    public void Le_nom_depend_du_pole_de_chaque_axe(Pole hierarchy, Pole production, Pole property, string expected)
    {
        Assert.Equal(expected, new Ideology(hierarchy, production, property).Name);
    }

    [Fact]
    public void Chaque_axe_garde_son_pole()
    {
        var ideology = new Ideology(Pole.B, Pole.A, Pole.B);

        Assert.Equal(Pole.B, ideology[IdeologicalAxis.Hierarchy]);
        Assert.Equal(Pole.A, ideology[IdeologicalAxis.Production]);
        Assert.Equal(Pole.B, ideology[IdeologicalAxis.Property]);
    }

    [Fact]
    public void L_accord_est_la_part_des_axes_en_commun()
    {
        var communist = new Ideology(Pole.A, Pole.A, Pole.A);
        var capitalist = new Ideology(Pole.B, Pole.A, Pole.B);

        Assert.Equal(1, communist.AgreementWith(communist), precision: 6);
        Assert.Equal(1.0 / 3, communist.AgreementWith(capitalist), precision: 6);
        Assert.Equal(0, communist.AgreementWith(new Ideology(Pole.B, Pole.B, Pole.B)), precision: 6);
    }

    [Fact]
    public void Changer_un_pole_donne_une_autre_ideologie_egale_a_celle_construite_directement()
    {
        var communist = new Ideology(Pole.A, Pole.A, Pole.A);

        var libertarian = communist.With(IdeologicalAxis.Property, Pole.B);

        Assert.Equal(new Ideology(Pole.A, Pole.A, Pole.B), libertarian);
        Assert.Equal("Libertaire", libertarian.Name);
    }
}
