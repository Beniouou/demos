using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Map;
using Demos.Game.Map.Layers;
using Godot;

namespace Demos.Game.Map;

/// <summary>
/// La carte à l'écran. Elle garde la caméra (déplacement et zoom), la souris (glisser, molette, clic), le survol
/// (cartouche, nom du bâtiment sous la souris) et le mode placement (MapCanvas.Placement.cs). Ce qu'elle montre est
/// dessiné par les calques (dossier Layers/), rendus dans une texture par <see cref="MapLayerStack"/> seulement quand
/// l'un d'eux doit changer ; un mouvement de souris ne redessine que ce qui la suit.
/// </summary>
public partial class MapCanvas : Control
{
    public TopographicMap Map { get; private set; } = null!;
    public InfluenceField Field { get; private set; } = null!;
    public List<CommuneSite> Communes { get; } = [];
    /// <summary>Les filtres d'information cochés, par-dessus la vue de base Monde (aucun au départ).</summary>
    public MapFilters Filters { get; private set; } = MapFilters.None;
    public VectorMapDefinition? VectorMap => _data.VectorMap;
    public GameView? GameView => _data.View;

    private readonly MapData _data = new();
    private readonly MapCamera _camera;
    private readonly MapGeometry _geometry;
    private readonly MapDrawContext _overlay;   // ce qui suit la souris, dessiné sur la carte elle-même
    private MapLayerStack? _layers;             // créée à l'entrée dans l'arbre (il faut la fenêtre)
    private TextureRect? _sceneView;            // la scène (eau, fond fixe, fumées, nuages), avec l'effet maquette
    private TextureRect? _labelsView;           // les étiquettes, par-dessus, hors de l'effet maquette

