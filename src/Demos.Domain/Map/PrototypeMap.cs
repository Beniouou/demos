using System;
using System.Collections.Generic;

namespace Demos.Domain.Map;

/// <summary>
/// Carte pilote confectionnée pour le prototype : relief, fleuve, forêt, gisements de ressources
/// et emplacement de départ pour la première commune.
/// </summary>
public static class PrototypeMap
{
    public const int DefaultWidth = 60;
    public const int DefaultHeight = 40;
    public static readonly MapCoordinates DefaultCommunePosition = new(24, 22);

    /// <summary>
    /// Crée la carte topographique pilote du prototype (60 × 40).
    /// </summary>
    public static TopographicMap CreateHandcrafted()
    {
        var riverCoords = BuildRiverCoordinates();

        return new TopographicMap(DefaultWidth, DefaultHeight, (x, y) =>
        {
            var coord = new MapCoordinates(x, y);
            var isRiver = riverCoords.Contains(coord);

            // 1. Mer / Baie côtière au Sud-Ouest
            var seaDist = Math.Sqrt(Math.Pow(x - 0, 2) + Math.Pow(y - 39, 2));
            if (seaDist < 14)
            {
                return new TerrainCell(coord, TerrainType.Water, 0.0, HasRiver: isRiver);
            }

            // 2. Massif montagneux au Nord-Est
            // Centre du massif vers (42, 6)
            var mountainDist = Math.Sqrt(Math.Pow(x - 42, 2) + Math.Pow((y - 6) * 1.5, 2));
            if (mountainDist < 12 && !isRiver)
            {
                var elevation = Math.Clamp(1.0 - (mountainDist / 14.0), 0.55, 0.98);
                string? resource = null;
                if ((x == 32 && y == 10) || (x == 45 && y == 8) || (x == 38 && y == 5))
                    resource = "pierre";
                else if ((x == 28 && y == 12) || (x == 40 && y == 12))
                    resource = "charbon";

                var terrain = mountainDist < 8 ? TerrainType.Mountains : TerrainType.Hills;
                return new TerrainCell(coord, terrain, elevation, HasRiver: isRiver, NaturalResource: resource);
            }

            // 3. Collines de piémont autour des montagnes
            if (mountainDist < 18 && !isRiver && y < 20 && x > 20)
            {
                var elevation = Math.Clamp(0.55 - (mountainDist - 12) * 0.04, 0.3, 0.55);
                string? resource = (x == 26 && y == 15) ? "pierre" : null;
                return new TerrainCell(coord, TerrainType.Hills, elevation, HasRiver: isRiver, NaturalResource: resource);
            }

            // 4. Forêt dense à l'Ouest
            var forestDist = Math.Sqrt(Math.Pow(x - 8, 2) + Math.Pow((y - 18) * 0.8, 2));
            if (forestDist < 12 && !isRiver && seaDist >= 14)
            {
                var elevation = 0.25 + 0.1 * Math.Sin(x * 0.5 + y * 0.3);
                var resource = (x % 3 == 0 && y % 3 == 0) ? "bois" : null;
                return new TerrainCell(coord, TerrainType.Forest, elevation, HasRiver: isRiver, NaturalResource: resource);
            }

            // 5. Plaine alluviale centrale et orientale
            var plainElevation = 0.12 + 0.08 * Math.Sin(x * 0.15) * Math.Cos(y * 0.15);
            string? plainResource = null;
            if ((x == 22 && y == 23) || (x == 28 && y == 21) || (x == 34 && y == 24))
                plainResource = "ble";
            else if ((x == 25 && y == 25) || (x == 18 && y == 26))
                plainResource = "lin";

            return new TerrainCell(coord, TerrainType.Plains, plainElevation, HasRiver: isRiver, NaturalResource: plainResource);
        });
    }

    private static HashSet<MapCoordinates> BuildRiverCoordinates()
    {
        // Tracé continu d'un fleuve navigable depuis la source en montagne (44, 4) jusqu'à la mer (12, 38)
        var path = new List<MapCoordinates>();
        var points = new (int X, int Y)[]
        {
            (44, 4), (43, 7), (40, 10), (37, 13), (34, 16),
            (30, 18), (26, 21), (23, 23), (20, 26), (17, 29),
            (14, 33), (11, 36), (8, 38)
        };

        for (var i = 0; i < points.Length - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];
            var curX = p1.X;
            var curY = p1.Y;
            path.Add(new(curX, curY));

            while (curX != p2.X || curY != p2.Y)
            {
                if (curX < p2.X) curX++;
                else if (curX > p2.X) curX--;

                if (curY < p2.Y) curY++;
                else if (curY > p2.Y) curY--;

                path.Add(new(curX, curY));
            }
        }

        return [.. path];
    }
}
