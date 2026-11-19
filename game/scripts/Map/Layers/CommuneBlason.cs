using System;
using System.Collections.Generic;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Le blason rond d'une commune sur la carte (décidé le 19 novembre 2026), dessiné par Godot. Le champ est partagé en
/// deux émaux (un métal, une couleur) selon un partage — coupé, parti, tranché ou écartelé —, tous tirés du nom de la
/// commune : le même d'une partie à l'autre. Au centre, une figure dit son activité principale ; la bordure dit son
/// rang (or : capitale, pourpre : chef-lieu de préfecture, argent : commune).
/// </summary>
public static class CommuneBlason
{
    /// <summary>La figure du centre : l'activité principale de la commune (étoile si elle ne produit rien).</summary>
    public enum Charge { Star, Wheat, Tree, Tower, Hammer, Drop, Flame }

    private const int ArcPoints = 24;

    /// <summary>La figure d'un produit : le plus fabriqué (en valeur) de la commune.</summary>
    public static Charge ChargeOf(string? productId) => productId switch
    {
        null => Charge.Star,
        "ble" or "farine" or "pain" or "alcool" or "lin" or "carcasse" or "viande" or "cuir" => Charge.Wheat,
        "bois" or "planche" or "meubles" => Charge.Tree,
        "pierre" or "marbre" or "ciment" or "sculpture" => Charge.Tower,
        _ when productId.StartsWith("materiaux", StringComparison.Ordinal) => Charge.Tower,
        "eau" => Charge.Drop,
        "charbon" or "petrole" or "gaz" or "fioul" or "energie" => Charge.Flame,
        _ => Charge.Hammer,
    };

    /// <summary>Dessine le blason : champ, figure, bordure ; <paramref name="hash"/> vient du nom de la commune.</summary>
    public static void Draw(CanvasItem pen, Vector2 center, float radius, int hash, Charge charge, Color border)
    {
        var metal = MapPalette.HeraldicMetals[Index(hash, 3, MapPalette.HeraldicMetals.Length)];
        var colour = MapPalette.HeraldicColours[Index(hash, 7, MapPalette.HeraldicColours.Length)];

        // Une ombre légère, puis le champ.
        pen.DrawCircle(center + new Vector2(1, 1.5f) * (radius / 10), radius + 1.5f, new Color(0, 0, 0, 0.35f));
        pen.DrawCircle(center, radius, colour);
        switch (Index(hash, 11, 4))
        {
            case 0: // Coupé : le haut en métal.
                pen.DrawColoredPolygon(Sector(center, radius, Mathf.Pi, Mathf.Tau), metal);
                break;
            case 1: // Parti : la gauche en métal.
                pen.DrawColoredPolygon(Sector(center, radius, Mathf.Pi / 2, Mathf.Pi * 1.5f), metal);
                break;
            case 2: // Tranché : du coin haut gauche au coin bas droit.
                pen.DrawColoredPolygon(Sector(center, radius, Mathf.Pi * 0.75f, Mathf.Pi * 1.75f), metal);
                break;
            default: // Écartelé : deux quartiers opposés en métal.
                pen.DrawColoredPolygon(Sector(center, radius, Mathf.Pi, Mathf.Pi * 1.5f), metal);
                pen.DrawColoredPolygon(Sector(center, radius, 0, Mathf.Pi / 2), metal);
                break;
        }

        DrawCharge(pen, center, radius * 0.55f, charge, metal == MapPalette.HeraldicMetals[0] ? MapPalette.HeraldicMetals[1] : MapPalette.HeraldicMetals[0]);

        // La bordure : son émail dit le rang ; un filet sombre la détache du champ et de la carte.
        pen.DrawArc(center, radius, 0, Mathf.Tau, ArcPoints * 2, border, Math.Max(2, radius * 0.22f), true);
        pen.DrawArc(center, radius + Math.Max(1, radius * 0.11f), 0, Mathf.Tau, ArcPoints * 2, MapPalette.HeraldicOutline, 1, true);
    }

    /// <summary>Un nombre de 0 à <paramref name="count"/> - 1 tiré du nom, différent pour chaque choix (<paramref name="salt"/>).</summary>
    private static int Index(int hash, int salt, int count) => (int)((uint)(hash * salt + salt * 7919) % (uint)count);

