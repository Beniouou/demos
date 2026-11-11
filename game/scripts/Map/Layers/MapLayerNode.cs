using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Le nœud de Godot d'un calque : il dessine le calque quand la pile le lui demande (<see cref="MapLayer.Draw"/>), et
/// porte ses nœuds propres (shader, MultiMesh). Chaque calque a le sien : un calque qui ne change pas n'est pas redessiné.
/// </summary>
public partial class MapLayerNode : Node2D
{
    public MapLayer Layer { get; private set; } = null!;
    public MapDrawContext Context { get; private set; } = null!;

    public void Init(MapLayer layer, MapDrawContext context)
    {
        Layer = layer;
        Context = context;
        Name = layer.GetType().Name;
    }

    public override void _Draw() => Layer.Draw(Context);
}
