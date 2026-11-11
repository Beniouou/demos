using System.Collections.Generic;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>Les fleuves et rivières (les 17 cours d'eau thalweg). Écritures (filtre Géographie physique) : leur nom, de près.</summary>
public sealed class RiversLayer : MapLayer
{
    public override string Name => "Fleuves";
    public override string Content => "Les fleuves et rivières";
    public override MapChange RedrawOn => MapChange.Camera | MapChange.Filters;

    public override void Draw(MapDrawContext c)
    {
        foreach (var (river, screenPoints) in VisibleRivers(c))
        {
            var riverWidth = (float)river.Width * c.Zoom;
            c.Pen.DrawPolyline(screenPoints, MapPalette.River, riverWidth, antialiased: true);
            c.Pen.DrawPolyline(screenPoints, MapPalette.RiverCore, System.Math.Max(1.0f, riverWidth * 0.4f), antialiased: true);
        }
    }

    public override void DrawLabels(MapDrawContext c)
    {
        if (!c.Has(MapFilters.Physical))
            return;

        foreach (var (river, screenPoints) in VisibleRivers(c))
        {
            if (c.Zoom >= 0.9f && screenPoints.Length >= 4 && !string.IsNullOrWhiteSpace(river.Name))
            {
                var textPos = screenPoints[screenPoints.Length / 2];
                c.Pen.DrawString(c.Font, textPos + new Vector2(10, -6), river.Name, HorizontalAlignment.Left, -1, 11, MapPalette.River);
            }
        }
    }

    /// <summary>Les cours d'eau à dessiner (rien hors de l'écran, nom compris) et leurs points à l'écran.</summary>
    private static IEnumerable<(RiverData River, Vector2[] ScreenPoints)> VisibleRivers(MapDrawContext c)
    {
        if (c.VectorMap == null)
            yield break;

        foreach (var river in c.VectorMap.AllRivers)
        {
            if (river.Points.Count < 2) continue;

            var shape = c.Scaled(river.Points);
            if (!c.IsOnScreen(shape, (float)river.Width * c.Zoom + 400f))
                continue;
            yield return (river, c.ToScreen(shape));
        }
    }
}