    /// <summary>Une part du disque, de l'angle <paramref name="from"/> à <paramref name="to"/> (y vers le bas).</summary>
    private static Vector2[] Sector(Vector2 center, float radius, float from, float to)
    {
        var points = new List<Vector2> { center };
        for (var index = 0; index <= ArcPoints; index++)
        {
            var angle = from + (to - from) * index / ArcPoints;
            points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
        return [.. points];
    }

    /// <summary>
    /// La figure, dans un métal (celui que le champ n'a pas, pour qu'elle se voie sur les deux émaux) avec un filet
    /// sombre. <paramref name="size"/> est son demi-côté.
    /// </summary>
    private static void DrawCharge(CanvasItem pen, Vector2 c, float size, Charge charge, Color fill)
    {
        var outline = MapPalette.HeraldicOutline;
        var line = Math.Max(1, size * 0.14f);
        Vector2 P(float x, float y) => c + new Vector2(x, y) * size;
        void Shape(Vector2[] points)
        {
            pen.DrawColoredPolygon(points, fill);
            pen.DrawPolyline([.. points, points[0]], outline, line, true);
        }
        void Disc(Vector2 at, float r)
        {
            pen.DrawCircle(at, r, outline);
            pen.DrawCircle(at, Math.Max(0.5f, r - line), fill);
        }

        switch (charge)
        {
            case Charge.Wheat: // Un épi : une tige et trois paires de grains.
                pen.DrawLine(P(0, 0.95f), P(0, -0.9f), outline, line * 1.6f, true);
                for (var row = 0; row < 3; row++)
                {
                    var y = -0.65f + row * 0.45f;
                    Disc(P(-0.25f, y), size * 0.22f);
                    Disc(P(0.25f, y), size * 0.22f);
                }
                Disc(P(0, -0.85f), size * 0.2f);
                break;
            case Charge.Tree: // Un arbre : un fût et une couronne.
                Shape([P(-0.15f, 0.95f), P(0.15f, 0.95f), P(0.15f, 0.1f), P(-0.15f, 0.1f)]);
                Disc(P(0, -0.3f), size * 0.62f);
                break;
            case Charge.Tower: // Une tour crénelée, porte ouverte.
                Shape([P(-0.55f, 0.95f), P(0.55f, 0.95f), P(0.55f, -0.55f), P(0.35f, -0.55f), P(0.35f, -0.9f), P(0.12f, -0.9f),
                    P(0.12f, -0.55f), P(-0.12f, -0.55f), P(-0.12f, -0.9f), P(-0.35f, -0.9f), P(-0.35f, -0.55f), P(-0.55f, -0.55f)]);
                pen.DrawRect(new Rect2(P(-0.17f, 0.35f), new Vector2(0.34f, 0.6f) * size), outline);
                break;
            case Charge.Hammer: // Un marteau : un manche en biais et une tête.
                Shape([P(-0.75f, 0.85f), P(-0.55f, 0.95f), P(0.25f, -0.25f), P(0.05f, -0.35f)]);
                Shape([P(-0.25f, -0.55f), P(0.15f, -0.95f), P(0.75f, -0.35f), P(0.35f, 0.05f)]);
                break;
            case Charge.Drop: // Une goutte.
                Shape([P(0, -0.95f), P(0.45f, -0.05f), P(0.5f, 0.3f), P(0.35f, 0.7f), P(0, 0.9f), P(-0.35f, 0.7f), P(-0.5f, 0.3f), P(-0.45f, -0.05f)]);
                break;
            case Charge.Flame: // Une flamme à trois pointes.
                Shape([P(0, 0.95f), P(-0.6f, 0.5f), P(-0.55f, -0.2f), P(-0.3f, 0.05f), P(-0.15f, -0.7f), P(0.05f, -0.2f), P(0.2f, -0.95f),
                    P(0.45f, -0.25f), P(0.6f, 0.45f)]);
                break;
            default: // Une étoile à cinq branches.
                var star = new Vector2[10];
                for (var index = 0; index < 10; index++)
                {
                    var angle = -Mathf.Pi / 2 + index * Mathf.Pi / 5;
                    var r = index % 2 == 0 ? 0.95f : 0.4f;
                    star[index] = P(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
                }
                Shape(star);
                break;
        }
    }
}