    /// <summary>Une image de la pile des calques, à la taille de la carte, derrière ce que la carte dessine elle-même.</summary>
    private TextureRect LayerView(Texture2D texture)
    {
        var view = new TextureRect
        {
            Texture = texture,
            ShowBehindParent = true,
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        AddChild(view);
        view.SetAnchorsPreset(LayoutPreset.FullRect);
        return view;
    }
    private bool _isDragging;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartOffset;
    private Vector2? _hoveredWorldPos;
    private string? _hoveredInfo;

    /// <summary>
    /// Un clic (sans glisser) sur une commune ou dans l'enveloppe d'une préfecture : l'identifiant de la structure, pour
    /// ouvrir sa fiche.
    /// </summary>
    public event Action<string>? StructureClicked;

    // Au-delà de ce déplacement (en pixels) entre l'appui et le relâchement, c'est un glisser, pas un clic.
    private const float ClickTolerance = 5f;

    // Rayon, en pixels à l'écran, autour du centre d'une commune où un clic la choisit.
    private const float CommuneClickRadius = 12f;

    public Vector2? HoveredWorldPos => _hoveredWorldPos;
    public MapCoordinates? HoveredCell => _hoveredWorldPos.HasValue
        ? new MapCoordinates((int)(_hoveredWorldPos.Value.X / 16.6), (int)(_hoveredWorldPos.Value.Y / 16.0))
        : null;

    /// <summary>Les calques de la carte, du bas vers le haut (le dernier : les étiquettes).</summary>
    public IReadOnlyList<MapLayer> Layers => _layers?.Layers ?? [];

    // La caméra et les calculs existent dès la création : la carte reçoit la partie (Init) avant d'entrer dans l'arbre.
    public MapCanvas()
    {
        _camera = new MapCamera(() => Size);
        _geometry = new MapGeometry(_camera);
        _overlay = new MapDrawContext(this, _camera, _geometry, _data, () => Filters);
    }

    /// <summary>Quelque chose a changé : les calques qui en dépendent sont redessinés (une fois la carte dans l'arbre).</summary>
    private void Redraw(MapChange change)
    {
        if (_layers is null)
            QueueRedraw();
        else
            _layers.Redraw(change);
    }

    public void AddCommune(string communeId, string name, MapCoordinates? coord = null)
    {
        var targetCoord = coord ?? (Communes.Count == 1 ? new MapCoordinates(36, 16) : new MapCoordinates(14, 28));
        Communes.Add(new CommuneSite(communeId, name, targetCoord, Population: 250, HasTownHall: true));
        RecalculateInfluence();
    }

    public override void _Ready()
    {
        ClipContents = true;
        CustomMinimumSize = new Vector2(400, 300);
        FocusMode = FocusModeEnum.All;

        // La carte et le relief d'abord : les calques en ont besoin pour créer leurs nœuds.
        _data.LoadVectorMap();

        _layers = new MapLayerStack(this, _camera, _geometry, _data, () => Filters);
        // La scène puis les étiquettes, derrière ce que la carte dessine elle-même (ce qui suit la souris).
        _sceneView = LayerView(_layers.SceneTexture);
        _sceneView.Material = MapShaders.Material("maquette");
        _labelsView = LayerView(_layers.LabelsTexture);
        UpdateModelEffect();
        Resized += () => Redraw(MapChange.Camera);
        Redraw(MapChange.Camera);

        Map = PrototypeMap.CreateHandcrafted();
        InitDefaultCommunes();
        RecalculateInfluence();

        // Centrer la vue sur le chef-lieu initial
        if (_data.VectorMap is not null)
            CenterOnWorld(new Vector2((float)_data.VectorMap.DefaultCommune.Position.X, (float)_data.VectorMap.DefaultCommune.Position.Y));
        else
            CenterOnWorld(new Vector2(470, 310));
    }

    public void InitDefaultCommunes()
    {
        Communes.Clear();
        Communes.Add(new CommuneSite(
            "commune-1",
            "Première commune",
            PrototypeMap.DefaultCommunePosition,
            Population: 5000,
            HasTownHall: true,
            HasPrefectureSeat: false));
    }

    public void RecalculateInfluence()
    {
        Field = InfluenceCalculator.Compute(Map, Communes);
        Redraw(MapChange.Game);
    }

    public void Init(GameView view)
    {
        _data.SetView(view);
        Redraw(MapChange.Game);
    }

    /// <summary>Suit la partie : les calques qui la montrent ne sont redessinés que si ce qu'ils en montrent a changé.</summary>
    public void Refresh(GameView view)
    {
        if (_data.Update(view))
            Redraw(MapChange.Game);
    }

    /// <summary>Coche ou décoche un filtre d'information ; les calques qui en dépendent sont redessinés.</summary>
    public void SetFilter(MapFilters filter, bool on)
    {
        var filters = on ? Filters | filter : Filters & ~filter;
        if (filters != Filters)
        {
            Filters = filters;
            Redraw(MapChange.Filters);
            UpdateModelEffect();
        }
    }

    public bool HasFilter(MapFilters filter) => (Filters & filter) != 0;

    /// <summary>L'effet maquette (flou en haut et en bas, couleurs plus vives) seulement dans la vue Monde seule.</summary>
    private void UpdateModelEffect()
    {
        if (_sceneView?.Material is ShaderMaterial material)
            material.SetShaderParameter("strength", Filters == MapFilters.None ? 1.0f : 0.0f);
    }

    public void CenterOnCapital()
    {
        var communes = _data.Communes();
        if (communes.Count > 0)
        {
            CenterOnWorld(communes[0].WorldPos);
        }
        else if (_data.VectorMap?.DefaultCommune is not null)
        {
            CenterOnWorld(new Vector2((float)_data.VectorMap.DefaultCommune.Position.X, (float)_data.VectorMap.DefaultCommune.Position.Y));
        }
        else
        {
            CenterOnWorld(new Vector2(470, 310));
        }
    }

    public void CenterOn(MapCoordinates coord)
    {
        var mapWidth = (float)(_data.VectorMap?.Width ?? 1000.0);
        var mapHeight = (float)(_data.VectorMap?.Height ?? 640.0);
        var worldX = (float)coord.X * (mapWidth / Map.Width);
        var worldY = (float)coord.Y * (mapHeight / Map.Height);
        CenterOnWorld(new Vector2(worldX, worldY));
    }

    public void CenterOnWorld(Vector2 worldPos)
    {
        _camera.CenterOn(worldPos);
        Redraw(MapChange.Camera);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
            {
                ApplyZoom(1.15f, mb.Position);
                AcceptEvent();
                return;
            }
            if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
            {
                ApplyZoom(1.0f / 1.15f, mb.Position);
                AcceptEvent();
                return;
            }

            if (mb.ButtonIndex is MouseButton.Right or MouseButton.Middle or MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _isDragging = true;
                    _dragStartMouse = mb.Position;
                    _dragStartOffset = _camera.PanOffset;
                }
                else
                {
                    _isDragging = false;
                    // Un clic gauche sans glisser pose le bâtiment en mode placement, sinon choisit la commune ou la
                    // préfecture sous la souris.
                    if (mb.ButtonIndex == MouseButton.Left && mb.Position.DistanceTo(_dragStartMouse) <= ClickTolerance
                        && !TryPlaceAt(mb.Position) && StructureAt(mb.Position) is { } structureId)
                        StructureClicked?.Invoke(structureId);
                }
                AcceptEvent();
                return;
            }
        }

