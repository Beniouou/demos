using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Le schéma de la chaîne de production d'une commune, dessiné par Godot. Chaque bâtiment est une boîte, placée dans
/// une colonne selon sa profondeur dans la chaîne (0 : sans matière ; sinon un de plus que ses fournisseurs), avec la
/// couleur de son propriétaire à gauche et un cadre orange quand ses matières ont manqué. Une flèche va du bâtiment
/// qui produit une matière à ceux qui l'utilisent dans la commune ; la dernière colonne montre les besoins que les
/// produits couvrent. Les lignes d'approvisionnement du joueur sont des flèches blanches épaisses. Un produit suivi
/// garde ses fournisseurs et ses clients en vue, le reste s'estompe. Un clic choisit un bâtiment.
/// </summary>
public partial class ChainDiagram : Control
{
    private const float BoxWidth = 190;
    private const float BoxHeight = 50;
    private const float ColumnGap = 70;
    private const float RowGap = 14;
    private const float Margin = 12;
    private const float HeaderHeight = 26;

    /// <summary>Une boîte : un bâtiment, ou un besoin des habitants (dernière colonne).</summary>
    private sealed record Node(string Key, string Title, string Detail, Color Strip, bool Short, ChainBuildingView? Building, Rect2 Box);

    /// <summary>Une flèche : de la boîte qui produit à celle qui utilise, avec le nom du produit.</summary>
    private sealed record Edge(string From, string To, string Product);

    private static readonly string[] ColumnTitles = ["Matières premières", "Transformation", "Produits finis"];

    private List<Node> _nodes = [];
    private List<Edge> _edges = [];
    private List<string> _columns = [];
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
    /// Change les données : les bâtiments de la commune, le nom de chaque besoin, et le produit suivi (null : tous).
    /// </summary>
    public void SetChain(IReadOnlyList<ChainBuildingView> buildings, System.Func<string, string> needName, string? followedProduct,
        IReadOnlyList<(string From, string To)>? supplyLinks = null)
    {
        _links = supplyLinks ?? [];
        var producersOf = buildings.GroupBy(b => b.ProductId).ToDictionary(g => g.Key, g => g.ToList());
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

        var lastColumn = depth.Values.DefaultIfEmpty(0).Max();
        var needColumn = lastColumn + 1;
        _columns = Enumerable.Range(0, needColumn + 1)
            .Select(c => c == needColumn ? "Besoins" : c == 0 ? ColumnTitles[0] : c == lastColumn ? ColumnTitles[2] : ColumnTitles[1])
            .ToList();

        // Les boîtes, colonne par colonne, rangées par produit puis par nom.
        _nodes = [];
        foreach (var column in Enumerable.Range(0, needColumn))
        {
            var row = 0;
            foreach (var building in buildings.Where(b => depth[b.Id] == column).OrderBy(b => b.ProductName).ThenBy(b => b.Name))
            {
                var detail = $"{building.ProducedToday:N0} par jour";
                _nodes.Add(new Node(building.Id, building.Name, detail, UiTheme.OwnershipColor(building.Ownership),
                    building.SupplyShare < 0.995, building, BoxAt(column, row++)));
            }
        }

        var needs = buildings.Where(b => b.NeedId is not null).Select(b => b.NeedId!).Distinct().OrderBy(needName).ToList();
        for (var row = 0; row < needs.Count; row++)
            _nodes.Add(new Node("besoin:" + needs[row], needName(needs[row]), "habitants", UiTheme.Muted, false, null, BoxAt(needColumn, row)));

        // Les flèches : de chaque producteur d'une matière vers chaque bâtiment qui l'utilise, et vers les besoins.
        _edges = [];
        foreach (var building in buildings)
        {
            foreach (var input in building.Inputs)
                foreach (var producer in producersOf.GetValueOrDefault(input.ProductId) ?? [])
                    if (producer.Id != building.Id)
                        _edges.Add(new Edge(producer.Id, building.Id, input.ProductName));
            if (building.NeedId is { } need)
                _edges.Add(new Edge(building.Id, "besoin:" + need, building.ProductName));
        }

        _followed = followedProduct is null ? null : Followed(buildings, followedProduct);

        var rows = Enumerable.Range(0, needColumn + 1).Select(c => _nodes.Count(n => n.Box.Position.X == ColumnX(c))).DefaultIfEmpty(0).Max();
        CustomMinimumSize = new Vector2(ColumnX(needColumn) + BoxWidth + Margin, HeaderHeight + Margin + rows * (BoxHeight + RowGap) + Margin);
        QueueRedraw();
    }

