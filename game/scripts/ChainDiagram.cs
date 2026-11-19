using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Le schéma de la chaîne de production d'une commune, dessiné par Godot, en bandes (décidé le 19 novembre 2026). Une
/// bande par besoin que la chaîne nourrit (se nourrir, se distraire, s'habiller, se meubler), puis Chantiers, Énergie
/// (combustible et eau des bâtiments), Services et Autres, séparées par un trait, avec le nom du besoin à droite.
/// Un bâtiment est dans la première bande (dans cet ordre) qu'il nourrit, sans être dupliqué : s'il en nourrit
/// d'autres, une flèche descend vers elles. Dans chaque bande, les colonnes suivent la profondeur dans la chaîne
/// (matières premières, transformation, produits finis), et ce qui va droit au besoin est dans la dernière ; le nom du
/// produit est écrit sur chaque flèche. Chaque boîte (le type en titre, le produit dessous) a
/// la couleur de son propriétaire à gauche, un cadre orange quand ses matières ont manqué ; les lignes
/// d'approvisionnement du joueur sont des flèches blanches épaisses. En bas, une bande « Sans recette » montre les
/// bâtiments qui ne produisent rien et ce qu'ils offrent. Un produit suivi garde ses fournisseurs et ses clients en vue,
/// le reste s'estompe. Un clic choisit un bâtiment (avec ou sans recette).
/// </summary>
public partial class ChainDiagram : Control
{
    private const float BoxWidth = 190;
    private const float BoxHeight = 46;
    private const float ColumnGap = 74;
    private const float RowGap = 10;
    private const float BandPadding = 10;
    private const float LabelWidth = 150;
    private const float SmallBoxWidth = 170;
    private const float SmallBoxHeight = 40;
    private const float Margin = 12;
    private const float HeaderHeight = 26;

    /// <summary>
    /// Une boîte : un bâtiment, avec ou sans recette. Son titre est le type (et le numéro d'un doublon), le produit va
    /// sur la ligne du dessous ; le nom entier s'affiche au survol.
    /// </summary>
    private sealed record Node(string Key, string Title, string Detail, Color Strip, bool Short, Rect2 Box)
    {
        public string? FullName { get; init; }
    }

    /// <summary>« Atelier (Farine) 2 » → (« Atelier 2 », « Farine ») ; un nom sans produit reste entier.</summary>
    private static (string Type, string? Product) Split(string name)
    {
        var open = name.IndexOf(" (", System.StringComparison.Ordinal);
        var close = name.LastIndexOf(')');
        return open < 0 || close < open ? (name, null) : (name[..open] + name[(close + 1)..], name[(open + 2)..close]);
    }

    /// <summary>Une flèche : de la boîte qui produit à celle qui utilise (ou au nom d'une bande), avec le produit.</summary>
    private sealed record Edge(string From, string To, string Product);

    /// <summary>Une bande : son nom (à droite, en majuscules) et sa hauteur dans le schéma.</summary>
    private sealed record Band(string Key, string Title, float Top, float Bottom);

    private static readonly string[] ColumnTitles = ["Matières premières", "Transformation", "Produits finis"];

    // L'ordre des bandes : un bâtiment qui en nourrit plusieurs va dans la première.
    private static readonly string[] BandOrder = ["Food", "Leisure", "Clothing", "Furniture", "chantiers", "energie", "services", "autres"];

    private List<Node> _nodes = [];
    private List<Edge> _edges = [];
    private List<Band> _bands = [];
    private Dictionary<string, Rect2> _labels = [];
    private List<string> _columns = [];
    private float _plainTop;
    private IReadOnlyList<(string From, string To)> _links = [];
    private HashSet<string>? _followed;
    private string? _selected;

    /// <summary>Un bâtiment a été choisi d'un clic.</summary>
    public event System.Action<string>? BuildingChosen;

    public ChainDiagram()
    {
        MouseFilter = MouseFilterEnum.Stop;
    }

