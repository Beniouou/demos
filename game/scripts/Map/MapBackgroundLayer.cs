using Godot;

namespace Demos.Game.Map;

/// <summary>
/// Le calque du fond de la carte, dans la SubViewport de <see cref="MapCanvas"/> : il appelle le dessin du fond quand la
/// carte le lui demande, et le résultat reste dans une texture jusqu'au prochain changement.
/// </summary>
public partial class MapBackgroundLayer : Node2D
{
    public event System.Action? Drawing;

    public override void _Draw() => Drawing?.Invoke();
}
