using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using Demos.Domain.Map;

namespace Demos.Carte;

/// <summary>
/// Commande <c>--relief</c> : calcule une fois la carte d'altitude et ses ombres à partir de <c>carte.json</c>
/// (<see cref="ReliefBuilder"/>, <see cref="Hillshade"/>) et les enregistre dans <c>game/data/</c> :
/// - <c>relief-altitude.png</c> : l'altitude sur 16 bits (rouge et vert, voir <see cref="ReliefEncoding"/>), le bleu à 0
///   sur la mer et 255 sur terre ;
/// - <c>relief-ombres.png</c> : l'éclairement en gris (0 dans l'ombre, 255 face à la lumière) ;
/// - <c>relief.json</c> : la taille, le codage, les altitudes extrêmes et les réglages utilisés.
/// Le jeu ne fait que les lire. Avec un chemin en plus, écrit aussi un aperçu en couleurs (altitude et ombres).
/// </summary>
internal static class ReliefExport
{
    public const string AltitudeFile = "relief-altitude.png";
    public const string ShadeFile = "relief-ombres.png";
    public const string InfoFile = "relief.json";

    public static int Run(string dataDir, string? previewPath)
    {
        var mapPath = Path.Combine(dataDir, "carte.json");
        var map = JsonSerializer.Deserialize<VectorMapDefinition>(File.ReadAllText(mapPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (map is null)
        {
            Console.WriteLine("carte.json illisible.");
            return 1;
        }

        var settings = new ReliefSettings();
        var watch = Stopwatch.StartNew();
        var grid = ReliefBuilder.Build(map, settings);
        var shade = Hillshade.Compute(grid, settings);
        var computeMs = watch.ElapsedMilliseconds;

        SaveRgb(Path.Combine(dataDir, AltitudeFile), grid.Width, grid.Height, i =>
        {
            var (high, low) = ReliefEncoding.Encode(grid.Altitudes[i]);
            return (high, low, grid.Altitudes[i] < 0 ? (byte)0 : (byte)255);
        });
        SaveRgb(Path.Combine(dataDir, ShadeFile), grid.Width, grid.Height, i => (shade[i], shade[i], shade[i]));

        var min = grid.Altitudes.Min();
        var max = grid.Altitudes.Max();
        var info = new
        {
            source = "carte.json",
            altitude = AltitudeFile,
            ombres = ShadeFile,
            largeur = grid.Width,
            hauteur = grid.Height,
            lieuesParPixel = grid.CellSize,
            codage = "altitude = (rouge × 256 + vert) / 10 − 1000 (m) ; bleu = 0 sur la mer, 255 sur terre",
            altitudeMin = Math.Round(min, 1),
            altitudeMax = Math.Round(max, 1),
            reglages = settings,
        };
        File.WriteAllText(Path.Combine(dataDir, InfoFile),
            JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + Environment.NewLine);

        if (previewPath is not null)
            SavePreview(previewPath, grid, shade, max);

        Console.WriteLine($"Relief : {grid.Width} × {grid.Height} pixels ({grid.CellSize} lieues par pixel), altitude de {min:F0} à {max:F0} m, calculé en {computeMs} ms.");
        Console.WriteLine($"Écrit : {AltitudeFile}, {ShadeFile}, {InfoFile} dans {dataDir}");
        return 0;
    }

    private static void SaveRgb(string path, int width, int height, Func<int, (byte R, byte G, byte B)> pixel)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        var row = new byte[data.Stride];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = pixel(y * width + x);
                // Les octets d'un pixel sont dans l'ordre bleu, vert, rouge.
                row[x * 3] = b;
                row[x * 3 + 1] = g;
                row[x * 3 + 2] = r;
            }
            Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
        }
        bitmap.UnlockBits(data);
        bitmap.Save(path, ImageFormat.Png);
    }

    /// <summary>Un aperçu pour l'œil : teintes d'altitude (bleu, vert, ocre, brun, blanc) multipliées par les ombres.</summary>
    private static void SavePreview(string path, ReliefGrid grid, byte[] shade, float max)
    {
        (double At, double R, double G, double B)[] ramp =
        [
            (-60, 70, 110, 150), (0, 150, 190, 210), (0.01, 120, 160, 100), (0.25, 170, 185, 110),
            (0.5, 200, 170, 110), (0.75, 150, 115, 85), (1.0, 245, 245, 240),
        ];
        SaveRgb(path, grid.Width, grid.Height, i =>
        {
            var altitude = grid.Altitudes[i];
            var t = altitude < 0 ? altitude : altitude / Math.Max(1, max);
            var k = 0;
            while (k < ramp.Length - 2 && t > ramp[k + 1].At) k++;
            var (a, b) = (ramp[k], ramp[k + 1]);
            var f = Math.Clamp((t - a.At) / (b.At - a.At), 0, 1);
            var light = altitude < 0 ? 1.0 : shade[i] / 180.0;
            byte C(double from, double to) => (byte)Math.Clamp((from + (to - from) * f) * light, 0, 255);
            return (C(a.R, b.R), C(a.G, b.G), C(a.B, b.B));
        });
    }
}