    public string? Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            QueueRedraw();
        }
    }

    /// <summary>
    /// Change les données : les bâtiments à recette de la commune, ceux sans recette, le nom de chaque besoin, le produit
    /// suivi (null : tous) et les lignes d'approvisionnement entre bâtiments de la commune.
    /// </summary>
    public void SetChain(IReadOnlyList<ChainBuildingView> buildings, IReadOnlyList<PlainBuildingView> plain,
        System.Func<string, string> needName, string? followedProduct, IReadOnlyList<(string From, string To)>? supplyLinks = null)
    {
        _links = supplyLinks ?? [];
        var producersOf = buildings.GroupBy(b => b.ProductId).ToDictionary(g => g.Key, g => g.ToList());
        var consumersOf = buildings.SelectMany(b => b.Inputs.Select(i => (i.ProductId, Building: b)))
            .GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Select(x => x.Building).Distinct().ToList());

        var depth = Depths(buildings, producersOf);
        var lastColumn = depth.Values.DefaultIfEmpty(0).Max();
        // Ce qui va droit au nom de sa bande (un produit fini, ou que personne n'utilise ici) se range dans la dernière
        // colonne : sa flèche ne traverse pas les autres boîtes de sa ligne.
        foreach (var building in buildings)
            if (building.NeedId is not null || !consumersOf.ContainsKey(building.ProductId))
                depth[building.Id] = lastColumn;
        _columns = Enumerable.Range(0, lastColumn + 1)
            .Select(c => c == 0 ? ColumnTitles[0] : c == lastColumn ? ColumnTitles[2] : ColumnTitles[1])
            .ToList();

        var bandsOf = buildings.ToDictionary(b => b.Id, b => BandsOf(b, consumersOf, []));
        var bandOf = bandsOf.ToDictionary(b => b.Key, b => b.Value.OrderBy(k => System.Array.IndexOf(BandOrder, k)).First());

        // Les bandes, de haut en bas ; dans chacune, une ligne par boîte de la colonne la plus remplie.
        _nodes = [];
        _bands = [];
        _labels = [];
        var top = HeaderHeight + Margin;
        var labelX = ColumnX(lastColumn + 1);
        foreach (var bandKey in BandOrder.Where(k => bandOf.ContainsValue(k)))
        {
            var members = buildings.Where(b => bandOf[b.Id] == bandKey).ToList();
            var rows = members.GroupBy(b => depth[b.Id]).Max(g => g.Count());
            var bandTop = top;
            foreach (var column in members.GroupBy(b => depth[b.Id]))
            {
                var row = 0;
                foreach (var building in column.OrderBy(b => b.Name))
                {
                    var box = new Rect2(ColumnX(column.Key), bandTop + BandPadding + row++ * (BoxHeight + RowGap), BoxWidth, BoxHeight);
                    var (type, product) = Split(building.Name);
                    _nodes.Add(new Node(building.Id, type, $"{product ?? building.ProductName} · {building.ProducedToday:N0} par jour",
                        UiTheme.OwnershipColor(building.Ownership), building.SupplyShare < 0.995, box) { FullName = building.Name });
                }
            }
            var bottom = bandTop + 2 * BandPadding + rows * (BoxHeight + RowGap) - RowGap;
            _bands.Add(new Band(bandKey, BandTitle(bandKey, needName), bandTop, bottom));
            _labels[bandKey] = new Rect2(labelX, (bandTop + bottom) / 2 - 14, LabelWidth, 28);
            top = bottom;
        }

        // La bande des bâtiments sans recette, en petites boîtes sur plusieurs lignes.
        _plainTop = top;
        var perRow = System.Math.Max(1, (int)((labelX + LabelWidth - Margin) / (SmallBoxWidth + RowGap)));
        for (var index = 0; index < plain.Count; index++)
        {
            var building = plain[index];
            var box = new Rect2(Margin + index % perRow * (SmallBoxWidth + RowGap),
                top + 22 + BandPadding + index / perRow * (SmallBoxHeight + RowGap), SmallBoxWidth, SmallBoxHeight);
            _nodes.Add(new Node(building.Id, building.Name, Offer(building), UiTheme.OwnershipColor(building.Ownership), false, box));
        }
        var plainRows = (plain.Count + perRow - 1) / perRow;
        var height = top + 22 + 2 * BandPadding + plainRows * (SmallBoxHeight + RowGap) + Margin;

        // Les flèches : du producteur d'une matière à ceux qui l'utilisent, et du produit fini au nom de sa bande.
        _edges = [];
        foreach (var building in buildings)
        {
            foreach (var input in building.Inputs)
                foreach (var producer in producersOf.GetValueOrDefault(input.ProductId) ?? [])
                    if (producer.Id != building.Id)
                        _edges.Add(new Edge(producer.Id, building.Id, input.ProductName));
            if (!consumersOf.ContainsKey(building.ProductId) || building.NeedId is not null)
                _edges.Add(new Edge(building.Id, "bande:" + bandOf[building.Id], ""));
        }

        _followed = followedProduct is null ? null : Followed(buildings, followedProduct);
        CustomMinimumSize = new Vector2(labelX + LabelWidth + Margin, height);
        QueueRedraw();
    }

    /// <summary>La profondeur de chaque bâtiment : 0 sans matière produite ici, sinon un de plus que ses fournisseurs.</summary>
    private static Dictionary<string, int> Depths(IReadOnlyList<ChainBuildingView> buildings, Dictionary<string, List<ChainBuildingView>> producersOf)
    {
        var depth = new Dictionary<string, int>();
        int DepthOf(ChainBuildingView building, HashSet<string> visiting)
        {
            if (depth.TryGetValue(building.Id, out var known))
                return known;
            // Une boucle (un bâtiment qui dépend de lui-même par d'autres) s'arrête là.
            if (!visiting.Add(building.Id))
                return 0;
            var deepest = building.Inputs
                .SelectMany(input => producersOf.GetValueOrDefault(input.ProductId) ?? [])
                .Select(producer => DepthOf(producer, visiting) + 1)
                .DefaultIfEmpty(0)
                .Max();
            visiting.Remove(building.Id);
            return depth[building.Id] = deepest;
        }
        foreach (var building in buildings)
            DepthOf(building, []);
        return depth;
    }

    /// <summary>
    /// Les bandes qu'un bâtiment nourrit : le besoin de son produit, sinon celles de ceux qui l'utilisent ; sans
    /// personne pour l'utiliser, Énergie pour un combustible ou l'eau, Chantiers pour les matériaux, Autres sinon.
    /// </summary>
    private static HashSet<string> BandsOf(ChainBuildingView building, Dictionary<string, List<ChainBuildingView>> consumersOf, HashSet<string> visiting)
    {
        if (building.NeedId is { } need)
            return [need is "Administration" or "Security" ? "services" : need is "Energy" ? "energie" : BandOrder.Contains(need) ? need : "autres"];
        if (!visiting.Add(building.Id))
            return [];
        var bands = (consumersOf.GetValueOrDefault(building.ProductId) ?? [])
            .Where(c => c.Id != building.Id)
            .SelectMany(c => BandsOf(c, consumersOf, visiting))
            .ToHashSet();
        visiting.Remove(building.Id);
        if (bands.Count > 0)
            return bands;
        return [building.FuelsBuildings ? "energie" : building.ProductId.StartsWith("materiaux") ? "chantiers" : "autres"];
    }

    private static string BandTitle(string key, System.Func<string, string> needName) => key switch
    {
        "chantiers" => "Chantiers",
        "energie" => "Énergie (bâtiments)",
        "services" => "Services",
        "autres" => "Autres produits",
        _ => needName(key),
    };

    /// <summary>Ce qu'offre un bâtiment sans recette : places de logement, places d'école, stockage.</summary>
    private static string Offer(PlainBuildingView building)
    {
        var parts = new List<string>();
        if (building.HousingPlaces > 0)
            parts.Add($"{building.HousingPlaces:N0} places de logement");
        if (building.SchoolPlaces > 0)
            parts.Add($"{building.SchoolPlaces:N0} élèves");
        if (building.StorageCapacity > 0)
            parts.Add($"stockage {building.StorageCapacity:N0}");
        return parts.Count == 0 ? "" : string.Join(" · ", parts);
    }

    /// <summary>
    /// Les boîtes à garder en vue pour un produit : ceux qui le produisent et ceux qui l'utilisent, puis tous leurs
    /// fournisseurs en remontant et tous leurs clients en descendant (noms de bandes compris).
    /// </summary>
    private HashSet<string> Followed(IReadOnlyList<ChainBuildingView> buildings, string productId)
    {
        var start = buildings.Where(b => b.ProductId == productId || b.Inputs.Any(i => i.ProductId == productId)).Select(b => b.Id).ToHashSet();
        var kept = new HashSet<string>(start);
        foreach (var forward in new[] { true, false })
        {
            var queue = new Queue<string>(start);
            while (queue.TryDequeue(out var key))
                foreach (var edge in _edges.Where(e => (forward ? e.From : e.To) == key))
                    if (kept.Add(forward ? edge.To : edge.From))
                        queue.Enqueue(forward ? edge.To : edge.From);
        }
        return kept;
    }

    private static float ColumnX(int column) => Margin + column * (BoxWidth + ColumnGap);

    private bool Dimmed(string key) => _followed is not null && !_followed.Contains(key);

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click && NodeAt(click.Position) is { } node)
        {
            Selected = node.Key;
            BuildingChosen?.Invoke(node.Key);
            AcceptEvent();
        }
    }

    private Node? NodeAt(Vector2 position) => _nodes.FirstOrDefault(n => n.Box.HasPoint(position));

    public override string _GetTooltip(Vector2 atPosition) =>
        NodeAt(atPosition) is { } node
            ? node.Detail.Length > 0 ? $"{node.FullName ?? node.Title} : {node.Detail}" : node.FullName ?? node.Title
            : "";

    public override Control? _MakeCustomTooltip(string forText) => UiTheme.Tooltip(forText);

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var size = UiTheme.FontSize - 2;
        var right = Size.X - Margin;

        for (var column = 0; column < _columns.Count; column++)
            DrawString(font, new Vector2(ColumnX(column), Margin + size), _columns[column].ToUpperInvariant(), fontSize: size - 1, modulate: UiTheme.Muted);
        DrawString(font, new Vector2(ColumnX(_columns.Count), Margin + size), "BESOIN", fontSize: size - 1, modulate: UiTheme.Muted);

        // Les bandes : un trait entre chacune, et leur nom à droite.
        foreach (var band in _bands)
        {
            DrawLine(new Vector2(Margin, band.Top), new Vector2(right, band.Top), UiTheme.Border);
            var label = _labels[band.Key];
            var alpha = Dimmed("bande:" + band.Key) ? 0.35f : 1f;
            DrawString(font, new Vector2(label.Position.X, label.GetCenter().Y + size / 2f - 2), band.Title.ToUpperInvariant(),
                HorizontalAlignment.Left, LabelWidth, size, UiTheme.Text with { A = alpha });
        }

        // La bande des bâtiments sans recette, sous un trait double.
        DrawLine(new Vector2(Margin, _plainTop), new Vector2(right, _plainTop), UiTheme.Muted, 2);
        DrawString(font, new Vector2(Margin, _plainTop + 18), "SANS RECETTE", fontSize: size - 1, modulate: UiTheme.Muted);

        var boxes = _nodes.ToDictionary(n => n.Key, n => n.Box);
        foreach (var (key, label) in _labels)
            boxes["bande:" + key] = label;

        foreach (var edge in _edges)
        {
            if (!boxes.TryGetValue(edge.From, out var from) || !boxes.TryGetValue(edge.To, out var to))
                continue;
            var dimmed = Dimmed(edge.From) || Dimmed(edge.To);
            var start = new Vector2(from.End.X, from.GetCenter().Y);
            var end = new Vector2(to.Position.X - 4, to.GetCenter().Y);
            DrawArrow(start, end, dimmed ? UiTheme.Border : UiTheme.Muted);
            // Le produit sur la flèche, quand elle est assez longue pour l'écrire.
            if (edge.Product.Length > 0 && !dimmed && end.X - start.X > 60)
                DrawString(font, (start + end) / 2 + new Vector2(-30, -5), edge.Product, HorizontalAlignment.Center, 60, size - 3, UiTheme.Muted);
        }

        // Les lignes d'approvisionnement entre bâtiments de la commune, en blanc et plus épaisses, par-dessus.
        foreach (var (fromKey, toKey) in _links)
        {
            if (!boxes.TryGetValue(fromKey, out var from) || !boxes.TryGetValue(toKey, out var to))
                continue;
            var color = Dimmed(fromKey) || Dimmed(toKey) ? UiTheme.Muted : UiTheme.Text;
            DrawArrow(new Vector2(from.End.X, from.GetCenter().Y), new Vector2(to.Position.X, to.GetCenter().Y), color, 3);
        }

        foreach (var node in _nodes)
        {
            var alpha = Dimmed(node.Key) ? 0.35f : 1f;
            var box = node.Box;
            var small = box.Size.Y < BoxHeight;
            DrawRect(box, UiTheme.Raised with { A = alpha });
            DrawRect(new Rect2(box.Position, new Vector2(5, box.Size.Y)), node.Strip with { A = alpha });
            if (node.Short)
                DrawRect(box, UiTheme.Accent with { A = alpha }, filled: false, width: 2);
            if (node.Key == _selected)
                DrawRect(box.Grow(2), UiTheme.Text, filled: false, width: 2);
            DrawString(font, box.Position + new Vector2(12, small ? 17 : 19), node.Title, HorizontalAlignment.Left, box.Size.X - 18,
                small ? size - 1 : size, UiTheme.Text with { A = alpha });
            DrawString(font, box.Position + new Vector2(12, small ? 33 : 37), node.Detail, HorizontalAlignment.Left, box.Size.X - 18, size - 2,
                UiTheme.Muted with { A = alpha });
        }
    }

    /// <summary>Une courbe douce d'une boîte à l'autre, avec une pointe à l'arrivée.</summary>
    private void DrawArrow(Vector2 from, Vector2 to, Color color, float width = 1.5f)
    {
        var bend = System.Math.Max(30, System.Math.Abs(to.X - from.X) / 2);
        var points = new Vector2[17];
        for (var index = 0; index <= 16; index++)
        {
            var t = index / 16f;
            var u = 1 - t;
            points[index] = u * u * u * from + 3 * u * u * t * (from + new Vector2(bend, 0)) + 3 * u * t * t * (to - new Vector2(bend, 0)) + t * t * t * to;
        }
        DrawPolyline(points, color, width, antialiased: true);
        DrawColoredPolygon([to, to + new Vector2(-8, -4), to + new Vector2(-8, 4)], color);
    }
}
