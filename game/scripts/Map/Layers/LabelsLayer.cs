using System.Collections.Generic;
using System.Linq;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Les étiquettes : les écritures de tous les calques (noms, cotes, badges, cartouches), dessinées au-dessus de tout,
/// dans l'ordre des calques. Chaque calque garde la logique de ses écritures (<see cref="MapLayer.DrawLabels"/>).
/// </summary>
public sealed class LabelsLayer(IReadOnlyList<MapLayer> layers) : MapLayer
{
    public override string Name => "Étiquettes";
    public override string Content => "Les écritures de tous les calques, au-dessus de tout";

    // Les écritures suivent ce qui oblige n'importe quel calque à se redessiner.
    public override MapChange RedrawOn { get; } = layers.Aggregate(MapChange.None, (all, layer) => all | layer.RedrawOn);

    public override void Draw(MapDrawContext c)
    {
        foreach (var layer in layers)
            layer.DrawLabels(c);
    }
}
