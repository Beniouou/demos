using System;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// La caméra de la carte : le déplacement (en pixels) et le zoom. Un point de la carte (en unités de carte) est à l'écran en
/// « déplacement + point × zoom ». La taille est celle de la carte à l'écran.
/// </summary>
public sealed class MapCamera(Func<Vector2> size)
{
    public const float MinZoom = 0.4f;
    public const float MaxZoom = 8.75f;

    public Vector2 PanOffset { get; set; }
    public float Zoom { get; private set; } = 1.0f;
    public Vector2 Size => size();

    public Vector2 ToScreen(Vector2 worldPos) => PanOffset + worldPos * Zoom;

    public Vector2 ToWorld(Vector2 screenPos) => (screenPos - PanOffset) / Zoom;

    /// <summary>Met ce point de la carte au centre de l'écran.</summary>
    public void CenterOn(Vector2 worldPos) => PanOffset = Size * 0.5f - worldPos * Zoom;

    /// <summary>Zoome autour d'un point de l'écran, entre <see cref="MinZoom"/> et <see cref="MaxZoom"/>. Vrai si le zoom a changé.</summary>
    public bool ZoomAt(float factor, Vector2 pivot)
    {
        var oldZoom = Zoom;
        var newZoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - oldZoom) < 0.001f)
            return false;

        PanOffset = pivot - (pivot - PanOffset) * (newZoom / oldZoom);
        Zoom = newZoom;
        return true;
    }
}
