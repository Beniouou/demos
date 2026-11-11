using System.Buffers.Binary;
using System.IO.Compression;

namespace Demos.Infrastructure;

/// <summary>
/// Lit une image PNG simple (8 bits par couleur, sans entrelacement, en RVB ou RVBA), sans bibliothèque d'images : de
/// quoi lire la carte d'altitude (<c>relief-altitude.png</c>) hors de Godot, dans la simulation et ses outils.
/// </summary>
public static class PngReader
{
    /// <summary>Les pixels de l'image : largeur, hauteur, et pour chaque pixel ses octets (3 ou 4), ligne par ligne.</summary>
    public sealed record Image(int Width, int Height, int Channels, byte[] Pixels)
    {
        public (byte R, byte G, byte B) this[int x, int y]
        {
            get
            {
                var i = (y * Width + x) * Channels;
                return (Pixels[i], Pixels[i + 1], Pixels[i + 2]);
            }
        }
    }

    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static Image Read(byte[] png)
    {
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException("Ce n'est pas une image PNG.");

        int width = 0, height = 0, channels = 0;
        using var compressed = new MemoryStream();
        var position = 8;
        while (position + 8 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(position));
            var type = System.Text.Encoding.ASCII.GetString(png, position + 4, 4);
            var data = png.AsSpan(position + 8, length);
            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    var bitDepth = data[8];
                    var colorType = data[9];
                    var interlace = data[12];
                    if (bitDepth != 8 || interlace != 0 || colorType is not (2 or 6))
                        throw new InvalidDataException("PNG non pris en charge (8 bits, RVB ou RVBA, sans entrelacement).");
                    channels = colorType == 2 ? 3 : 4;
                    break;
                case "IDAT":
                    compressed.Write(data);
                    break;
            }
            position += 12 + length;
            if (type == "IEND")
                break;
        }

        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        return new Image(width, height, channels, Unfilter(raw.ToArray(), width, height, channels));
    }

    /// <summary>Défait le filtre de chaque ligne (aucun, gauche, haut, moyenne, Paeth).</summary>
    private static byte[] Unfilter(byte[] data, int width, int height, int channels)
    {
        var stride = width * channels;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            var filter = data[y * (stride + 1)];
            var source = y * (stride + 1) + 1;
            var row = y * stride;
            for (var x = 0; x < stride; x++)
            {
                int left = x >= channels ? pixels[row + x - channels] : 0;
                int up = y > 0 ? pixels[row - stride + x] : 0;
                int upLeft = x >= channels && y > 0 ? pixels[row - stride + x - channels] : 0;
                int value = data[source + x];
                pixels[row + x] = (byte)(filter switch
                {
                    1 => value + left,
                    2 => value + up,
                    3 => value + (left + up) / 2,
                    4 => value + Paeth(left, up, upLeft),
                    _ => value,
                });
            }
        }
        return pixels;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
