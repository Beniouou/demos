using Demos.Domain.Economy;
using Demos.Domain.Map;
using Demos.Domain.Politics;

namespace Demos.Tests.Domain;

/// <summary>La taille d'un bâtiment et la limite des zones de la carte (décidées le 20 novembre 2026).</summary>
public class BuildingSizeAndZoneTests
{
    private static readonly BuildingType Farm = new("ferme", "Ferme", "Extraction", 1, "", 2_200, 2,
        Production: new Production("ble", 10), Construction: new ConstructionCost("materiaux-de-construction", 2_200, 30));

    [Fact]
    public void Une_ferme_a_10_pourcent_a_220_emplois_et_coute_220_materiaux()
    {
        var farm = Farm.Sized(Farm.Build("ferme-1", "commune"), 0.1);

        Assert.Equal(220, farm.Jobs);
        Assert.Equal(220, Farm.ConstructionQuantity(0.1), 6);
        Assert.Equal(2_200, Farm.ConstructionQuantity(1), 6);
    }

    [Fact]
    public void Une_taille_minuscule_garde_10_emplois_et_20_materiaux()
    {
        var small = new BuildingType("atelier", "Atelier", "Production", 1, "", 40, 2,
            Construction: new ConstructionCost("materiaux-de-construction", 40, 30));

        Assert.Equal(10, small.Sized(small.Build("a", "commune"), 0.1).Jobs);
        Assert.Equal(20, small.ConstructionQuantity(0.1), 6);
    }

    /// <summary>Une terre fertile carrée de 4 × 4 km (16 km²), tout entière à moins de 5 km de son centre.</summary>
    private static readonly PolygonArea Field = new("Champ", "fertile",
    [
        new(0, 0), new(MapScale.ToUnits(4), 0), new(MapScale.ToUnits(4), MapScale.ToUnits(4)), new(0, MapScale.ToUnits(4)),
    ]);

    private static readonly Point2D Middle = new(MapScale.ToUnits(2), MapScale.ToUnits(2));

    [Fact]
    public void Une_terre_fertile_de_16_km2_supporte_320_emplois()
    {
        var capacity = ZoneCapacity.CapacityAt(["fertile"], Middle, [Field]);

        // 16 km² × 20 emplois par km², mesuré sur une grille de 200 m (à une rangée de cases près).
        Assert.InRange(capacity, 300, 340);
    }

    [Fact]
    public void Les_emplois_des_batiments_voisins_de_la_meme_zone_comptent()
    {
        IReadOnlyList<string> fertile = ["fertile"];
        var used = ZoneCapacity.UsedAt(fertile, Middle,
        [
            (new Point2D(Middle.X + MapScale.ToUnits(1), Middle.Y), 200, fertile),
            (new Point2D(Middle.X + MapScale.ToUnits(9), Middle.Y), 500, fertile),
            (Middle, 300, (IReadOnlyList<string>)["foret"]),
        ]);

        // Seule la ferme à 1 km compte : l'autre est à 9 km, le camp de bûcheron est sur une autre sorte de zone.
        Assert.Equal(200, used, 6);
    }
}