        if (@event is InputEventMouseMotion mm)
        {
            if (_isDragging)
            {
                _camera.PanOffset = _dragStartOffset + (mm.Position - _dragStartMouse);
                Redraw(MapChange.Camera);
            }

            UpdateHoveredInfo(mm.Position);
            // En mode placement, le curseur vert ou rouge suit la souris.
            if (IsPlacing)
                QueueRedraw();
        }
    }

    /// <summary>
    /// La structure sous un point de l'écran : la commune la plus proche dans un petit rayon, sinon la préfecture dont
    /// l'enveloppe contient le point (la plus petite, si plusieurs s'emboîtent), sinon rien.
    /// </summary>
    private string? StructureAt(Vector2 screenPos)
    {
        var commune = _data.Communes()
            .Select(c => (c.Id, Distance: _camera.ToScreen(c.WorldPos).DistanceTo(screenPos)))
            .Where(c => c.Distance <= CommuneClickRadius * Math.Max(_camera.Zoom, 1f))
            .OrderBy(c => c.Distance)
            .FirstOrDefault();
        if (commune.Id is not null)
            return commune.Id;

        var worldPos = _camera.ToWorld(screenPos);
        return _data.View?.Prefectures()
            .Where(p => _data.PrefecturePolygons(p).Any(polygon => Geometry2D.IsPointInPolygon(worldPos, polygon)))
            .OrderBy(p => p.MemberIds.Count)
            .FirstOrDefault()?.Id;
    }

    private void ApplyZoom(float factor, Vector2 pivot)
    {
        if (_camera.ZoomAt(factor, pivot))
            Redraw(MapChange.Camera);
    }

    private void UpdateHoveredInfo(Vector2 screenPos)
    {
        var worldPos = _camera.ToWorld(screenPos);
        _hoveredWorldPos = worldPos;

        var mapWidth = (float)(_data.VectorMap?.Width ?? 1000.0);
        var mapHeight = (float)(_data.VectorMap?.Height ?? 640.0);
        if (worldPos.X >= 0 && worldPos.X <= mapWidth && worldPos.Y >= 0 && worldPos.Y <= mapHeight)
        {
            var cellX = Math.Clamp((int)(worldPos.X / 16.6), 0, Map.Width - 1);
            var cellY = Math.Clamp((int)(worldPos.Y / 16.0), 0, Map.Height - 1);
            var coord = new MapCoordinates(cellX, cellY);

            var cell = Map[coord];
            var inf = Field[coord];

            var altM = (int)(cell.Elevation * 1200);
            var terrainName = cell.Terrain switch
            {
                TerrainType.Water => "Étendue maritime (Baie)",
                TerrainType.Plains => cell.HasRiver ? "Vallée alluviale de la Sereine" : "Plaine arable",
                TerrainType.Forest => "Bois des Chênes (forêt)",
                TerrainType.Hills => "Collines de piémont",
                TerrainType.Mountains => "Massif rocheux du Nord",
                _ => "Terre",
            };

            var res = cell.NaturalResource is not null ? $" · Gisement : {cell.NaturalResource.ToUpperInvariant()}" : "";
            var jur = inf.IsControlled ? " · Sous juridiction de Première commune" : " · Terres inexplorées (Terra Incognita)";

            _hoveredInfo = $"Arpentage IGN : X {(int)worldPos.X} · Y {(int)worldPos.Y} | {terrainName} (Alt. {altM} m){res}{jur}";
            QueueRedraw();
        }
        else if (_hoveredInfo is not null)
        {
            _hoveredInfo = null;
            QueueRedraw();
        }
    }

    /// <summary>
    /// La carte elle-même ne dessine que les calques déjà rendus (une texture, voir <see cref="MapLayerStack"/>) et,
    /// par-dessus, ce qui suit la souris : un mouvement de souris ne redessine pas les calques.
    /// </summary>
    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;

        // Le nom du bâtiment d'extraction sous la souris (MapCanvas.Placement.cs)
        DrawHoveredExtractionName(font);

        // Barre d'échelle (MapCanvas.Scale.cs)
        DrawScaleBar(font);

        // Réticule de survol et cartouche topographique
        DrawHoverCartouche(font);

        // Mode placement d'un bâtiment d'extraction (MapCanvas.Placement.cs)
        DrawPlacement(font);
    }

    private void DrawHoverCartouche(Font font)
    {
        if (!string.IsNullOrWhiteSpace(_hoveredInfo))
        {
            var cartoucheRect = new Rect2(12, Size.Y - 36, Size.X - 24, 26);
            DrawRect(cartoucheRect, MapPalette.LabelBackground);
            DrawRect(cartoucheRect, MapPalette.LabelBorder, filled: false, width: 1.0f);
            DrawString(font, new Vector2(24, Size.Y - 18), _hoveredInfo, HorizontalAlignment.Left, -1, 12, MapPalette.InkText);
        }
    }
}