    /// <summary>
    /// Les boîtes à garder en vue pour un produit : ceux qui le produisent et ceux qui l'utilisent, puis tous leurs
    /// fournisseurs en remontant et tous leurs clients en descendant (besoins compris).
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

    private static Rect2 BoxAt(int column, int row) =>
        new(ColumnX(column), HeaderHeight + Margin + row * (BoxHeight + RowGap), BoxWidth, BoxHeight);

    private bool Dimmed(string key) => _followed is not null && !_followed.Contains(key);

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
            && NodeAt(click.Position) is { Building: { } building })
        {
            Selected = building.Id;
            BuildingChosen?.Invoke(building.Id);
            AcceptEvent();
        }
    }

    private Node? NodeAt(Vector2 position) => _nodes.FirstOrDefault(n => n.Box.HasPoint(position));

    public override string _GetTooltip(Vector2 atPosition)
    {
        if (NodeAt(atPosition) is not { Building: { } building })
            return "";
        var lines = new List<string>
        {
            $"{building.Name} : {building.ProducedToday:N0} le dernier jour ({building.Workers:N0} travailleurs de production).",
        };
        if (building.Inputs.Count > 0)
            lines.Add($"Matières : {string.Join(", ", building.Inputs.Select(i => i.ProductName))} ; elles ont permis {building.SupplyShare:P0} de la production.");
        if (building.EnergyShare < 0.995)
            lines.Add($"Énergie couverte : {building.EnergyShare:P0}.");
        return string.Join("\n", lines);
    }

    public override Control? _MakeCustomTooltip(string forText) => UiTheme.Tooltip(forText);

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var size = UiTheme.FontSize - 2;

        for (var column = 0; column < _columns.Count; column++)
            DrawString(font, new Vector2(ColumnX(column), Margin + size), _columns[column].ToUpperInvariant(), fontSize: size - 1, modulate: UiTheme.Muted);

        var boxes = _nodes.ToDictionary(n => n.Key, n => n.Box);
        foreach (var edge in _edges)
        {
            if (!boxes.TryGetValue(edge.From, out var from) || !boxes.TryGetValue(edge.To, out var to))
                continue;
            var dimmed = Dimmed(edge.From) || Dimmed(edge.To);
            var color = dimmed ? UiTheme.Border : UiTheme.Muted;
            DrawArrow(new Vector2(from.End.X, from.GetCenter().Y), new Vector2(to.Position.X, to.GetCenter().Y), color);
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
            DrawRect(box, UiTheme.Raised with { A = alpha });
            DrawRect(new Rect2(box.Position, new Vector2(5, box.Size.Y)), node.Strip with { A = alpha });
            if (node.Short)
                DrawRect(box, UiTheme.Accent with { A = alpha }, filled: false, width: 2);
            if (node.Key == _selected)
                DrawRect(box.Grow(2), UiTheme.Text, filled: false, width: 2);
            DrawString(font, box.Position + new Vector2(12, 20), node.Title, HorizontalAlignment.Left, BoxWidth - 18, size,
                UiTheme.Text with { A = alpha });
            DrawString(font, box.Position + new Vector2(12, 40), node.Detail, HorizontalAlignment.Left, BoxWidth - 18, size - 1,
                UiTheme.Muted with { A = alpha });
        }
    }

    /// <summary>Une courbe douce d'une boîte à l'autre, avec une pointe à l'arrivée.</summary>
    private void DrawArrow(Vector2 from, Vector2 to, Color color, float width = 1.5f)
    {
        var bend = System.Math.Max(30, (to.X - from.X) / 2);
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
