using System;
using System.Collections.Generic;
using Demos.Application;
using Godot;

namespace Demos.Game.Map;

/// <summary>
/// Les deux calques de la carte, pour la vitesse (rien ne change à l'image) :
/// - le fond (mer, forêts, relief, fleuves, frontières, communes, bâtiments) est dessiné dans une SubViewport, donc dans
///   une texture, seulement quand il change : déplacement, zoom, vue, taille, ou ce qu'il montre de la partie ;
/// - la carte elle-même affiche cette texture, puis ce qui suit la souris (cartouche, nom survolé, mode placement),
///   redessiné seul à chaque mouvement.
/// Les dessins passent par <see cref="_pen"/>, l'élément en train d'être dessiné (le calque du fond ou la carte).
/// </summary>
public partial class MapCanvas
{
    private SubViewport _backgroundViewport = null!;
    private MapBackgroundLayer _backgroundLayer = null!;
    private CanvasItem _pen = null!;
    private int _backgroundStateKey;

    /// <summary>Crée la SubViewport du fond, aux réglages de rendu de la fenêtre, et la suit quand la carte change de taille.</summary>
    private void CreateLayers()
    {
        var window = GetViewport();
        _backgroundViewport = new SubViewport
        {
            Disable3D = true,
            TransparentBg = false,
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
        AddChild(_backgroundViewport);
        _backgroundLayer = new MapBackgroundLayer();
        _backgroundLayer.Drawing += DrawBackground;
        _backgroundViewport.AddChild(_backgroundLayer);

        Resized += RedrawBackground;
        RedrawBackground();
    }

    /// <summary>
    /// Le fond a changé : il sera redessiné et rendu une fois dans sa texture avant l'image (et la carte avec lui, pour
    /// ce qui suit la souris).
    /// </summary>
    private void RedrawBackground()
    {
        if (_backgroundViewport is not null)
        {
            _backgroundViewport.Size = new Vector2I(Math.Max(1, Mathf.CeilToInt(Size.X)), Math.Max(1, Mathf.CeilToInt(Size.Y)));
            _backgroundLayer.QueueRedraw();
            _backgroundViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        }
        QueueRedraw();
    }

    /// <summary>
    /// Une empreinte de ce que le fond montre de la partie : communes (nom, habitants affichés), préfectures, capitale,
    /// bâtiments posés et chantiers. Si elle change, le fond est redessiné.
    /// </summary>
    private static int BackgroundStateKey(GameView view, IReadOnlyList<CommuneView> communes, IReadOnlyList<PrefectureView> prefectures)
    {
        var key = new HashCode();
        key.Add(view.CapitalId);
        foreach (var commune in communes)
        {
            key.Add(commune.Id);
            key.Add(commune.Name);
            key.Add(commune.Residents.ToString("N0"));
            key.Add(view.CommunePosition(commune.Id));
        }
        foreach (var prefecture in prefectures)
        {
            key.Add(prefecture.Id);
            key.Add(prefecture.Name);
            key.Add(prefecture.SeatId);
            foreach (var member in prefecture.MemberIds)
                key.Add(member);
        }
        foreach (var (id, position) in view.BuildingPositions())
        {
            key.Add(id);
            key.Add(position);
        }
        foreach (var site in view.ConstructionSites())
            key.Add(site.BuildingId);
        // Les bâtiments d'extraction : nom, commune, zone (sa couleur), chantier ou non.
        if (view.BuildingPositions().Count > 0)
            foreach (var (id, info) in ExtractionBuildingInfos(view))
            {
                key.Add(id);
                key.Add(info);
            }
        return key.ToHashCode();
    }
}
