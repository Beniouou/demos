using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// La pile des calques de la carte et leur rendu, en trois étages :
/// - le <b>fond fixe</b> (terrain, biomes, relief, fleuves, frontières, communes, bâtiments) : une SubViewport refaite
///   seulement quand l'un de ses calques doit changer (<see cref="Redraw"/>), et seulement ce calque-là ;
/// - la <b>scène</b> : une SubViewport refaite à chaque image par la carte graphique, qui pose l'eau animée, puis le fond
///   fixe (sa texture), puis les fumées et les nuages. Le fond fixe est rangé dans la scène pour être rendu avant elle ;
/// - les <b>étiquettes</b> de tous les calques : une SubViewport à part, refaite quand une étiquette change, posée par-dessus
///   la scène et hors de l'effet maquette.
/// La carte affiche la scène (avec l'effet maquette) puis les étiquettes, derrière ce qu'elle dessine elle-même (ce qui
/// suit la souris).
/// </summary>
public sealed class MapLayerStack
{
    private readonly Control _host;
    private readonly SubViewport _scene;
    private readonly SubViewport _fixed;
    private readonly SubViewport _labels;
    private readonly List<MapLayerNode> _nodes = [];

    /// <summary>Les calques, du bas vers le haut ; le dernier est celui des étiquettes.</summary>
    public IReadOnlyList<MapLayer> Layers { get; }

    /// <summary>L'image de la scène (eau, fond fixe, fumées, nuages), sans les étiquettes.</summary>
    public Texture2D SceneTexture => _scene.GetTexture();

    /// <summary>L'image des étiquettes, sur fond transparent.</summary>
    public Texture2D LabelsTexture => _labels.GetTexture();

    public MapLayerStack(Control host, MapCamera camera, MapGeometry geometry, MapData data, Func<MapFilters> filters)
    {
        _host = host;
        MapLayer[] drawn =
        [
            new TerrainLayer(),
            new WaterLayer(),
            new BiomesLayer(),
            new ReliefLayer(),
            new RiversLayer(),
            new TransportLayer(),
            new PoliticsLayer(),
            new DepositsLayer(),
            new CommunesLayer(),
            new ProductionLayer(),
            new AtmosphereLayer(),
        ];
        var labels = new LabelsLayer(drawn);
        Layers = [.. drawn, labels];

        _scene = NewViewport(host, transparent: false);
        _scene.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _fixed = NewViewport(host, transparent: true);
        _labels = NewViewport(host, transparent: true);
        host.AddChild(_scene);
        host.AddChild(_labels);

        MapLayerNode Node(MapLayer layer, Node parent)
        {
            var node = new MapLayerNode();
            var context = new MapDrawContext(node, camera, geometry, data, filters);
            node.Init(layer, context);
            parent.AddChild(node);
            layer.CreateNodes(node, context);
            _nodes.Add(node);
            return node;
        }

        // La scène : l'eau animée, puis le fond fixe (et la SubViewport qui le rend, enfant de la scène pour être rendue
        // avant elle), puis les fumées et les nuages.
        foreach (var layer in drawn.Where(l => l.Place == MapLayerPlace.AnimatedBelow))
            Node(layer, _scene);
        _scene.AddChild(_fixed);
        _scene.AddChild(new Sprite2D { Texture = _fixed.GetTexture(), Centered = false, Name = "FondFixe" });
        foreach (var layer in drawn.Where(l => l.Place == MapLayerPlace.AnimatedAbove))
            Node(layer, _scene);

        foreach (var layer in drawn.Where(l => l.Place == MapLayerPlace.Fixed))
            Node(layer, _fixed);
        Node(labels, _labels);
    }

    private static SubViewport NewViewport(Control host, bool transparent)
    {
        // Aux réglages de rendu de la fenêtre.
        var window = host.GetViewport();
        return new SubViewport
        {
            Disable3D = true,
            TransparentBg = transparent,
            GuiDisableInput = true,
            RenderTargetClearMode = SubViewport.ClearMode.Always,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            Msaa2D = window.Msaa2D,
            UseHdr2D = window.UseHdr2D,
            CanvasItemDefaultTextureFilter = window.CanvasItemDefaultTextureFilter,
            CanvasItemDefaultTextureRepeat = window.CanvasItemDefaultTextureRepeat,
            Snap2DTransformsToPixel = window.Snap2DTransformsToPixel,
            Snap2DVerticesToPixel = window.Snap2DVerticesToPixel,
        };
    }

    /// <summary>
    /// Quelque chose a changé : chaque calque qui en dépend est mis à jour et redessiné, et l'étage qui le porte est rendu
    /// une fois (la scène, elle, l'est à chaque image). La carte elle-même est toujours redessinée (ce qui suit la souris).
    /// </summary>
    public void Redraw(MapChange change)
    {
        var size = new Vector2I(Math.Max(1, Mathf.CeilToInt(_host.Size.X)), Math.Max(1, Mathf.CeilToInt(_host.Size.Y)));
        foreach (var viewport in new[] { _scene, _fixed, _labels })
            viewport.Size = size;

        foreach (var node in _nodes)
        {
            if ((node.Layer.RedrawOn & change) == 0)
                continue;
            node.Layer.UpdateNodes(node.Context);
            node.QueueRedraw();
            var viewport = node.GetViewport() as SubViewport;
            if (viewport is not null && viewport != _scene)
                viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        }
        _host.QueueRedraw();
    }
}
