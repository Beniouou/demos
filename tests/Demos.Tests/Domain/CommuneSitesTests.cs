using Demos.Domain.Map;

namespace Demos.Tests.Domain;

/// <summary>Où se fonde une commune (décidé le 12 novembre 2026) : ni l'eau, ni une falaise, ni un glacier, ni trop près d'une autre.</summary>
public class CommuneSitesTests
{
    // Une carte de 4 × 4 pixels de 100 unités (40 km de côté) : la mer sur la colonne de gauche.
    private static readonly ReliefGrid Relief = new(4, 4, 100,
        [-5, 10, 10, 10, -5, 10, 10, 10, -5, 10, 10, 10, -5, 10, 10, 10]);

    private static readonly PolygonArea Cliff = new("Falaise", "falaise", [new(300, 300), new(390, 300), new(390, 390), new(300, 390)]);
    private static readonly PolygonArea Glacier = new("Glacier", "glacier", [new(300, 0), new(390, 0), new(390, 90), new(300, 90)]);
    private static readonly PolygonArea Forest = new("Forêt", "foret", [new(150, 150), new(250, 150), new(250, 250), new(150, 250)]);

    private static string? Problem(Point2D position, params (string, Point2D)[] communes) =>
        CommuneSites.ProblemWith(position, [Cliff, Glacier, Forest], Relief, communes, minDistance: 16);

    [Fact]
    public void Une_commune_se_fonde_sur_la_terre_meme_en_foret() =>
        Assert.Null(Problem(new Point2D(200, 200)));

    [Fact]
    public void Pas_sur_l_eau_ni_hors_de_la_carte()
    {
        Assert.Equal("sur l'eau", Problem(new Point2D(20, 200)));
        Assert.Equal("hors de la carte", Problem(new Point2D(500, 200)));
    }

    [Fact]
    public void Pas_sur_une_falaise_ni_sur_un_glacier()
    {
        Assert.Equal("sur une falaise", Problem(new Point2D(350, 350)));
        Assert.Equal("sur un glacier", Problem(new Point2D(350, 50)));
    }

    [Fact]
    public void Pas_a_moins_de_la_distance_minimale_d_une_autre_commune()
    {
        // 100 unités = 10 km : trop près ; 200 unités = 20 km : permis.
        Assert.StartsWith("trop près de Capitale", Problem(new Point2D(200, 200), ("Capitale", new Point2D(200, 300))));
        Assert.Null(Problem(new Point2D(200, 100), ("Capitale", new Point2D(200, 300))));
    }
}
