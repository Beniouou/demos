using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Communes → Statistiques, en lecture : pour la commune du menu (au-dessus des onglets), la courbe jour par jour du produit
/// choisi (production, utilisé, envoyé vers une autre commune, perdu en route, perdu au stockage, et le stock en fin de mois) ; dessous, un tableau
/// de tous les produits avec leurs logos officiels pour le dernier mois fini.
/// </summary>
public partial class ProductionStatsPage : VBoxContainer, IMenuPage
{
    private const string ProducedHint = "Ce que les bâtiments de la commune ont produit, paiement en nature compris (ces produits vont droit aux travailleurs, sans passer par le stockage).";
    private const string UsedHint = "Ce qui est sorti du stockage pour servir : achats des habitants, matières et énergie des bâtiments, matériaux des chantiers.";
    private const string SentHint = "Ce qui est parti vers une autre commune (échanges entre communes), avant la perte de transport.";
    private const string LostInTransitHint = "La part de l'envoyé qui s'est perdue en route vers une autre commune (échanges et lignes d'approvisionnement), selon la distance ; un chemin ou une route la réduit.";
    private const string LostHint = "Ce qui a été perdu dans la commune : faute de place pour stocker, ou parce que le produit s'est périmé.";
    private const string InTransitHint = "Ce qui est parti de la commune vers une autre et n'est pas encore arrivé, à la fin de la période (après la perte en route).";
    private const string StockHint = "Ce qui est dans le stockage de la commune, en vente, à la fin de la période.";
    private const string PriceHint = "Le prix du produit ; pour l'instant, les prix restent les prix de départ.";
    private const string HistoryHint = "Un relevé chaque jour joué, pris par l'interface pendant la partie (un par mois au-delà d'un an) : la courbe montre les flux en moyenne par jour entre deux relevés, et le stock au relevé ; le tableau, le dernier mois fini. Il repart de zéro à chaque lancement.";

    private readonly GameView _view = null!;
    private readonly ProductionHistory _history = null!;
    private readonly CommuneFocus _focus = null!;
    private readonly OptionButton _product = null!;
    // Un produit par entrée du menu ; null pour un intertitre de famille.
    private readonly List<string?> _productIds = [];
    // Les familles repliées dans le tableau, gardées pour la partie (la page est refaite à chaque ouverture du menu).
    private static readonly HashSet<string> CollapsedFamilies = [];
    private readonly LineChart _chart = new();
    private readonly Label _title = null!;
    private readonly Label _hint = null!;
    private readonly Tree _table = null!;
    private string _chartKey = "";
    private string _tableKey = "";

