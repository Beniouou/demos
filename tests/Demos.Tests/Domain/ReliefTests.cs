using Demos.Domain.Map;

namespace Demos.Tests.Domain;

public class ReliefTests
{
    // Une île ronde au milieu d'une carte de 400 × 400 lieues : courbes de 100, 200 et 300 m (rayons 150, 100 et 50),
    // un sommet coté à 400 m au centre, et la mer au-delà de 180 lieues du centre.
    private static readonly Point2D Center = new(200, 200);

    private static ContourLine Circle(double altitude, double radius) =>
        new(altitude, altitude % 100 == 0, true, CirclePoints(radius));

    private static List<Point2D> CirclePoints(double radius) =>
        Enumerable.Range(0, 120)
            .Select(i => i * Math.Tau / 120)
            .Select(a => new Point2D(Center.X + radius * Math.Cos(a), Center.Y + radius * Math.Sin(a)))
            .ToList();

    private static readonly PolygonArea Sea = new("Mer", "Water",
        [new(0, 0), new(400, 0), new(400, 400), new(0, 400)],
        [CirclePoints(180)]);

    private static ReliefGrid Island(ReliefSettings? settings = null) =>
        ReliefBuilder.Build(400, 400,
            [Circle(100, 150), Circle(200, 100), Circle(300, 50)],
            [new SpotElevation(400, "Sommet", Center)],
            [Sea],
            settings ?? new ReliefSettings());

    [Fact]
    public void La_grille_a_un_pixel_par_taille_de_cellule()
    {
        var grid = Island();
        Assert.Equal(200, grid.Width);
        Assert.Equal(200, grid.Height);
        Assert.Equal(2.0, grid.CellSize);
    }

    [Theory]
    [InlineData(150, 100)]
    [InlineData(100, 200)]
    [InlineData(50, 300)]
    public void L_altitude_passe_par_les_courbes_de_niveau(double radius, double altitude)
    {
        var grid = Island();
        foreach (var angle in new[] { 0.3, 1.7, 4.0 })
        {
            var x = Center.X + radius * Math.Cos(angle);
            var y = Center.Y + radius * Math.Sin(angle);
            Assert.InRange(grid.Sample(x, y), altitude - 12, altitude + 12);
        }
    }

    [Fact]
    public void Le_sommet_cote_est_le_point_le_plus_haut_et_le_terrain_descend_vers_la_mer()
    {
        var grid = Island();
        Assert.InRange(grid.Sample(Center.X, Center.Y), 360, 400);

        var previous = double.MaxValue;
        for (var r = 0; r <= 170; r += 5)
        {
            var altitude = grid.Sample(Center.X + r, Center.Y);
            Assert.True(altitude <= previous + 0.5, $"à {r} lieues du centre : {altitude:F1} m après {previous:F1} m");
            previous = altitude;
        }
    }

    [Fact]
    public void La_mer_est_sous_zero_et_s_enfonce_avec_la_distance_a_la_cote()
    {
        var settings = new ReliefSettings();
        var grid = Island(settings);

        Assert.True(grid.IsWater(0, 0));
        Assert.False(grid.IsWater(100, 100));
        Assert.InRange(grid[0, 0], -settings.MaxSeaDepth, -1);
        // Près de la côte (à 186 lieues du centre), moins profond qu'au coin de la carte.
        Assert.True(grid.Sample(Center.X + 186, Center.Y) > grid[0, 0]);
        Assert.All(Enumerable.Range(0, grid.Width * grid.Height).Where(i => grid.Altitudes[i] >= 0),
            i => Assert.True(grid.Altitudes[i] >= 0));
    }

    [Fact]
    public void Un_terrain_plat_a_partout_la_meme_ombre()
    {
        var grid = new ReliefGrid(10, 10, 2, Enumerable.Repeat(50f, 100).ToArray());
        var shade = Hillshade.Compute(grid, new ReliefSettings { LightElevation = 45 });
        Assert.All(shade, value => Assert.Equal(180, value));
    }

    [Fact]
    public void Une_pente_tournee_vers_la_lumiere_est_plus_claire_que_celle_qui_lui_tourne_le_dos()
    {
        // Lumière du nord-ouest : un versant qui monte vers l'est regarde l'ouest (éclairé), un versant qui descend
        // vers l'est regarde l'est (dans l'ombre).
        var settings = new ReliefSettings();
        var rising = new ReliefGrid(10, 10, 2, Enumerable.Range(0, 100).Select(i => (float)(i % 10 * 20)).ToArray());
        var falling = new ReliefGrid(10, 10, 2, Enumerable.Range(0, 100).Select(i => (float)((9 - i % 10) * 20)).ToArray());

        var flat = 180;
        Assert.True(Hillshade.Compute(rising, settings)[55] > flat);
        Assert.True(Hillshade.Compute(falling, settings)[55] < flat);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(123.4)]
    [InlineData(-55.5)]
    [InlineData(2500)]
    public void L_altitude_codee_dans_l_image_se_relit_au_decimetre(double altitude)
    {
        var (high, low) = ReliefEncoding.Encode(altitude);
        Assert.InRange(ReliefEncoding.Decode(high, low), altitude - 0.05, altitude + 0.05);
    }
}
