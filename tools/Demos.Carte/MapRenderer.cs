using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Demos.Carte.Rendering;
using Demos.Domain.Map;

namespace Demos.Carte;

public enum MapRenderStyle
{
    IgnPastel,
    VintageParchment1874
}

/// <summary>
/// Façade du moteur de rendu cartographique pour Windows Forms (GDI+) :
/// délègue au moteur IGN Pastel (IgnMapRenderer) ou au moteur Parchemin Ancien 1874 (VintageMapRenderer).
/// </summary>
public static class MapRenderer
{
    public static void Render(
        Graphics g,
        Rectangle bounds,
        VectorMapDefinition map,
        Heightmap heightmap,
        MapRenderStyle style,
        float zoom = 1.0f,
        PointF panOffset = default,
        bool showContours = true,
        bool showLabels = true,
        bool showResources = true,
        bool showCadastre = true)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // Calcul de la matrice de transformation avec zoom et panoramique
        var baseScaleX = (float)bounds.Width / (float)map.Width;
        var baseScaleY = (float)bounds.Height / (float)map.Height;
        var baseScale = Math.Min(baseScaleX, baseScaleY);
        var finalScale = baseScale * zoom;

        var boundsCenter = new PointF(bounds.X + bounds.Width * 0.5f, bounds.Y + bounds.Height * 0.5f);
        var mapCenterX = boundsCenter.X + panOffset.X;
        var mapCenterY = boundsCenter.Y + panOffset.Y;

        var mapLeft = mapCenterX - (float)map.Width * finalScale * 0.5f;
        var mapTop = mapCenterY - (float)map.Height * finalScale * 0.5f;
        var mapRect = new RectangleF(mapLeft, mapTop, (float)map.Width * finalScale, (float)map.Height * finalScale);

        // Fond général hors-carte
        using (var bgBrush = new SolidBrush(Color.FromArgb(240, 235, 225)))
        {
            g.FillRectangle(bgBrush, bounds);
        }

        // Sauvegarder l'état pour appliquer le clipping et la transformation vers l'espace carte
        var state = g.Save();
        g.SetClip(mapRect);
        g.TranslateTransform(mapRect.X, mapRect.Y);
        g.ScaleTransform(finalScale, finalScale);

        if (style == MapRenderStyle.IgnPastel)
        {
            IgnMapRenderer.Render(g, map, heightmap, showContours, showLabels, showResources, showCadastre);
        }
        else
        {
            VintageMapRenderer.Render(g, map, heightmap, showContours, showLabels, showResources, showCadastre);
        }

        g.Restore(state);

        // Cadre extérieur et graduations
        DrawOuterBorder(g, mapRect, style);
    }

    private static void DrawOuterBorder(Graphics g, RectangleF mapRect, MapRenderStyle style)
    {
        var borderColor = (style == MapRenderStyle.IgnPastel)
            ? Color.FromArgb(122, 104, 82)
            : Color.FromArgb(70, 48, 28);

        using var framePen = new Pen(borderColor, 2.5f);
        using var innerPen = new Pen(borderColor, 1.0f);

        // Cadre principal
        g.DrawRectangle(framePen, mapRect.X, mapRect.Y, mapRect.Width, mapRect.Height);
        g.DrawRectangle(innerPen, mapRect.X + 4, mapRect.Y + 4, mapRect.Width - 8, mapRect.Height - 8);

        // Graduations d'arpentage sur les 4 arêtes
        using var tickPen = new Pen(borderColor, 1.0f);
        const int stepsX = 10;
        for (var i = 1; i < stepsX; i++)
        {
            var tx = mapRect.X + (mapRect.Width / stepsX) * i;
            g.DrawLine(tickPen, tx, mapRect.Y, tx, mapRect.Y + 4);
            g.DrawLine(tickPen, tx, mapRect.Bottom - 4, tx, mapRect.Bottom);
        }

        const int stepsY = 6;
        for (var j = 1; j < stepsY; j++)
        {
            var ty = mapRect.Y + (mapRect.Height / stepsY) * j;
            g.DrawLine(tickPen, mapRect.X, ty, mapRect.X + 4, ty);
            g.DrawLine(tickPen, mapRect.Right - 4, ty, mapRect.Right, ty);
        }
    }
}
