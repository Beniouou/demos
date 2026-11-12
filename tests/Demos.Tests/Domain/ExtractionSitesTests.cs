using Demos.Domain.Map;

namespace Demos.Tests.Domain;

public class ExtractionSitesTests
{
    // Une forêt carrée de 100 unités (10 km) de côté, avec une clairière au milieu, et un gisement de charbon à côté.
    private static readonly PolygonArea Forest = new("Bois", "foret",
        [new(0, 0), new(100, 0), new(100, 100), new(0, 100)],
        [[new(40, 40), new(60, 40), new(60, 60), new(40, 60)]]);

    private static readonly PolygonArea Coal = new("Houillère", "charbon", [new(200, 0), new(300, 0), new(300, 100), new(200, 100)]);

    private static readonly PolygonArea[] Zones = [Forest, Coal];

    [Theory]
    [InlineData("camp-forestier", "bois", "foret")]
    [InlineData("ferme", "ble", "fertile")]
    [InlineData("carriere", "pierre", "pierre")]
    [InlineData("mine", "charbon", "charbon")]
    [InlineData("mine", "minerai-fer", "fer")]
    public void Chaque_batiment_d_extraction_demande_sa_zone(string typeId, string productId, string zone) =>
        Assert.Equal([zone], ExtractionSites.ZonesFor(typeId, productId));

    [Fact]
    public void Une_mine_d_un_autre_minerai_va_sur_le_charbon_ou_le_fer_et_les_autres_batiments_sans_zone()
    {
        Assert.Equal(["charbon", "fer"], ExtractionSites.ZonesFor("mine", "or"));
        Assert.Empty(ExtractionSites.ZonesFor("puits", "eau"));
        Assert.Empty(ExtractionSites.ZonesFor("tour-de-forage", null));
    }

    [Fact]
    public void Un_camp_de_bucheron_se_pose_dans_la_foret_a_portee_mais_pas_dans_la_clairiere()
    {
        var commune = new Point2D(150, 50);
        string? Problem(Point2D p) => ExtractionSites.ProblemWith(["foret"], p, commune, 24, Zones);

        Assert.Null(Problem(new(10, 10)));
        Assert.NotNull(Problem(new(50, 50)));   // la clairière, un trou de la forêt
        Assert.NotNull(Problem(new(250, 50)));  // le charbon, pas une forêt
        Assert.Contains("trop loin", ExtractionSites.ProblemWith(["foret"], new(10, 10), new(1000, 1000), 24, Zones));
    }

    [Fact]
    public void L_emplacement_le_plus_proche_est_sur_la_zone_et_a_portee()
    {
        var commune = new Point2D(150, 50);

        var site = ExtractionSites.NearestSite(["charbon"], commune, 24, Zones);

        Assert.NotNull(site);
        Assert.True(ExtractionSites.Contains(Coal, site.Value));
        Assert.Null(ExtractionSites.NearestSite(["fer"], commune, 24, Zones));
        Assert.Null(ExtractionSites.NearestSite(["charbon"], new Point2D(2000, 2000), 24, Zones));
    }
}
