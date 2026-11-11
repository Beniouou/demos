using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// L'eau : la mer et les lacs, teintés selon leur profondeur (shader <c>eau.gdshader</c>), sous le terrain, dans la
/// scène animée (rendue à chaque image). Sans relief (images absentes), les plans d'eau de la carte vectorielle.
/// </summary>
public sealed class WaterLayer : MapLayer
{
    private ShaderMaterial? _material;

    public override string Name => "Eau";
    public override string Content => "La mer et les lacs, teintés selon leur profondeur";
    public override MapChange RedrawOn => MapChange.Camera;
    public override MapLayerPlace Place => MapLayerPlace.AnimatedBelow;

    public override void CreateNodes(Node2D node, MapDrawContext c)
    {
        if (c.Data.Relief is null)
            return;
        _material = MapShaders.Material("eau");
        node.Material = _material;
    }

    public override void UpdateNodes(MapDrawContext c)
    {
        if (_material is not null)
            MapShaders.SetView(_material, c);
    }

    public override void Draw(MapDrawContext c)
    {
        c.Pen.DrawRect(new Rect2(Vector2.Zero, c.Size), _material is null ? MapPalette.Parchment : Colors.White);
        if (_material is not null || c.VectorMap?.WaterBodies is not { Count: > 0 } waters)
            return;

        foreach (var body in waters)
        {
            if (body.IsErroneousGlobalSea(c.VectorMap.Width, c.VectorMap.Height))
                continue;
            var shape = c.Scaled(body.Boundary);
            if (shape.Points.Length >= 3 && c.IsOnScreen(shape, 4f))
            {
                var color = body.Kind is "Island" or "Land" ? MapPalette.Parchment : MapPalette.Water;
                c.FillTriangles(c.Triangles(shape), color);
                c.Pen.DrawPolyline(c.ToScreen(shape), MapPalette.WaterShallow, 2.0f, antialiased: true);
            }
        }
    }
}
