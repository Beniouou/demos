using System.Text.Json;
using Demos.Domain.Map;

namespace Demos.Tests.Domain;

/// <summary>Les départs d'une carte (décidé le 23 novembre 2026) : fiche de pauvreté, verdict, départ préréglé.</summary>
public class StartSitesTests
{
    // Le camp est au milieu de la carte ; une unité vaut 100 m.
    private static readonly Point2D Camp = new(1000, 1000);

    /// <summary>Un carré de cette sorte, centré à (dx, dy) km du camp, de ce côté en km.</summary>
    private static PolygonArea Square(string kind, double dxKm, double dyKm, double sideKm)
    {
        var (cx, cy) = (Camp.X + dxKm * 10, Camp.Y + dyKm * 10);
        var h = sideKm * 5;
        return new PolygonArea(kind, kind, [new(cx - h, cy - h), new(cx + h, cy - h), new(cx + h, cy + h), new(cx - h, cy + h)]);
    }

    /// <summary>Une terre d'exil type : 3 × 3 km de terre fertile, forêt et pierre à 10 km, fer à 30 km.</summary>
    private static List<PolygonArea> PoorLand() =>
    [
        Square("fertile", 0, 0, 3),
        Square("foret", 10, 0, 4),
        Square("pierre", 0, 10, 2),
        Square("fer", 30, 0, 2),
    ];

    private static StartSiteReport ReportOf(IReadOnlyList<PolygonArea> zones) =>
        StartSites.Report(Camp, zones, [], elevation: null);

    [Fact]
    public void La_fiche_mesure_les_surfaces_les_emplois_et_les_gisements()
    {
        var report = ReportOf(PoorLand());

        Assert.Equal(9, report.FertileNearKm2, 0);
        Assert.Equal(9, report.FertileFarKm2, 0);
        Assert.Equal(0, report.ForestNearKm2);
        Assert.Equal(16, report.ForestFarKm2, 0);
        // 9 km² de terre fertile à 20 emplois par km².
        Assert.InRange(report.FarmJobs, 170, 190);
        Assert.Equal(0, report.ForestJobs);
        Assert.Equal(29, report.KmTo("fer")!.Value, 1);
        Assert.Equal(9, report.KmTo("pierre")!.Value, 1);
        Assert.Null(report.KmTo("charbon"));
    }

    [Fact]
    public void Une_terre_pauvre_mais_qui_permet_de_s_etendre_est_jouable()
    {
        var (verdict, reasons) = StartSites.Judge(ReportOf(PoorLand()));

        Assert.Equal(ExileVerdict.Playable, verdict);
        Assert.Empty(reasons);
    }

    [Fact]
    public void Sans_assez_de_terre_fertile_ou_sans_fer_atteignable_la_terre_est_injouable()
    {
        var tooSmall = PoorLand();
        tooSmall[0] = Square("fertile", 0, 0, 1.5);   // 2,25 km², 45 emplois
        Assert.Equal(ExileVerdict.Unplayable, StartSites.Judge(ReportOf(tooSmall)).Verdict);

        var ironTooFar = PoorLand();
        ironTooFar[3] = Square("fer", 60, 0, 2);
        var (verdict, reasons) = StartSites.Judge(ReportOf(ironTooFar));
        Assert.Equal(ExileVerdict.Unplayable, verdict);
        Assert.Contains(reasons, r => r.Contains("fer trop loin"));

        var noIron = PoorLand().Where(z => z.Kind != "fer").ToList();
        Assert.Equal(ExileVerdict.Unplayable, StartSites.Judge(ReportOf(noIron)).Verdict);
    }

    [Fact]
    public void Du_fer_a_portee_ou_trop_de_terre_fertile_rend_la_terre_trop_facile()
    {
        var ironNear = PoorLand();
        ironNear[3] = Square("fer", 12, 0, 2);
        Assert.Equal(ExileVerdict.TooEasy, StartSites.Judge(ReportOf(ironNear)).Verdict);

        var rich = PoorLand();
        rich[0] = Square("fertile", 0, 0, 12);
        Assert.Equal(ExileVerdict.TooEasy, StartSites.Judge(ReportOf(rich)).Verdict);
    }

    [Fact]
    public void Le_depart_preregle_ne_tient_pas_si_une_zone_manque_ou_deborde()
    {
        var zones = PoorLand();
        ZoneDemand[] demands =
        [
            new("Ferme (Blé)", ["fertile"], 120),
            new("Ferme (Lin)", ["fertile"], 120),
            new("Mine (Charbon)", ["charbon"], 50),
        ];

        var problems = StartSites.PresetProblems(demands, Camp, zones);

        // Les deux fermes partagent 9 km² (180 emplois) : 240 débordent ; et il n'y a pas de charbon.
        Assert.Contains(problems, p => p.StartsWith("Ferme (Blé) : zone pleine"));
        Assert.Contains(problems, p => p.StartsWith("Mine (Charbon) : pas de charbon"));
        Assert.Empty(StartSites.PresetProblems([new("Ferme (Blé)", ["fertile"], 150)], Camp, zones));
    }

    [Fact]
    public void Les_terres_d_exil_s_enregistrent_et_une_ancienne_carte_se_lit_sans_elles()
    {
        var commune = new CommuneData("commune-1", "Première commune", Camp);
        var map = new VectorMapDefinition("Essai", 2000, 2000, [], [], new RiverData("", []), [], [], [], commune);
        var camel = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var read = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var withoutSites = JsonSerializer.Serialize(map, camel);
        Assert.DoesNotContain("exileSites", withoutSites);
        Assert.Empty(JsonSerializer.Deserialize<VectorMapDefinition>(withoutSites, read)!.AllExileSites);

        var withSites = map with { ExileSites = [new ExileSite("Grève des Sables", new Point2D(300, 400))] };
        var back = JsonSerializer.Deserialize<VectorMapDefinition>(JsonSerializer.Serialize(withSites, camel), read)!;
        Assert.Equal("Grève des Sables", Assert.Single(back.AllExileSites).Name);
        Assert.Equal(new Point2D(300, 400), back.AllExileSites[0].Position);
    }
}