    public ProductionStatsPage(GameView view, ProductionHistory history, CommuneFocus focus)
    {
        _view = view;
        _history = history;
        _focus = focus;
        AddThemeConstantOverride("separation", 8);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = "Produit", CustomMinimumSize = new Vector2(90, 0) });
        _product = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, FitToLongestItem = false };
        _product.ItemSelected += _ =>
        {
            UpdateProductIcon();
            RefreshChart();
        };
        row.AddChild(_product);
        AddChild(row);

        AddChild(_chart);

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 8);
        _title = UiTheme.Title("");
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleRow.AddChild(_title);
        _hint = UiTheme.Caption("");
        titleRow.AddChild(_hint);
        AddChild(titleRow);

        _table = new Tree
        {
            HideRoot = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
            Columns = 9,
        };
        _table.AddThemeColorOverride("font_color", UiTheme.Text);
        _table.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _table.SetColumnTitlesVisible(true);

        var headers = new[]
        {
            ("Produit", "", 180),
            ("Production", ProducedHint, 80),
            ("Utilisé", UsedHint, 70),
            ("Envoyé", SentHint, 70),
            ("Perdu route", LostInTransitHint, 85),
            ("Perdu stock", LostHint, 85),
            ("En route", InTransitHint, 70),
            ("Stock", StockHint, 70),
            ("Prix", PriceHint, 60),
        };

        for (var i = 0; i < headers.Length; i++)
        {
            _table.SetColumnTitle(i, headers[i].Item1);
            _table.SetColumnCustomMinimumWidth(i, headers[i].Item3);
            _table.SetColumnExpand(i, i == 0);
        }

        _table.ItemCollapsed += item =>
        {
            if (item.GetMetadata(1).AsString() is not { Length: > 0 } family)
                return;
            if (item.Collapsed)
                CollapsedFamilies.Add(family);
            else
                CollapsedFamilies.Remove(family);
        };

        _table.ItemSelected += () =>
        {
            if (_table.GetSelected()?.GetMetadata(0).AsString() is { Length: > 0 } id)
            {
                var idx = _productIds.IndexOf(id);
                if (idx >= 0 && idx != _product.Selected)
                {
                    _product.Select(idx);
                    UpdateProductIcon();
                    RefreshChart();
                }
            }
        };

        AddChild(_table);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public ProductionStatsPage() { }

    public void Refresh()
    {
        // Les produits de la commune du menu Communes (au-dessus des onglets).
        var flows = _view.ProductFlows().Where(f => f.CommuneId == _focus.Id).ToList();
        FillProducts(flows);
        RefreshChart();
        RefreshTable(flows, _focus.Id);
    }

    private string? SelectedProduct => _product.Selected >= 0 && _product.Selected < _productIds.Count ? _productIds[_product.Selected] : null;

    private void UpdateProductIcon()
    {
        if (_product.Selected >= 0 && _product.Selected < _product.ItemCount)
            _product.Icon = _product.GetItemIcon(_product.Selected);
        else
            _product.Icon = null;
    }

    /// <summary>
    /// Les produits qui ont servi ou sont en stock, rangés par famille (un intertitre par famille), par nom dans chacune ;
    /// refait quand il en apparaît un, le choix gardé.
    /// </summary>
    private void FillProducts(IReadOnlyList<ProductFlowView> flows)
    {
        var products = flows.Select(f => (f.ProductId, f.ProductName)).Distinct().ToList();
        var groups = ProductFamilies.Group(_view, products, p => p.ProductId, p => p.ProductName);
        var ids = groups.SelectMany(g => g.Items.Select(p => (string?)p.ProductId).Prepend(null)).ToList();
        if (ids.SequenceEqual(_productIds))
            return;

        var selected = SelectedProduct;
        _productIds.Clear();
        _productIds.AddRange(ids);
        _product.Clear();
        foreach (var (family, items) in groups)
        {
            _product.AddSeparator(ProductFamilies.NameOf(family));
            foreach (var (productId, name) in items)
            {
                var icon = IconRegistry.GetProductIcon(productId, 20) ?? IconRegistry.GetProductIcon(name, 20);
                if (icon != null)
                    _product.AddIconItem(icon, name);
                else
                    _product.AddItem(name);
            }
        }
        // Le produit gardé, sinon le premier qui n'est pas un intertitre.
        var index = selected is null ? -1 : _productIds.IndexOf(selected);
        if (index < 0)
            index = _productIds.FindIndex(id => id is not null);
        if (index >= 0)
        {
            _product.Select(index);
            UpdateProductIcon();
        }
    }

    /// <summary>
    /// La courbe : un point par mois fini (la différence entre deux relevés), nommé par le mois. Refaite seulement quand
    /// un relevé s'ajoute ou que le choix change.
    /// </summary>
    private void RefreshChart()
    {
        var snapshots = _history.Snapshots;
        var productId = SelectedProduct;
        var key = $"{snapshots.Count}|{snapshots.LastOrDefault()?.Date}|{_focus.Id}|{productId}";
        if (key == _chartKey)
            return;
        _chartKey = key;

        // Un point par relevé (chaque jour sur les douze derniers mois, chaque mois avant ; décidé le 21 novembre 2026) :
        // les flux en moyenne par jour entre deux relevés, pour que jours et mois se lisent sur la même échelle ; le stock
        // tel qu'il est au relevé.
        var days = new List<string>();
        var figures = new List<(ProductFigures Figures, double Days)>();
        for (var index = 1; index < snapshots.Count && productId is not null; index++)
        {
            days.Add(WorldText.DayLabel(snapshots[index - 1].Date));
            figures.Add((ProductFigures.Of(snapshots[index].Flows, productId, _focus.Id)
                .Since(ProductFigures.Of(snapshots[index - 1].Flows, productId, _focus.Id)),
                System.Math.Max(1, snapshots[index].Date.DayNumber - snapshots[index - 1].Date.DayNumber)));
        }

        LineChart.Series Series(string name, int color, System.Func<ProductFigures, double> value, bool perDay = true) =>
            new(name, UiTheme.SeriesColor(color), figures.Select(f => perDay ? value(f.Figures) / f.Days : value(f.Figures)).ToList());
        _chart.SetData(days,
            [
                Series("Production par jour", 2, f => f.Produced),
                Series("Utilisé par jour", 0, f => f.Used),
                Series("Envoyé par jour", 1, f => f.Sent),
                Series("Perdu en route par jour", 6, f => f.LostInTransit),
                Series("Perdu au stockage par jour", 3, f => f.Lost),
                Series("Stock", 7, f => f.Stock, perDay: false),
            ],
            value => value.ToString("N0"));
    }

    private void RefreshTable(IReadOnlyList<ProductFlowView> flows, string? communeId)
    {
        // Le tableau montre le dernier mois fini : les relevés du 1er du mois.
        var snapshots = _history.MonthStarts;
        var (title, now, before) = snapshots.Count >= 2
            ? ($"Mois de {WorldText.MonthLabel(snapshots[^2].Date)}", snapshots[^1].Flows, snapshots[^2].Flows)
            : ("Depuis le début de la partie", flows, (IReadOnlyList<ProductFlowView>)[]);

        _title.Text = title;
        _hint.Text = snapshots.Count < 2 ? "(le mois se montre à partir du 1er du mois prochain)" : "";

        var products = flows.Select(f => (f.ProductId, f.ProductName)).Distinct().OrderBy(p => p.ProductName).ToList();
        var key = $"{title}|{communeId}|{string.Join(",", products.Select(p => p.ProductId))}|{now.Count}";
        if (key == _tableKey)
            return;
        _tableKey = key;

        _table.Clear();
        var root = _table.CreateItem();

        if (products.Count == 0)
        {
            var empty = _table.CreateItem(root);
            empty.SetText(0, "Aucun produit encore.");
            empty.SetCustomColor(0, UiTheme.Muted);
            return;
        }

        // Une ligne repliable par famille, sans totaux (des quantités de produits différents ne s'additionnent pas).
        var shown = products
            .Select(p => (p.ProductId, p.ProductName,
                Figures: ProductFigures.Of(now, p.ProductId, communeId).Since(ProductFigures.Of(before, p.ProductId, communeId))))
            .Where(p => p.Figures.Produced > 0 || p.Figures.Used > 0 || p.Figures.Sent > 0 || p.Figures.Lost > 0
                || p.Figures.LostInTransit > 0 || p.Figures.InTransit > 0 || p.Figures.Stock > 0)
            .ToList();
        foreach (var (family, items) in ProductFamilies.Group(_view, shown, p => p.ProductId, p => p.ProductName))
        {
            var familyKey = ProductFamilies.KeyOf(family);
            var familyRow = _table.CreateItem(root);
            familyRow.SetText(0, $"{ProductFamilies.NameOf(family)} ({items.Count})");
            familyRow.SetMetadata(0, "");
            familyRow.SetMetadata(1, familyKey);
            familyRow.SetSelectable(0, false);
            familyRow.SetCustomColor(0, UiTheme.Accent);
            familyRow.Collapsed = CollapsedFamilies.Contains(familyKey);
            for (var c = 1; c < _table.Columns; c++)
                familyRow.SetSelectable(c, false);

            foreach (var (productId, name, figures) in items)
                AddProductRow(familyRow, productId, name, figures);
        }
    }

    private void AddProductRow(TreeItem parent, string productId, string name, ProductFigures figures)
    {
        var row = _table.CreateItem(parent);
        row.SetMetadata(0, productId);

        var icon = IconRegistry.GetProductIcon(productId, 20) ?? IconRegistry.GetProductIcon(name, 20);
        row.SetIcon(0, icon);
        row.SetIconMaxWidth(0, 36);
        row.SetText(0, name);

        row.SetText(1, $"{figures.Produced:N0}");
        row.SetText(2, $"{figures.Used:N0}");
        row.SetText(3, $"{figures.Sent:N0}");
        row.SetText(4, $"{figures.LostInTransit:N0}");
        row.SetText(5, $"{figures.Lost:N0}");
        row.SetText(6, $"{figures.InTransit:N0}");
        row.SetText(7, $"{figures.Stock:N0}");
        row.SetText(8, $"{figures.Price:N2}");

        for (var c = 1; c <= 8; c++)
            row.SetTextAlignment(c, HorizontalAlignment.Right);
    }
}
