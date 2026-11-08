using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Demos.Carte.Rendering;

/// <summary>
/// Bibliothèque de pictogrammes et primitives géométriques cartographiques
/// partagés entre les styles de rendu IGN Pastel et Parchemin Ancien.
/// </summary>
public static class CartoSymbols
{
    /// <summary>
    /// Test d'inclusion d'un point dans un polygone (algorithme Ray Casting).
    /// </summary>
    public static bool IsPointInPolygon(float x, float y, PointF[] poly)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if (((poly[i].Y > y) != (poly[j].Y > y)) &&
                (x < (poly[j].X - poly[i].X) * (y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>
    /// Bosquet stylisé doux (style IGN).
    /// </summary>
    public static void DrawTreeCluster(Graphics g, Brush brush, float x, float y, float r)
    {
        g.FillEllipse(brush, x - r, y - r, r * 2, r * 2);
        g.FillEllipse(brush, x + r * 0.7f - r * 0.8f, y + r * 0.2f - r * 0.8f, r * 1.6f, r * 1.6f);
        g.FillEllipse(brush, x - r * 0.6f - r * 0.7f, y + r * 0.3f - r * 0.7f, r * 1.4f, r * 1.4f);
    }

    /// <summary>
    /// Canopée d'arbre gravée à la plume d'oie avec tronc (style ancien 1874).
    /// </summary>
    public static void DrawHandDrawnTree(Graphics g, Pen pen, float x, float y, float r)
    {
        g.DrawArc(pen, x - r, y - r, r * 1.5f, r * 1.5f, 180, 180);
        g.DrawArc(pen, x - r * 0.3f, y - r * 1.2f, r * 1.4f, r * 1.4f, 200, 180);
        g.DrawArc(pen, x + r * 0.4f, y - r * 0.7f, r * 1.2f, r * 1.2f, 220, 160);
        g.DrawLine(pen, x + r * 0.2f, y + r * 0.3f, x + r * 0.2f, y + r * 0.8f);
    }

    /// <summary>
    /// Brin de blé / sillons arables (terres fertiles).
    /// </summary>
    public static void DrawWheatStalk(Graphics g, Pen pen, float x, float y, float r)
    {
        g.DrawLine(pen, x, y + r, x, y - r);
        g.DrawLine(pen, x, y - r * 0.3f, x - r * 0.6f, y - r * 0.8f);
        g.DrawLine(pen, x, y - r * 0.3f, x + r * 0.6f, y - r * 0.8f);
        g.DrawLine(pen, x, y - r * 0.7f, x - r * 0.5f, y - r * 1.2f);
        g.DrawLine(pen, x, y - r * 0.7f, x + r * 0.5f, y - r * 1.2f);
    }

    /// <summary>
    /// Touffe de roseaux avec reflet aquatique (marais).
    /// </summary>
    public static void DrawReedTuft(Graphics g, Pen pen, float x, float y, float r)
    {
        g.DrawLine(pen, x, y + r * 0.5f, x, y - r * 0.8f);
        g.DrawLine(pen, x, y + r * 0.5f, x - r * 0.6f, y - r * 0.6f);
        g.DrawLine(pen, x, y + r * 0.5f, x + r * 0.6f, y - r * 0.6f);
        g.DrawLine(pen, x - r * 0.8f, y + r * 0.6f, x + r * 0.8f, y + r * 0.6f);
    }

    /// <summary>
    /// Ponctuation et semis de sable sablonneux (plage).
    /// </summary>
    public static void DrawSandStipples(Graphics g, Brush brush, float x, float y, float r)
    {
        g.FillRectangle(brush, x - 1.5f, y - 1.5f, 2f, 2f);
        g.FillRectangle(brush, x + r * 0.5f, y - r * 0.3f, 1.5f, 1.5f);
        g.FillRectangle(brush, x - r * 0.4f, y + r * 0.4f, 1.5f, 1.5f);
    }

    /// <summary>
    /// Arête rocheuse et crans de falaise (dénivelé marqué).
    /// </summary>
    public static void DrawCliffRock(Graphics g, Pen pen, float x, float y, float r)
    {
        g.DrawLine(pen, x - r, y, x + r, y);
        g.DrawLine(pen, x - r * 0.6f, y, x - r * 0.8f, y + r * 0.8f);
        g.DrawLine(pen, x, y, x - r * 0.2f, y + r * 0.9f);
        g.DrawLine(pen, x + r * 0.6f, y, x + r * 0.4f, y + r * 0.8f);
    }

    /// <summary>
    /// Rides éoliennes de dunes (désert).
    /// </summary>
    public static void DrawDuneRipple(Graphics g, Pen pen, float x, float y, float r)
    {
        g.DrawArc(pen, x - r, y - r * 0.4f, r, r * 0.8f, 200, 140);
        g.DrawArc(pen, x, y - r * 0.2f, r, r * 0.8f, 20, 140);
    }

    /// <summary>
    /// Crevasse brisée et sérac anguleux (glacier).
    /// </summary>
    public static void DrawGlacierCrevasse(Graphics g, Pen pen, float x, float y, float r)
    {
        var p1 = new PointF(x - r, y - r * 0.5f);
        var p2 = new PointF(x - r * 0.2f, y + r * 0.2f);
        var p3 = new PointF(x + r * 0.3f, y - r * 0.2f);
        var p4 = new PointF(x + r, y + r * 0.6f);
        g.DrawLines(pen, new[] { p1, p2, p3, p4 });
    }
}
