using System.Collections.Generic;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Les petites images de la carte (<c>game/assets/carte/</c>, dessinées par l'outil de carte : <c>--images</c>) et de
/// quoi les poser par milliers en un seul appel à la carte graphique (<see cref="MultiMeshInstance2D"/>). Les images
/// sont lues telles quelles (pas d'import de Godot) : un dessin de même nom et de même taille les remplace.
/// </summary>
public static class MapSprites
{
    private static readonly Dictionary<string, Texture2D?> Textures = [];
    private static ArrayMesh? _quad;

    /// <summary>L'image <c>game/assets/carte/{name}.png</c>, avec ses réductions (nette de loin), ou rien si elle manque.</summary>
    public static Texture2D? Texture(string name)
    {
        if (Textures.TryGetValue(name, out var cached))
            return cached;
        Texture2D? texture = null;
        var bytes = FileAccess.GetFileAsBytes($"res://assets/carte/{name}.png");
        var image = new Image();
        if (bytes.Length > 0 && image.LoadPngFromBuffer(bytes) == Error.Ok)
        {
            image.GenerateMipmaps();
            texture = ImageTexture.CreateFromImage(image);
        }
        return Textures[name] = texture;
    }

    /// <summary>Un carré d'une unité de carte de côté, centré : chaque image posée l'agrandit à sa taille.</summary>
    public static ArrayMesh Quad
    {
        get
        {
            if (_quad is not null)
                return _quad;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = new Vector2[] { new(-0.5f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0.5f), new(-0.5f, 0.5f) };
            arrays[(int)Mesh.ArrayType.TexUV] = new Vector2[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
            arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
            _quad = new ArrayMesh();
            _quad.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return _quad;
        }
    }

    /// <summary>
    /// Une image posée à chaque endroit donné (en unités de carte : centre et taille), en un seul nœud ; avec, si demandé, son
    /// ombre portée dessous (un second nœud, voir <see cref="PlaceOnMap"/>).
    /// </summary>
    public static MultiMeshInstance2D Instances(Texture2D texture, IReadOnlyList<(Vector2 Center, float Size)> items, Color? tint = null)
    {
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            Mesh = Quad,
            InstanceCount = items.Count,
        };
        for (var i = 0; i < items.Count; i++)
            multimesh.SetInstanceTransform2D(i, new Transform2D(0, new Vector2(items[i].Size, items[i].Size), 0, items[i].Center));
        return new MultiMeshInstance2D
        {
            Multimesh = multimesh,
            Texture = texture,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            Modulate = tint ?? Colors.White,
        };
    }

    /// <summary>L'ombre portée de ces images : la même silhouette, sombre, décalée de <paramref name="offset"/> unités.</summary>
    public static MultiMeshInstance2D Shadow(MultiMeshInstance2D instances, float strength)
    {
        var material = MapShaders.Material("ombre");
        material.SetShaderParameter("strength", strength);
        return new MultiMeshInstance2D
        {
            Multimesh = instances.Multimesh,
            Texture = instances.Texture,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            Material = material,
        };
    }

    /// <summary>Met un nœud posé en unités de carte à sa place à l'écran (déplacement + point × zoom), décalé de <paramref name="offset"/> unités.</summary>
    public static void PlaceOnMap(Node2D node, MapCamera camera, Vector2 offset = default)
    {
        node.Position = camera.PanOffset + offset * camera.Zoom;
        node.Scale = new Vector2(camera.Zoom, camera.Zoom);
    }
}
