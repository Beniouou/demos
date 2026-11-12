using System;
using System.Text.Json;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Le relief calculé une fois par l'outil de carte (<c>--relief</c>) : la carte d'altitude et ses ombres, lues dans
/// <c>game/data/</c> (<c>relief.json</c>, <c>relief-altitude.png</c>, <c>relief-ombres.png</c>). Rien n'est calculé ici.
/// Les images sont lues octet par octet (pas d'import de Godot) : l'altitude, codée sur deux octets, doit rester exacte.
/// </summary>
public sealed class MapRelief
{
    private readonly Image _altitudeImage;

    private MapRelief(Image altitude, Image shade, Vector2 texels, float cellSize)
    {
        _altitudeImage = altitude;
        Altitude = ImageTexture.CreateFromImage(altitude);
        Shade = ImageTexture.CreateFromImage(shade);
        Texels = texels;
        CellSize = cellSize;
    }

    /// <summary>L'altitude : rouge et vert sur 16 bits (décimètres au-dessus de −1 000 m), bleu 0 sur la mer.</summary>
    public ImageTexture Altitude { get; }

    /// <summary>Les ombres, en gris (0,706 sur un terrain plat).</summary>
    public ImageTexture Shade { get; }

    /// <summary>Le nombre de pixels des images (largeur, hauteur).</summary>
    public Vector2 Texels { get; }

    /// <summary>La taille d'un pixel, en unités de carte.</summary>
    public float CellSize { get; }

    /// <summary>L'altitude (m) d'un point de la carte (unités de carte), au pixel le plus proche ; la mer est sous 0.</summary>
    public float AltitudeAt(Vector2 world)
    {
        var x = Mathf.Clamp((int)(world.X / CellSize), 0, (int)Texels.X - 1);
        var y = Mathf.Clamp((int)(world.Y / CellSize), 0, (int)Texels.Y - 1);
        var pixel = _altitudeImage.GetPixel(x, y);
        return (Mathf.RoundToInt(pixel.R * 255) * 256 + Mathf.RoundToInt(pixel.G * 255)) / 10f - 1000f;
    }

    public static MapRelief? Load()
    {
        const string Info = "res://data/relief.json";
        try
        {
            if (!FileAccess.FileExists(Info))
                return null;
            using var json = JsonDocument.Parse(FileAccess.GetFileAsString(Info));
            var root = json.RootElement;
            var altitude = Image(root.GetProperty("altitude").GetString()!);
            var shade = Image(root.GetProperty("ombres").GetString()!);
            if (altitude is null || shade is null)
                return null;
            return new MapRelief(altitude, shade,
                new Vector2(root.GetProperty("largeur").GetInt32(), root.GetProperty("hauteur").GetInt32()),
                (float)(root.GetProperty("metresParPixel").GetDouble() / Demos.Domain.Map.MapScale.MetersPerUnit));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Relief illisible : {ex.Message}");
            return null;
        }
    }

    private static Image? Image(string file)
    {
        var bytes = FileAccess.GetFileAsBytes($"res://data/{file}");
        if (bytes.Length == 0)
            return null;
        var image = new Image();
        return image.LoadPngFromBuffer(bytes) == Error.Ok ? image : null;
    }
}

/// <summary>Les shaders de la carte (<c>res://shaders/carte/</c>) et les réglages qu'ils partagent : la caméra et le relief.</summary>
public static class MapShaders
{
    public static ShaderMaterial Material(string name) =>
        new() { Shader = GD.Load<Shader>($"res://shaders/carte/{name}.gdshader") };

    /// <summary>Donne au shader la caméra (déplacement, zoom) et, s'il y en a un, le relief.</summary>
    public static void SetView(ShaderMaterial material, MapDrawContext c)
    {
        material.SetShaderParameter("pan", c.Camera.PanOffset);
        material.SetShaderParameter("zoom", c.Zoom);
        if (c.Data.Relief is { } relief)
        {
            material.SetShaderParameter("altitude_tex", relief.Altitude);
            material.SetShaderParameter("shade_tex", relief.Shade);
            material.SetShaderParameter("texel_count", relief.Texels);
            material.SetShaderParameter("cell_size", relief.CellSize);
        }
    }
}
