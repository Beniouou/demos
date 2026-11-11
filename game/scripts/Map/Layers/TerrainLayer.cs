using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Le terrain : colore la terre selon l'altitude, avec les ombres du relief (shader <c>terrain.gdshader</c>, à partir
/// des images calculées par l'outil de carte). La mer reste transparente : l'eau animée est dessous. Sans relief
/// (images absentes), rien : le calque de l'eau peint alors le papier vélin.
/// </summary>
public sealed class TerrainLayer : MapLayer
{
    private ShaderMaterial? _material;

    public override string Name => "Terrain";
    public override string Content => "La terre, colorée selon l'altitude, avec les ombres du relief";
    public override MapChange RedrawOn => MapChange.Camera;

    public override void CreateNodes(Node2D node, MapDrawContext c)
    {
        if (c.Data.Relief is null)
            return;
        _material = MapShaders.Material("terrain");
        node.Material = _material;
    }

    public override void UpdateNodes(MapDrawContext c)
    {
        if (_material is not null)
            MapShaders.SetView(_material, c);
    }

    // Sans relief, le calque de l'eau peint le papier et les plans d'eau : le terrain ne dessine rien.
    public override void Draw(MapDrawContext c)
    {
        if (_material is not null)
            c.Pen.DrawRect(new Rect2(Vector2.Zero, c.Size), Colors.White);
    }
}
