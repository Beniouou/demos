using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Demos.Application;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game.Map.Layers;

/// <summary>
/// Ce que la carte montre du monde et de la partie, partagé par tous les calques : la carte vectorielle
/// (<c>carte.json</c>), la vue de la partie (lecture seule), la place des communes, les enveloppes des préfectures
/// (gardées jusqu'au prochain changement) et les bâtiments d'extraction.
/// </summary>
public sealed class MapData
{
    private readonly Dictionary<string, List<Vector2[]>> _prefecturePolygonsCache = [];
    private int _lastCommunesCount = -1;
    private int _lastPrefecturesCount = -1;
    private int _stateKey;

    public VectorMapDefinition? VectorMap { get; private set; }

    /// <summary>Le relief calculé par l'outil de carte (altitude et ombres), ou rien s'il manque.</summary>
    public MapRelief? Relief { get; private set; }
    public GameView? View { get; private set; }

    public void LoadVectorMap()
    {
        Relief = MapRelief.Load();
        try
        {
            if (FileAccess.FileExists("res://data/carte.json"))
            {
                var json = FileAccess.GetFileAsString("res://data/carte.json");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                VectorMap = JsonSerializer.Deserialize<VectorMapDefinition>(json, options);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Erreur lecture carte.json : {ex.Message}");
        }
    }

    public void SetView(GameView view)
    {
        View = view;
        _prefecturePolygonsCache.Clear();
    }

    /// <summary>
    /// Suit la partie. Vrai si ce que la carte en montre a changé (communes, habitants affichés, préfectures, capitale,
    /// bâtiments posés, chantiers) : les calques qui la dessinent sont alors à refaire.
    /// </summary>
    public bool Update(GameView view)
    {
        View = view;
        var changed = false;
        var comms = view.Communes();
        var prefs = view.Prefectures();
        if (comms.Count != _lastCommunesCount || prefs.Count != _lastPrefecturesCount)
        {
            _lastCommunesCount = comms.Count;
            _lastPrefecturesCount = prefs.Count;
            _prefecturePolygonsCache.Clear();
            changed = true;
        }

        var state = StateKey(view, comms, prefs);
        if (state != _stateKey)
        {
            _stateKey = state;
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// Une empreinte de ce que la carte montre de la partie : communes (nom, habitants affichés), préfectures, capitale,
    /// bâtiments posés et chantiers.
    /// </summary>
    private static int StateKey(GameView view, IReadOnlyList<CommuneView> communes, IReadOnlyList<PrefectureView> prefectures)
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
        // Les routes (niveau) et les échanges du mois (arrondis) : la carte les montre.
        foreach (var road in view.Roads())
        {
            key.Add(road.A);
            key.Add(road.B);
            key.Add(road.Level);
        }
        foreach (var flow in view.TradeFlows())
            key.Add(Math.Round(flow.MonthlyQuantity / 10));
        // Les bâtiments d'extraction : nom, commune, zone (sa couleur), chantier ou non.
        if (view.BuildingPositions().Count > 0)
            foreach (var (id, info) in ExtractionBuildings(view))
            {
                key.Add(id);
                key.Add(info);
            }
        return key.ToHashCode();
    }

    // ---- Communes ----------------------------------------------------------------------------------------------------

    /// <summary>Les communes à montrer : celles de la partie qui ont une place, sinon celles de la carte, sinon une démo.</summary>
    public IReadOnlyList<(string Id, string Name, Vector2 WorldPos, double Population)> Communes()
    {
        var list = new List<(string Id, string Name, Vector2 WorldPos, double Population)>();

        if (View is not null)
        {
            var simCommunes = View.Communes();
            foreach (var c in simCommunes)
            {
                var pos = CommunePosition(c.Id);
                if (pos.HasValue)
                {
                    list.Add((c.Id, c.Name, pos.Value, c.Residents));
                }
            }
            if (list.Count > 0)
                return list;
        }

        if (VectorMap is not null)
        {
            foreach (var c in VectorMap.AllCommunes)
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

    public Vector2? CommunePosition(string communeId)
    {
        if (View is not null)
        {
            var p = View.CommunePosition(communeId);
            if (p.HasValue)
                return new Vector2((float)p.Value.X, (float)p.Value.Y);
        }

        if (VectorMap is not null)
        {
            var match = VectorMap.AllCommunes.FirstOrDefault(c => c.Id == communeId);
            if (match is not null)
                return new Vector2((float)match.Position.X, (float)match.Position.Y);

            if ((communeId == View?.CapitalId) && VectorMap.DefaultCommune is not null)
                return new Vector2((float)VectorMap.DefaultCommune.Position.X, (float)VectorMap.DefaultCommune.Position.Y);
        }

        if (communeId == View?.CapitalId)
            return new Vector2(470, 310);

        return null;
    }

    // ---- Préfectures -------------------------------------------------------------------------------------------------

    /// <summary>L'enveloppe d'une préfecture : les disques de juridiction de ses communes, fusionnés (gardée).</summary>
    public List<Vector2[]> PrefecturePolygons(PrefectureView prefecture)
    {
        if (_prefecturePolygonsCache.TryGetValue(prefecture.Id, out var cached))
            return cached;

        var memberPositions = new List<Vector2>();
        foreach (var memberId in prefecture.MemberIds)
        {
            var pos = CommunePosition(memberId);
            if (pos.HasValue)
                memberPositions.Add(pos.Value);
        }

        if (prefecture.SeatId is not null)
        {
            var seatPos = CommunePosition(prefecture.SeatId);
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
        var currentRegions = new List<Vector2[]> { CirclePolygon(memberPositions[0], radius, 16) };

        for (var i = 1; i < memberPositions.Count; i++)
        {
            var newDisk = CirclePolygon(memberPositions[i], radius, 16);
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

    public static Vector2[] CirclePolygon(Vector2 center, float radius, int segments = 16)
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

    // ---- Bâtiments d'extraction --------------------------------------------------------------------------------------

    /// <summary>Chaque bâtiment d'extraction (et chaque chantier) : son nom, sa commune, sa zone, et s'il est en chantier.</summary>
    public static Dictionary<string, (string Name, string? CommuneId, string? Kind, bool Building)> ExtractionBuildings(GameView view)
    {
        var buildings = new Dictionary<string, (string Name, string? CommuneId, string? Kind, bool Building)>();
        void Visit(StructureNode node, string? communeId)
        {
            var commune = node.Kind == StructureNodeKind.Commune ? node.Id : communeId;
            if (node.Kind == StructureNodeKind.Building)
                buildings[node.Id] = (node.Name, communeId, ExtractionSites.ZonesFor(node.TypeId, node.ProductId).FirstOrDefault(), false);
            foreach (var child in node.Children)
                Visit(child, commune);
        }
        foreach (var root in view.StructureTree())
            Visit(root, null);
        foreach (var site in view.ConstructionSites())
            buildings[site.BuildingId] = (site.Name, site.CommuneId, ExtractionSites.ZonesFor(site.TypeId, null).FirstOrDefault(), true);
        return buildings;
    }
}
