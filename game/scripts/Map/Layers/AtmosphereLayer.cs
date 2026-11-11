using System;
using System.Collections.Generic;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// L'atmosphère de la vue Monde : les fumées qui montent des villages (shader <c>fumee.gdshader</c>, image
/// <c>fumee.png</c>) et les nuages qui passent avec leur ombre (shader <c>nuages.gdshader</c>). Dans la scène animée,
/// au-dessus du fond fixe : tout le mouvement est calculé par la carte graphique. Seulement sans filtre (la vue Monde
/// seule), pour ne jamais cacher une information.
/// </summary>
public sealed class AtmosphereLayer : MapLayer
{
    private Node2D? _node;
    private ShaderMaterial? _clouds;
    private MultiMeshInstance2D? _smoke;
    private int _smokeKey;

    public override string Name => "Atmosphère";
    public override string Content => "Les fumées des villages et les nuages qui passent";
    public override MapChange RedrawOn => MapChange.Camera | MapChange.Filters | MapChange.Game;
    public override MapLayerPlace Place => MapLayerPlace.AnimatedAbove;

    public override void CreateNodes(Node2D node, MapDrawContext c)
    {
        _node = node;
        if (c.Data.Relief is not null)
        {
            _clouds = MapShaders.Material("nuages");
            node.Material = _clouds;
        }
    }

    public override void UpdateNodes(MapDrawContext c)
    {
        if (_node is null)
            return;
        // L'atmosphère seulement dans la vue Monde seule.
        _node.Visible = c.Filters == MapFilters.None;
        if (_clouds is not null)
            MapShaders.SetView(_clouds, c);

        var key = SmokeKey(c);
        if (key != _smokeKey)
        {
            _smokeKey = key;
            BuildSmoke(c);
        }
        if (_smoke is not null)
            MapSprites.PlaceOnMap(_smoke, c.Camera);
    }

    // Le calque lui-même : un rectangle de la taille de la carte, colorié par le shader des nuages.
    public override void Draw(MapDrawContext c)
    {
        if (_clouds is not null)
            c.Pen.DrawRect(new Rect2(Vector2.Zero, c.Size), Colors.White);
    }

    private static int SmokeKey(MapDrawContext c)
    {
        var key = new HashCode();
        foreach (var (id, _, pos, population) in c.Data.Communes())
        {
            key.Add(id);
            key.Add(pos);
            key.Add(Chimneys(population));
        }
        return key.ToHashCode();
    }

    private static int Chimneys(double population) =>
        Math.Max(1, (int)Math.Round(population / MapPalette.ResidentsPerHouse / MapPalette.HousesPerChimney));

    /// <summary>
    /// Les bouffées de fumée : pour chaque cheminée (près du centre du village, toujours au même endroit), quelques
    /// bouffées décalées dans le cycle. Leur mouvement est dans le shader ; ici, seulement leurs données de départ.
    /// </summary>
    private void BuildSmoke(MapDrawContext c)
    {
        _smoke?.QueueFree();
        _smoke = null;
        if (_node is null || MapSprites.Texture("fumee") is not { } texture)
            return;

        var puffs = new List<Color>();
        foreach (var (id, _, center, population) in c.Data.Communes())
        {
            var random = new Random(id.Length * 7919 + (int)center.X * 31 + (int)center.Y);
            var chimneys = Chimneys(population);
            var radius = MapPalette.HouseSpacing * MathF.Sqrt(chimneys * MapPalette.HousesPerChimney) * 0.6f;
            for (var k = 0; k < chimneys; k++)
            {
                var angle = random.NextSingle() * MathF.Tau;
                var chimney = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * random.NextSingle();
                var start = random.NextSingle();
                for (var p = 0; p < MapPalette.PuffsPerChimney; p++)
                    puffs.Add(new Color(chimney.X, chimney.Y, (start + (float)p / MapPalette.PuffsPerChimney) % 1f, MapPalette.PuffSize));
            }
        }
        if (puffs.Count == 0)
            return;

        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseCustomData = true,
            Mesh = MapSprites.Quad,
            InstanceCount = puffs.Count,
        };
        for (var i = 0; i < puffs.Count; i++)
        {
            // Pas de déplacement par bouffée : le shader place tout à partir des données de départ.
            multimesh.SetInstanceTransform2D(i, Transform2D.Identity);
            multimesh.SetInstanceCustomData(i, puffs[i]);
        }
        _smoke = new MultiMeshInstance2D
        {
            Multimesh = multimesh,
            Texture = texture,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            Material = MapShaders.Material("fumee"),
            // La fumée sous les nuages : dessinée avant le rectangle des nuages (le dessin du calque).
            ShowBehindParent = true,
        };
        _node.AddChild(_smoke);
    }
}
