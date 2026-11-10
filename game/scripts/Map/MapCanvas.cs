using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Demos.Application;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map;

/// <summary>
/// Rendu cartographique vectoriel continu : style carte IGN et cadastre urbain architectural.
/// Zéro case, zéro quadrillage. Dessin à l'encre, courbes de niveau continues (Marching Squares),
/// estompage de relief, fleuve sinueux et plan cadastral vivant des bâtiments construits.
/// </summary>
public partial class MapCanvas : Control
{
    public TopographicMap Map { get; private set; } = null!;
    public InfluenceField Field { get; private set; } = null!;
    public List<CommuneSite> Communes { get; } = [];
    public MapViewMode ViewMode { get; set; } = MapViewMode.Geography;
    public VectorMapDefinition? VectorMap => _vectorMap;
    public GameView? GameView => _gameView;

    private VectorMapDefinition? _vectorMap;
    private GameView? _gameView;
    private readonly Dictionary<string, List<Vector2[]>> _prefecturePolygonsCache = [];
    private int _lastCommunesCount = -1;
    private int _lastPrefecturesCount = -1;
    private Vector2 _panOffset;
    private float _zoom = 1.0f;
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

        Map = PrototypeMap.CreateHandcrafted();
        InitDefaultCommunes();
        RecalculateInfluence();

        LoadVectorMap();

