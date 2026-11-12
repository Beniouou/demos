using System;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map;

/// <summary>
/// La barre d'échelle, en bas à gauche de la carte : une longueur ronde en km (1, 2 ou 5 × une puissance de 10 : 500 m, 1 km, 20 km…),
/// choisie pour que la barre fasse entre <see cref="ScaleMinWidth"/> et 2,5 fois cette largeur à l'écran, au zoom
/// courant. Elle est découpée en quatre tronçons alternés, comme sur une carte d'état-major. Dessinée par la carte
/// elle-même, au-dessus de tout et hors de l'effet maquette.
/// </summary>
public partial class MapCanvas
{
    // La barre fait au moins tant de pixels.
    private const float ScaleMinWidth = 120f;

    /// <summary>La longueur ronde, en km, dont la barre fait au moins <see cref="ScaleMinWidth"/> pixels au zoom donné.</summary>
    public static double ScaleLength(float zoom)
    {
        var minimum = MapScale.ToKm(ScaleMinWidth / Math.Max(zoom, 0.0001f));
        var power = Math.Pow(10, Math.Floor(Math.Log10(minimum)));
        foreach (var step in new[] { 1.0, 2.0, 5.0, 10.0 })
            if (step * power >= minimum)
                return step * power;
        return 10 * power;
    }

    private void DrawScaleBar(Font font)
    {
        var km = ScaleLength(_camera.Zoom);
        var width = (float)(MapScale.ToUnits(km) * _camera.Zoom);
        const float Height = 6f;
        const int Segments = 4;

        // Au-dessus du cartouche de survol (en bas, 36 pixels).
        var origin = new Vector2(24, Size.Y - 58);
        var label = MapScale.Format(km);
        var labelSize = font.GetStringSize(label, HorizontalAlignment.Left, -1, 12);

        // Le fond clair, pour lire la barre sur n'importe quel terrain.
        var back = new Rect2(origin - new Vector2(8, labelSize.Y + 8), new Vector2(Math.Max(width, labelSize.X) + 16, Height + labelSize.Y + 16));
        DrawRect(back, MapPalette.ScaleBackground);

        var segment = width / Segments;
        for (var i = 0; i < Segments; i++)
            DrawRect(new Rect2(origin + new Vector2(i * segment, 0), new Vector2(segment, Height)),
                i % 2 == 0 ? MapPalette.ScaleDark : MapPalette.ScaleLight);
        DrawRect(new Rect2(origin, new Vector2(width, Height)), MapPalette.ScaleDark, filled: false, width: 1f);

        // Les graduations : 0 et la longueur entière.
        DrawString(font, origin + new Vector2(0, -4), "0", HorizontalAlignment.Left, -1, 11, MapPalette.InkText);
        DrawString(font, origin + new Vector2(width - labelSize.X, -4), label, HorizontalAlignment.Left, -1, 12, MapPalette.InkText);
    }
}