        // Centrer la vue sur le chef-lieu initial
        if (_vectorMap is not null)
            CenterOnWorld(new Vector2((float)_vectorMap.DefaultCommune.Position.X, (float)_vectorMap.DefaultCommune.Position.Y));
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
        QueueRedraw();
    }

    public void Init(GameView view)
    {
        _gameView = view;
        _prefecturePolygonsCache.Clear();
        QueueRedraw();
    }

    public void Refresh(GameView view)
    {
        _gameView = view;
        var comms = view.Communes();
        var prefs = view.Prefectures();
        if (comms.Count != _lastCommunesCount || prefs.Count != _lastPrefecturesCount)
        {
            _lastCommunesCount = comms.Count;
            _lastPrefecturesCount = prefs.Count;
            _prefecturePolygonsCache.Clear();
            QueueRedraw();
        }
    }

    public void SetViewMode(MapViewMode mode)
    {
        if (ViewMode != mode)
        {
            ViewMode = mode;
            QueueRedraw();
        }
    }

    public void CenterOnCapital()
    {
        var communes = GetActiveCommunesList();
        if (communes.Count > 0)
        {
            CenterOnWorld(communes[0].WorldPos);
        }
        else if (_vectorMap?.DefaultCommune is not null)
        {
            CenterOnWorld(new Vector2((float)_vectorMap.DefaultCommune.Position.X, (float)_vectorMap.DefaultCommune.Position.Y));
        }
        else
        {
            CenterOnWorld(new Vector2(470, 310));
        }
    }

    public void CenterOn(MapCoordinates coord)
    {
        var mapWidth = (float)(_vectorMap?.Width ?? 1000.0);
        var mapHeight = (float)(_vectorMap?.Height ?? 640.0);
        var worldX = (float)coord.X * (mapWidth / Map.Width);
        var worldY = (float)coord.Y * (mapHeight / Map.Height);
        CenterOnWorld(new Vector2(worldX, worldY));
    }

    public void CenterOnWorld(Vector2 worldPos)
    {
        var viewCenter = Size * 0.5f;
        _panOffset = viewCenter - worldPos * _zoom;
        QueueRedraw();
    }

    private void LoadVectorMap()
    {
        try
        {
            if (FileAccess.FileExists("res://data/carte.json"))
            {
                var json = FileAccess.GetFileAsString("res://data/carte.json");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                _vectorMap = JsonSerializer.Deserialize<VectorMapDefinition>(json, options);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Erreur lecture carte.json : {ex.Message}");
        }
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
                    _dragStartOffset = _panOffset;
                }
                else
                {
                    _isDragging = false;
                    // Un clic gauche sans glisser choisit la commune ou la préfecture sous la souris.
                    if (mb.ButtonIndex == MouseButton.Left && mb.Position.DistanceTo(_dragStartMouse) <= ClickTolerance
                        && StructureAt(mb.Position) is { } structureId)
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
                _panOffset = _dragStartOffset + (mm.Position - _dragStartMouse);
                QueueRedraw();
            }

            UpdateHoveredInfo(mm.Position);
        }
    }

    /// <summary>
    /// La structure sous un point de l'écran : la commune la plus proche dans un petit rayon, sinon la préfecture dont
    /// l'enveloppe contient le point (la plus petite, si plusieurs s'emboîtent), sinon rien.
    /// </summary>
    private string? StructureAt(Vector2 screenPos)
    {
        var commune = GetActiveCommunesList()
            .Select(c => (c.Id, Distance: ToScreen(c.WorldPos).DistanceTo(screenPos)))
            .Where(c => c.Distance <= CommuneClickRadius * Math.Max(_zoom, 1f))
            .OrderBy(c => c.Distance)
            .FirstOrDefault();
        if (commune.Id is not null)
            return commune.Id;

        var worldPos = (screenPos - _panOffset) / _zoom;
        return _gameView?.Prefectures()
            .Where(p => GetPrefecturePolygons(p).Any(polygon => Geometry2D.IsPointInPolygon(worldPos, polygon)))
            .OrderBy(p => p.MemberIds.Count)
            .FirstOrDefault()?.Id;
    }

    private void ApplyZoom(float factor, Vector2 pivot)
    {
        var oldZoom = _zoom;
        var newZoom = Math.Clamp(_zoom * factor, 0.4f, 3.5f);
        if (Math.Abs(newZoom - oldZoom) < 0.001f)
            return;

        _panOffset = pivot - (pivot - _panOffset) * (newZoom / oldZoom);
        _zoom = newZoom;
        QueueRedraw();
    }

    private void UpdateHoveredInfo(Vector2 screenPos)
    {
        var worldPos = (screenPos - _panOffset) / _zoom;
        _hoveredWorldPos = worldPos;

        var mapWidth = (float)(_vectorMap?.Width ?? 1000.0);
        var mapHeight = (float)(_vectorMap?.Height ?? 640.0);
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

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;

        // 1. Fond papier vélin IGN doux
        DrawRect(new Rect2(Vector2.Zero, Size), MapPalette.Parchment);

        // 2. Mer et littoral (Baie méridionale pastel)
        DrawSea();

        // 3. Forêts et biomes naturels (contours organiques pastel)
        DrawForests();

        // 4. Estompage topographique (réservé à l'ancienne maquette historique 1000x640 sans courbes réelles)
        if (_vectorMap is null || (_vectorMap.Width <= 1100.0 && _vectorMap.Height <= 750.0 && _vectorMap.Contours.Count == 0))
        {
            DrawHillshading();
        }

        // 5. Courbes de niveau IGN continues (calculées par Marching Squares)
        DrawContourLines(font);

        // 6. Points cotés d'altitude (croix IGN et cotes en mètres)
        DrawSpotElevations(font);

        // 7. Fleuves et réseau hydrographique
        DrawRiver(font);

        // 8. Voies navigables et réseau d'échanges marchands (mode Transit)
        if (ViewMode == MapViewMode.Transit)
        {
            DrawTransitNetwork(font);
        }

        // 9. Préfectures, frontières de juridiction et liaisons administratives
        DrawPrefecturesAndJurisdictions(font);

        // 10. Frontière de souveraineté et voile Terra Incognita (ancienne maquette démo)
        DrawSovereigntyAndWildlands();

        // 11. Gisements et unités de production géoréférencées (Carrières, Mines, Terres arables)
        DrawExteriorProduction(font);

        // 12. Les Communes, Chefs-lieux et Capitale (cadastre architectural ou symboles territoriaux)
        DrawCadastralTown(font);

        // 13. Réticule de survol et cartouche topographique
        DrawHoverCartouche(font);
    }

    private void DrawSea()
    {
        if (_vectorMap?.WaterBodies is not { Count: > 0 } waters)
            return;

        foreach (var body in waters)
        {
            if (body.IsErroneousGlobalSea(_vectorMap.Width, _vectorMap.Height))
                continue;

            var screenPoints = body.Boundary
                .Select(p => ToScreen(new Vector2((float)p.X, (float)p.Y)))
                .ToArray();

            if (screenPoints.Length >= 3)
            {
                var color = body.Kind is "Island" or "Land" ? MapPalette.Parchment : MapPalette.Water;
                SafeDrawColoredPolygon(screenPoints, color);
                DrawPolyline(screenPoints, MapPalette.WaterShallow, 2.0f, antialiased: true);
            }
        }
    }

    private void DrawForests()
    {
        var biomes = _vectorMap?.AllBiomes ?? _vectorMap?.Forests;
        if (biomes is not { Count: > 0 })
            return;

        var isGeopolitics = ViewMode == MapViewMode.Geopolitics;
        var isTransit = ViewMode == MapViewMode.Transit;

        foreach (var biome in biomes)
        {
            var screenPoints = biome.Boundary
                .Select(p => ToScreen(new Vector2((float)p.X, (float)p.Y)))
                .ToArray();

            if (screenPoints.Length >= 3)
            {
                var (fill, stroke) = biome.Kind.ToLowerInvariant() switch
                {
                    "foret" or "forest" => (MapPalette.Forest, MapPalette.ForestStroke),
                    "fertile" or "zone fertile" => (new Color("#EEE4B9"), new Color("#C8B982")),
                    "marais" => (new Color("#C3DACD"), new Color("#87AFA0")),
                    "plage" => (new Color("#F2E6BC"), new Color("#D7C391")),
                    "falaise" => (new Color("#C8CACD"), new Color("#787D82")),
                    "desert" => (new Color("#F5DEAA"), new Color("#D7AF73")),
                    "glacier" => (new Color("#E1F0F8"), new Color("#8CB4D2")),
                    _ => (MapPalette.Forest, MapPalette.ForestStroke)
                };

                if (isGeopolitics || isTransit)
                {
                    fill = new Color(fill.R, fill.G, fill.B, fill.A * 0.55f);
                    stroke = new Color(stroke.R, stroke.G, stroke.B, stroke.A * 0.55f);
                }

                if (biome.Holes is { Count: > 0 } holes)
                {
                    var pieces = new List<Vector2[]> { screenPoints };
                    foreach (var hole in holes)
                    {
                        if (hole.Count < 3) continue;
                        var holePoints = hole.Select(p => ToScreen(new Vector2((float)p.X, (float)p.Y))).ToArray();
                        var nextPieces = new List<Vector2[]>();
                        foreach (var piece in pieces)
                        {
                            var clipped = Geometry2D.ClipPolygons(piece, holePoints);
                            if (clipped != null && clipped.Count > 0)
                            {
                                foreach (var cp in clipped) nextPieces.Add(cp);
                            }
                            else
                            {
                                nextPieces.Add(piece);
                            }
                        }
                        pieces = nextPieces;
                    }

                    foreach (var piece in pieces)
                    {
                        if (piece.Length >= 3)
                        {
                            SafeDrawColoredPolygon(piece, fill);
                        }
                    }

                    DrawPolyline(screenPoints, stroke, 1.2f, antialiased: true);
                    foreach (var hole in holes)
                    {
                        if (hole.Count >= 3)
                        {
                            var holePoints = hole.Select(p => ToScreen(new Vector2((float)p.X, (float)p.Y))).ToArray();
                            DrawPolyline(holePoints, stroke, 1.2f, antialiased: true);
                        }
                    }
                }
                else
                {
                    SafeDrawColoredPolygon(screenPoints, fill);
                    DrawPolyline(screenPoints, stroke, 1.2f, antialiased: true);
                }

                if (biome.Kind is "foret" or "forest")
                {
                    // Petits bosquets dessinés uniquement à l'intérieur du massif forestier
                    var minX = screenPoints.Min(p => p.X);
                    var maxX = screenPoints.Max(p => p.X);
                    var minY = screenPoints.Min(p => p.Y);
                    var maxY = screenPoints.Max(p => p.Y);

                    var step = 32f * _zoom;
                    if (step < 14f) step = 14f;

                    var treeCount = 0;
                    for (var y = minY + step * 0.5f; y <= maxY; y += step)
                    {
                        for (var x = minX + step * 0.5f; x <= maxX; x += step)
                        {
                            var pt = new Vector2(x, y);
                            if (Geometry2D.IsPointInPolygon(pt, screenPoints) && !(biome.Holes?.Any(h => Geometry2D.IsPointInPolygon(pt, h.Select(p => ToScreen(new Vector2((float)p.X, (float)p.Y))).ToArray())) ?? false))
                            {
                                DrawTreeCluster(pt);
                                treeCount++;
                            }
                        }
                    }

                    if (treeCount == 0)
                    {
                        var center = screenPoints.Aggregate(Vector2.Zero, (acc, p) => acc + p) / screenPoints.Length;
                        if (Geometry2D.IsPointInPolygon(center, screenPoints) && !(biome.Holes?.Any(h => Geometry2D.IsPointInPolygon(center, h.Select(p => ToScreen(new Vector2((float)p.X, (float)p.Y))).ToArray())) ?? false))
                        {
                            DrawTreeCluster(center);
                        }
                    }
                }
            }
        }
    }

    private void DrawTreeCluster(Vector2 pos)
    {
        var r = 8f * Math.Max(0.6f, _zoom);
        DrawCircle(pos, r, MapPalette.ForestTrees);
        DrawCircle(pos + new Vector2(r * 0.7f, r * 0.3f), r * 0.8f, MapPalette.ForestTrees);
        DrawCircle(pos + new Vector2(-r * 0.7f, r * 0.3f), r * 0.75f, MapPalette.ForestTrees);
    }

    private void DrawHillshading()
    {
        // Masse d'ombrage continue au sud-est des crêtes nord-est
        var p1 = ToScreen(new Vector2(580, 120));
        var p2 = ToScreen(new Vector2(720, 70));
        var p3 = ToScreen(new Vector2(860, 100));
        var p4 = ToScreen(new Vector2(940, 160));
        var p5 = ToScreen(new Vector2(900, 240));
        var p6 = ToScreen(new Vector2(700, 200));

        DrawColoredPolygon([p1, p2, p3, p4, p5, p6], MapPalette.Hillshade);
    }

    private void DrawContourLines(Font font)
    {
        if (_vectorMap?.Contours is not { Count: > 0 } contours)
            return;

        var isGeopolitics = ViewMode == MapViewMode.Geopolitics;
        var isTransit = ViewMode == MapViewMode.Transit;

        foreach (var contour in contours)
        {
            if (contour.Points.Count < 2)
                continue;

            // En vue géopolitique, alléger les courbes de niveau pour privilégier la souveraineté
            if (isGeopolitics && !contour.IsIndex)
                continue;

            var screenPoints = contour.Points
                .Select(p => ToScreen(new Vector2((float)p.X, (float)p.Y)))
                .ToArray();

            var color = contour.IsIndex ? MapPalette.ContourIndex : MapPalette.ContourStandard;
            if (isGeopolitics || isTransit)
            {
                color = new Color(color.R, color.G, color.B, color.A * 0.45f);
            }

            var width = (contour.IsIndex ? 1.4f : 0.75f) * Math.Clamp(_zoom, 0.7f, 1.8f);

            DrawPolyline(screenPoints, color, width, antialiased: true);

            // Cotes altimétriques sur les courbes maîtresses (100, 200, 300, 400, 500)
            if (contour.IsIndex && _zoom >= 0.85f && screenPoints.Length >= 4 && !isGeopolitics)
            {
                var mid = screenPoints[screenPoints.Length / 2];
                var label = $"{contour.Altitude:F0}";
                DrawString(font, mid + new Vector2(0, -2), label, HorizontalAlignment.Left, -1, 10, color);
            }
        }
    }

    private void DrawSpotElevations(Font font)
    {
        if (_vectorMap?.SpotElevations is not { Count: > 0 } spots)
            return;

        foreach (var spot in spots)
        {
            var pos = ToScreen(new Vector2((float)spot.Position.X, (float)spot.Position.Y));
            const float crossSize = 4f;

            DrawLine(pos + new Vector2(-crossSize, 0), pos + new Vector2(crossSize, 0), MapPalette.SpotElevation, 1.2f);
            DrawLine(pos + new Vector2(0, -crossSize), pos + new Vector2(0, crossSize), MapPalette.SpotElevation, 1.2f);

            var cat = PlaceCategories.Get(spot.Category);
            var label = spot.ShowAltitude
                ? $"{cat.Icon} {spot.Name} ({spot.Altitude:F0} m)"
                : $"{cat.Icon} {spot.Name}";
            DrawString(font, pos + new Vector2(6, 4), label, HorizontalAlignment.Left, -1, 11, MapPalette.SpotElevation);
        }
    }

    private void DrawRiver(Font font)
    {
        if (_vectorMap == null)
            return;

        foreach (var river in _vectorMap.AllRivers)
        {
            if (river.Points.Count < 2) continue;

            var screenPoints = river.Points
                .Select(p => ToScreen(new Vector2((float)p.X, (float)p.Y)))
                .ToArray();

            var riverWidth = (float)river.Width * _zoom;
            DrawPolyline(screenPoints, MapPalette.River, riverWidth, antialiased: true);
            DrawPolyline(screenPoints, MapPalette.RiverCore, Math.Max(1.0f, riverWidth * 0.4f), antialiased: true);

            if (_zoom >= 0.9f && screenPoints.Length >= 4 && !string.IsNullOrWhiteSpace(river.Name))
            {
                var textPos = screenPoints[screenPoints.Length / 2];
                DrawString(font, textPos + new Vector2(10, -6), river.Name, HorizontalAlignment.Left, -1, 11, MapPalette.River);
            }
        }
    }

    private void DrawSovereigntyAndWildlands()
    {
        // Masquer le liseré de démo locale pour les cartes de pays/régions
        if (_vectorMap is not null && (_vectorMap.Width > 1100.0 || _vectorMap.Height > 750.0))
            return;

        // Liseré d'arpentage souple délimitant la souveraineté
        var poly = new[]
        {
            ToScreen(new Vector2(270, 160)),
            ToScreen(new Vector2(480, 120)),
            ToScreen(new Vector2(660, 170)),
            ToScreen(new Vector2(700, 440)),
            ToScreen(new Vector2(480, 510)),
            ToScreen(new Vector2(220, 430)),
            ToScreen(new Vector2(200, 230)),
        };

        DrawPolyline([.. poly, poly[0]], MapPalette.InfluenceBorder, 1.8f * _zoom, antialiased: true);
    }

    private void DrawExteriorProduction(Font font)
    {
        if (_vectorMap == null)
            return;

        var isNaturalResources = ViewMode == MapViewMode.NaturalResources;
        var isProduction = ViewMode == MapViewMode.Production;
        var s = _zoom;

        if (_vectorMap.AllResourceZones.Count > 0)
        {
            foreach (var zone in _vectorMap.AllResourceZones)
            {
                if (zone.Boundary.Count < 3) continue;
                var screenPoints = zone.Boundary
                    .Select(p => ToScreen(new Vector2((float)p.X, (float)p.Y)))
                    .ToArray();

                var (fill, stroke, icon) = zone.Kind.ToLowerInvariant() switch
                {
                    "pierre" => (new Color(0.55f, 0.62f, 0.68f, 0.35f), new Color(0.40f, 0.48f, 0.55f, 0.85f), "🪨"),
                    "charbon" => (new Color(0.20f, 0.20f, 0.22f, 0.40f), new Color(0.12f, 0.12f, 0.14f, 0.90f), "⛏️"),
                    "fer" => (new Color(0.72f, 0.36f, 0.22f, 0.35f), new Color(0.55f, 0.25f, 0.15f, 0.85f), "⛓️"),
                    "fertile" or "zone fertile" or "ble" or "lin" => (new Color(0.88f, 0.80f, 0.45f, 0.35f), new Color(0.70f, 0.60f, 0.28f, 0.85f), "🌾"),
                    _ => (new Color(0.6f, 0.6f, 0.6f, 0.35f), new Color(0.4f, 0.4f, 0.4f, 0.85f), "💎")
                };

                if (isNaturalResources)
                {
                    fill = new Color(fill.R, fill.G, fill.B, Math.Min(0.55f, fill.A * 1.5f));
                    stroke = new Color(stroke.R, stroke.G, stroke.B, 1.0f);
                }
                else if (ViewMode == MapViewMode.Geopolitics)
                {
                    fill = new Color(fill.R, fill.G, fill.B, fill.A * 0.4f);
                    stroke = new Color(stroke.R, stroke.G, stroke.B, stroke.A * 0.4f);
                }

                SafeDrawColoredPolygon(screenPoints, fill);
                DrawPolyline(screenPoints, stroke, (isNaturalResources ? 2.0f : 1.4f) * s, antialiased: true);

                if (s >= 0.6f || isNaturalResources)
                {
                    var center = screenPoints.Aggregate(Vector2.Zero, (acc, p) => acc + p) / screenPoints.Length;
                    var label = $"{icon} {zone.Name}";
                    var fontSize = (int)Math.Clamp(10 * s, 9, 13);
                    DrawString(font, center, label, HorizontalAlignment.Center, -1, fontSize, stroke);
                }
            }
            return;
        }

        if (_vectorMap.Resources is not { Count: > 0 } res)
            return;

        foreach (var r in res)
        {
            var pos = ToScreen(new Vector2((float)r.Position.X, (float)r.Position.Y));
            var (name, roofColor, isMine) = r.Resource.ToLowerInvariant() switch
            {
                "pierre" => ("Carrière de pierre", MapPalette.RoofSlate, false),
                "charbon" => ("Mine de charbon", new Color("#333333"), true),
                "fer" => ("Gisement de fer", new Color("#8B3A2A"), true),
                "fertile" or "ble" or "lin" => ("Zone fertile", MapPalette.RoofTile, false),
                _ => ("Site d'extraction", MapPalette.RoofTile, false),
            };

            DrawBuildingFootprint(pos, new Vector2(24, 16) * s, roofColor);

            if (s >= 0.85f || isNaturalResources || isProduction)
            {
                var label = $"{name} ({r.Resource})";
                DrawString(font, pos + new Vector2(-30, 22 * s), label, HorizontalAlignment.Center, -1, 10, MapPalette.InkText);
            }
        }
    }

    private void DrawCadastralTown(Font font)
    {
        if (_vectorMap is not null && (_vectorMap.Width > 1100.0 || _vectorMap.AllCommunes.Count > 1 || (_gameView is not null && _gameView.Communes().Count > 1)))
        {
            DrawCommunesNational(font);
            return;
        }

        DrawLegacyCadastralTown(font);
    }

    private void DrawCommunesNational(Font font)
    {
        var communes = GetActiveCommunesList();
        var prefectures = _gameView?.Prefectures();
        var seatIds = prefectures?.Where(p => p.SeatId is not null).Select(p => p.SeatId!).ToHashSet() ?? [];

        var s = _zoom;

        foreach (var (id, name, pos, pop) in communes)
        {
            var center = ToScreen(pos);
            var isCapital = id == _gameView?.CapitalId;
            var isPrefectureSeat = seatIds.Contains(id);

            if (isCapital)
            {
                // 1. Capitale nationale : Sceau or et carmin
                DrawCircle(center, 9.0f * s, MapPalette.BlasonGold);
                DrawCircle(center, 7.5f * s, MapPalette.BlasonRed);
                DrawCircle(center, 4.0f * s, MapPalette.BlasonGold);
                DrawCircle(center, 2.0f * s, Colors.White);

                if (s >= 0.5f)
                {
                    var popText = pop > 0 ? $" · {pop:N0} hab." : "";
                    var label = $"CAPITALE · {name.ToUpperInvariant()}{popText}";
                    DrawCommuneBadge(font, center, label, MapPalette.BlasonRed, MapPalette.BlasonGold);
                }
            }
            else if (isPrefectureSeat)
            {
                // 2. Chef-lieu de préfecture : Sceau civique pourpre
                DrawCircle(center, 8.0f * s, MapPalette.PrefectureSeat);
                DrawCircle(center, 6.0f * s, Colors.White);
                DrawCircle(center, 3.5f * s, MapPalette.PrefectureSeat);

                if (s >= 0.5f)
                {
                    var popText = pop > 0 ? $" · {pop:N0} hab." : "";
                    var label = $"CHEF-LIEU · {name}{popText}";
                    DrawCommuneBadge(font, center, label, MapPalette.PrefectureSeat, new Color("#EBDCE4"));
                }
            }
            else
            {
                // 3. Commune ordinaire : Disque bicolore sobre
                DrawCircle(center, 6.0f * s, new Color("#8B1E1E"));
                DrawCircle(center, 4.0f * s, new Color("#FFF9EE"));
                DrawCircle(center, 2.0f * s, new Color("#8B1E1E"));

                if (s >= 0.5f)
                {
                    var popText = pop > 0 ? $" ({pop:N0} hab.)" : "";
                    var label = $"{name}{popText}";
                    DrawCommuneBadge(font, center, label, MapPalette.InkText, MapPalette.LabelBackground);
                }
            }
        }
    }

    private void DrawPrefecturesAndJurisdictions(Font font)
    {
        var prefectures = _gameView?.Prefectures();
        var isGeopolitics = ViewMode == MapViewMode.Geopolitics;
        var s = _zoom;

        if (prefectures is { Count: > 0 } prefs)
        {
            for (var idx = 0; idx < prefs.Count; idx++)
            {
                var pref = prefs[idx];
                var washColor = MapPalette.PrefectureDepartmentWashes[idx % MapPalette.PrefectureDepartmentWashes.Length];
                var borderColor = MapPalette.PrefectureDepartmentBorders[idx % MapPalette.PrefectureDepartmentBorders.Length];

                if (!isGeopolitics)
                {
                    washColor = new Color(washColor.R, washColor.G, washColor.B, washColor.A * 0.45f);
                    borderColor = new Color(borderColor.R, borderColor.G, borderColor.B, borderColor.A * 0.45f);
                }

                var regions = GetPrefecturePolygons(pref);
                foreach (var poly in regions)
                {
                    var screenPts = poly.Select(ToScreen).ToArray();
                    SafeDrawColoredPolygon(screenPts, washColor);
                    DrawPolyline([.. screenPts, screenPts[0]], borderColor, (isGeopolitics ? 2.2f : 1.2f) * s, antialiased: true);
                }

                // Liaisons administratives pointillées entre le chef-lieu et chaque membre
                if (pref.SeatId is not null)
                {
                    var seatPos = GetCommuneWorldPosition(pref.SeatId);
                    if (seatPos.HasValue)
                    {
                        var screenSeat = ToScreen(seatPos.Value);
                        foreach (var memberId in pref.MemberIds)
                        {
                            if (memberId == pref.SeatId) continue;
                            var memberPos = GetCommuneWorldPosition(memberId);
                            if (memberPos.HasValue)
                            {
                                var screenMember = ToScreen(memberPos.Value);
                                DrawDashedLine(screenSeat, screenMember, MapPalette.AdministrativeLink, (isGeopolitics ? 2.0f : 1.2f) * s, dash: 6.0f * s);
                            }
                        }

                        // Cartouche de préfecture au-dessus du chef-lieu
                        if (isGeopolitics && s >= 0.55f)
                        {
                            DrawPrefectureHeader(font, screenSeat, pref.Name);
                        }
                    }
                }
            }
        }
        else
        {
            // Aucune préfecture formelle : tracer les bassins de juridiction directe des communes
            var communes = GetActiveCommunesList();
            foreach (var (_, _, pos, _) in communes)
            {
                var disk = CreateCirclePolygon(pos, 240.0f, 20);
                var screenPts = disk.Select(ToScreen).ToArray();
                var wash = isGeopolitics ? MapPalette.PrefectureWash : new Color(MapPalette.PrefectureWash.R, MapPalette.PrefectureWash.G, MapPalette.PrefectureWash.B, 0.04f);
                SafeDrawColoredPolygon(screenPts, wash);
                DrawPolyline([.. screenPts, screenPts[0]], MapPalette.InfluenceBorder, (isGeopolitics ? 1.8f : 1.0f) * s, antialiased: true);
            }
        }
    }

    private void DrawTransitNetwork(Font font)
    {
        if (_vectorMap == null) return;
        var s = _zoom;

        // 1. Mise en valeur des corridors fluviaux navigables
        foreach (var river in _vectorMap.AllRivers)
        {
            if (river.Points.Count < 2) continue;
            var screenPts = river.Points.Select(p => ToScreen(new Vector2((float)p.X, (float)p.Y))).ToArray();
            var haloWidth = (float)river.Width * 2.0f * s;
            DrawPolyline(screenPts, MapPalette.TradeRiverHighlight, haloWidth, antialiased: true);
        }

        // 2. Axes d'échanges commerciaux inter-communes
        var communes = GetActiveCommunesList();
        if (communes.Count >= 2)
        {
            for (var i = 0; i < communes.Count; i++)
            {
                for (var j = i + 1; j < communes.Count; j++)
                {
                    var c1 = communes[i];
                    var c2 = communes[j];
                    var p1 = ToScreen(c1.WorldPos);
                    var p2 = ToScreen(c2.WorldPos);

                    DrawDashedLine(p1, p2, MapPalette.TradeRoad, 2.2f * s, dash: 8.0f * s);

                    var dx = (double)(c1.WorldPos.X - c2.WorldPos.X);
                    var dy = (double)(c1.WorldPos.Y - c2.WorldPos.Y);
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    var loss = GeographicEconomics.TransportLoss(dist);

                    var mid = (p1 + p2) * 0.5f;
                    var tag = $"{dist:F0} lieues · Perte {(loss * 100):F0}%";
                    var fontSize = (int)Math.Clamp(9 * s, 8, 11);
                    var tagSize = font.GetStringSize(tag, HorizontalAlignment.Left, -1, fontSize);
                    var tagRect = new Rect2(mid.X - tagSize.X * 0.5f - 4, mid.Y - tagSize.Y * 0.5f - 2, tagSize.X + 8, tagSize.Y + 4);

                    DrawRect(tagRect, MapPalette.LabelBackground);
                    DrawRect(tagRect, MapPalette.TradeRoad, filled: false, width: 0.9f);
                    DrawString(font, new Vector2(tagRect.Position.X + 4, tagRect.Position.Y + tagSize.Y), tag, HorizontalAlignment.Left, -1, fontSize, MapPalette.TradeRoad);
                }
            }
        }
    }

    private IReadOnlyList<(string Id, string Name, Vector2 WorldPos, double Population)> GetActiveCommunesList()
    {
        var list = new List<(string Id, string Name, Vector2 WorldPos, double Population)>();

        if (_gameView is not null)
        {
            var simCommunes = _gameView.Communes();
            foreach (var c in simCommunes)
            {
                var pos = GetCommuneWorldPosition(c.Id);
                if (pos.HasValue)
                {
                    list.Add((c.Id, c.Name, pos.Value, c.Residents));
                }
            }
            if (list.Count > 0)
                return list;
        }

        if (_vectorMap is not null)
        {
            foreach (var c in _vectorMap.AllCommunes)
            {
                list.Add((c.Id, c.Name, new Vector2((float)c.Position.X, (float)c.Position.Y), c.Population));
            }
            if (list.Count > 0)
                return list;
        }

        // Fallback démo
        list.Add(("commune-1", "Première commune", new Vector2(470, 310), 5000));
        return list;
    }

    private Vector2? GetCommuneWorldPosition(string communeId)
    {
        if (_gameView is not null)
        {
            var p = _gameView.CommunePosition(communeId);
            if (p.HasValue)
                return new Vector2((float)p.Value.X, (float)p.Value.Y);
        }

        if (_vectorMap is not null)
        {
            var match = _vectorMap.AllCommunes.FirstOrDefault(c => c.Id == communeId);
            if (match is not null)
                return new Vector2((float)match.Position.X, (float)match.Position.Y);

            if ((communeId == _gameView?.CapitalId) && _vectorMap.DefaultCommune is not null)
                return new Vector2((float)_vectorMap.DefaultCommune.Position.X, (float)_vectorMap.DefaultCommune.Position.Y);
        }

        if (communeId == _gameView?.CapitalId)
            return new Vector2(470, 310);

        return null;
    }

    private List<Vector2[]> GetPrefecturePolygons(PrefectureView prefecture)
    {
        if (_prefecturePolygonsCache.TryGetValue(prefecture.Id, out var cached))
            return cached;

        var memberPositions = new List<Vector2>();
        foreach (var memberId in prefecture.MemberIds)
        {
            var pos = GetCommuneWorldPosition(memberId);
            if (pos.HasValue)
                memberPositions.Add(pos.Value);
        }

        if (prefecture.SeatId is not null)
        {
            var seatPos = GetCommuneWorldPosition(prefecture.SeatId);
            if (seatPos.HasValue && !memberPositions.Any(p => p.DistanceSquaredTo(seatPos.Value) < 1.0f))
            {
                memberPositions.Add(seatPos.Value);
            }
        }

        if (memberPositions.Count == 0)
        {
            _prefecturePolygonsCache[prefecture.Id] = [];
            return [];
        }

        const float radius = 240.0f; // Rayon de juridiction en lieues
        var currentRegions = new List<Vector2[]> { CreateCirclePolygon(memberPositions[0], radius, 16) };

        for (var i = 1; i < memberPositions.Count; i++)
        {
            var newDisk = CreateCirclePolygon(memberPositions[i], radius, 16);
            var mergedWithAny = false;
            for (var r = 0; r < currentRegions.Count; r++)
            {
                var merged = Geometry2D.MergePolygons(currentRegions[r], newDisk);
                if (merged.Count == 1)
                {
                    currentRegions[r] = merged[0];
                    mergedWithAny = true;
                    break;
                }
            }
            if (!mergedWithAny)
            {
                currentRegions.Add(newDisk);
            }
        }

        if (currentRegions.Count > 1)
        {
            var consolidated = new List<Vector2[]>();
            foreach (var poly in currentRegions)
            {
                var merged = false;
                for (var j = 0; j < consolidated.Count; j++)
                {
                    var res = Geometry2D.MergePolygons(consolidated[j], poly);
                    if (res.Count == 1)
                    {
                        consolidated[j] = res[0];
                        merged = true;
                        break;
                    }
                }
                if (!merged) consolidated.Add(poly);
            }
            currentRegions = consolidated;
        }

        _prefecturePolygonsCache[prefecture.Id] = currentRegions;
        return currentRegions;
    }

    private static Vector2[] CreateCirclePolygon(Vector2 center, float radius, int segments = 16)
    {
        var pts = new Vector2[segments];
        var step = Mathf.Tau / segments;
        for (var i = 0; i < segments; i++)
        {
            var angle = i * step;
            pts[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        return pts;
    }

    private void DrawCommuneBadge(Font font, Vector2 center, string text, Color textColor, Color bgColor)
    {
        var s = _zoom;
        var fontSize = (int)Math.Clamp(10 * s, 9, 13);
        var textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
        var padding = new Vector2(6 * s, 3 * s);
        var boxRect = new Rect2(center.X - textSize.X * 0.5f - padding.X, center.Y + 12 * s, textSize.X + padding.X * 2, textSize.Y + padding.Y * 2);

        DrawRect(boxRect, bgColor);
        DrawRect(boxRect, MapPalette.LabelBorder, filled: false, width: 1.0f);
        DrawString(font, new Vector2(boxRect.Position.X + padding.X, boxRect.Position.Y + padding.Y + textSize.Y - 2), text, HorizontalAlignment.Left, -1, fontSize, textColor);
    }

    private void DrawPrefectureHeader(Font font, Vector2 seatCenter, string prefName)
    {
        var s = _zoom;
        var fontSize = (int)Math.Clamp(11 * s, 10, 14);
        var label = $"DÉPARTEMENT DE {prefName.ToUpperInvariant()}";
        var textSize = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize);
        var padding = new Vector2(8 * s, 4 * s);
        var boxRect = new Rect2(seatCenter.X - textSize.X * 0.5f - padding.X, seatCenter.Y - 26 * s - textSize.Y, textSize.X + padding.X * 2, textSize.Y + padding.Y * 2);

        DrawRect(boxRect, new Color(0.99f, 0.98f, 0.96f, 0.94f));
        DrawRect(boxRect, MapPalette.PrefectureBorder, filled: false, width: 1.2f);
        DrawString(font, new Vector2(boxRect.Position.X + padding.X, boxRect.Position.Y + padding.Y + textSize.Y - 2), label, HorizontalAlignment.Left, -1, fontSize, MapPalette.PrefectureSeat);
    }

    private void DrawLegacyCadastralTown(Font font)
    {
        var townCenter = ToScreen(new Vector2(470, 310));

        // 1. Ruelle et chemins pavés reliant les quartiers
        var s = _zoom;
        DrawLine(townCenter + new Vector2(-50, 20) * s, townCenter + new Vector2(60, -15) * s, MapPalette.Street, 3f * s);
        DrawLine(townCenter + new Vector2(-20, -40) * s, townCenter + new Vector2(30, 45) * s, MapPalette.Street, 3f * s);
        DrawLine(townCenter, townCenter + new Vector2(-65, -20) * s, MapPalette.Street, 2.5f * s);
        DrawLine(townCenter + new Vector2(20, -10) * s, townCenter + new Vector2(80, 25) * s, MapPalette.Street, 2.5f * s);

        // 2. Bâtiments réels dessinés avec leurs toitures cadastrales
        // Mairie
        var mairiePos = townCenter + new Vector2(-15, -30) * s;
        DrawBuildingFootprint(mairiePos, new Vector2(28, 20) * s, MapPalette.RoofSlate);
        DrawCircle(mairiePos + new Vector2(0, -14 * s), 3f * s, MapPalette.BellTower);

        // École
        var ecolePos = townCenter + new Vector2(-65, -15) * s;
        DrawBuildingFootprint(ecolePos, new Vector2(24, 16) * s, MapPalette.RoofTile);

        // Moulin & Boulangerie
        var moulinPos = townCenter + new Vector2(45, -20) * s;
        DrawBuildingFootprint(moulinPos, new Vector2(22, 16) * s, MapPalette.RoofTile);
        DrawCircle(moulinPos + new Vector2(14 * s, 0), 4f * s, MapPalette.BuildingStroke);

        // Logements d'État
        var logPos = townCenter + new Vector2(-45, 30) * s;
        DrawBuildingFootprint(logPos, new Vector2(32, 18) * s, MapPalette.RoofTile);

        // Entrepôt d'État
        var entrepotPos = townCenter + new Vector2(60, 25) * s;
        DrawBuildingFootprint(entrepotPos, new Vector2(28, 16) * s, MapPalette.RoofSlate);

        // 3. Le Blason central communal
        DrawBlason(townCenter + new Vector2(0, 5) * s);

        // 4. Cartouche avec le nom
        var bannerRect = new Rect2(townCenter.X - 90 * s, townCenter.Y + 60 * s, 180 * s, 22 * s);
        DrawRect(bannerRect, MapPalette.LabelBackground);
        DrawRect(bannerRect, MapPalette.LabelBorder, filled: false, width: 1.2f);
        DrawString(font, new Vector2(bannerRect.Position.X, bannerRect.Position.Y + 15 * s), "PREMIÈRE COMMUNE", HorizontalAlignment.Center, (int)bannerRect.Size.X, (int)(11 * s), MapPalette.InkText);
    }

    private void DrawBuildingFootprint(Vector2 center, Vector2 size, Color roofColor)
    {
        var half = size * 0.5f;
        var rect = new Rect2(center - half, size);

        // Empreinte murale
        DrawRect(rect, MapPalette.BuildingWall);
        DrawRect(rect, MapPalette.BuildingStroke, filled: false, width: 1.0f);

        // Toit à deux versants avec arête de faîtage
        var pLeft = new Vector2(rect.Position.X, rect.Position.Y + half.Y);
        var pRight = new Vector2(rect.Position.X + size.X, rect.Position.Y + half.Y);
        var pTop = new Vector2(center.X, rect.Position.Y);
        var pBottom = new Vector2(center.X, rect.Position.Y + size.Y);

        DrawColoredPolygon([rect.Position, new Vector2(rect.Position.X + size.X, rect.Position.Y), pBottom, new Vector2(rect.Position.X, rect.Position.Y + size.Y)], roofColor);
        DrawLine(new Vector2(rect.Position.X, center.Y), new Vector2(rect.Position.X + size.X, center.Y), MapPalette.BuildingStroke, 1.2f);
    }

    private void DrawBlason(Vector2 pos)
    {
        var s = _zoom;
        var shield = new Vector2[]
        {
            pos + new Vector2(-12, -14) * s,
            pos + new Vector2(12, -14) * s,
            pos + new Vector2(12, 2) * s,
            pos + new Vector2(0, 16) * s,
            pos + new Vector2(-12, 2) * s,
        };

        DrawColoredPolygon(shield, MapPalette.BlasonRed);
        DrawPolyline([.. shield, shield[0]], MapPalette.BuildingStroke, 1.5f, antialiased: true);

        // Étoile d'or au chef
        DrawCircle(pos + new Vector2(0, -4) * s, 3f * s, MapPalette.BlasonGold);
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

    private Vector2 ToScreen(Vector2 worldPos) => _panOffset + worldPos * _zoom;

    private void SafeDrawColoredPolygon(Vector2[] points, Color color)
    {
        if (points == null || points.Length < 3) return;

        var cleaned = CleanPolygonVertices(points);
        if (cleaned.Length < 3) return;

        if (Geometry2D.IsPolygonClockwise(cleaned))
        {
            Array.Reverse(cleaned);
        }

        if (cleaned.Length == 3)
        {
            var area = Math.Abs((cleaned[1].X - cleaned[0].X) * (cleaned[2].Y - cleaned[0].Y) - (cleaned[1].Y - cleaned[0].Y) * (cleaned[2].X - cleaned[0].X));
            if (area > 1.0f)
            {
                if (Geometry2D.IsPolygonClockwise(cleaned)) Array.Reverse(cleaned);
                DrawColoredPolygon(cleaned, color);
            }
            return;
        }

        var indices = Geometry2D.TriangulatePolygon(cleaned);
        if (indices != null && indices.Length >= 3)
        {
            for (var i = 0; i < indices.Length; i += 3)
            {
                var tri = new[] { cleaned[indices[i]], cleaned[indices[i + 1]], cleaned[indices[i + 2]] };
                var cross = (tri[1].X - tri[0].X) * (tri[2].Y - tri[0].Y) - (tri[1].Y - tri[0].Y) * (tri[2].X - tri[0].X);
                if (Math.Abs(cross) > 1.0f)
                {
                    if (Geometry2D.IsPolygonClockwise(tri))
                    {
                        Array.Reverse(tri);
                    }
                    DrawColoredPolygon(tri, color);
                }
            }
            return;
        }

        var convexParts = Geometry2D.DecomposePolygonInConvex(cleaned);
        if (convexParts != null && convexParts.Count > 0)
        {
            foreach (var part in convexParts)
            {
                if (part.Length >= 3)
                {
                    var p = part;
                    if (Geometry2D.IsPolygonClockwise(p))
                    {
                        var rev = (Vector2[])p.Clone();
                        Array.Reverse(rev);
                        p = rev;
                    }
                    var partIndices = Geometry2D.TriangulatePolygon(p);
                    if (partIndices != null && partIndices.Length >= 3)
                    {
                        for (var i = 0; i < partIndices.Length; i += 3)
                        {
                            var tri = new[] { p[partIndices[i]], p[partIndices[i + 1]], p[partIndices[i + 2]] };
                            if (Geometry2D.IsPolygonClockwise(tri))
                            {
                                Array.Reverse(tri);
                            }
                            DrawColoredPolygon(tri, color);
                        }
                    }
                }
            }
        }
    }

    private static Vector2[] CleanPolygonVertices(Vector2[] pts)
    {
        if (pts.Length < 3) return pts;
        var list = new List<Vector2>(pts.Length);
        for (var i = 0; i < pts.Length; i++)
        {
            var curr = pts[i];
            if (list.Count == 0 || curr.DistanceSquaredTo(list[^1]) > 0.5f)
            {
                list.Add(curr);
            }
        }
        if (list.Count >= 3 && list[0].DistanceSquaredTo(list[^1]) <= 0.5f)
        {
            list.RemoveAt(list.Count - 1);
        }
        return list.ToArray();
    }
}
