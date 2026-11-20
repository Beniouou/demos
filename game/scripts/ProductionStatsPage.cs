using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Communes → Statistiques, en lecture : pour la commune du menu (au-dessus des onglets), la courbe mois par mois du produit
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
    private const string HistoryHint = "Un relevé le 1er de chaque mois, pris par l'interface pendant la partie : il repart de zéro à chaque lancement.";

    private readonly GameView _view = null!;
    private readonly ProductionHistory _history = null!;
    private readonly CommuneFocus _focus = null!;
    private readonly OptionButton _product = null!;
    private readonly List<string> _productIds = [];
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

    /// <summary>Les produits qui ont servi ou sont en stock, par nom ; refait quand il en apparaît un, le choix gardé.</summary>
    private void FillProducts(IReadOnlyList<ProductFlowView> flows)
    {
        var products = flows.Select(f => (f.ProductId, f.ProductName)).Distinct().OrderBy(p => p.ProductName).ToList();
        if (products.Select(p => p.ProductId).SequenceEqual(_productIds))
            return;

        var selected = SelectedProduct;
        _productIds.Clear();
        _productIds.AddRange(products.Select(p => p.ProductId));
        _product.Clear();
        foreach (var (productId, name) in products)
        {
            var icon = IconRegistry.GetProductIcon(productId, 20) ?? IconRegistry.GetProductIcon(name, 20);
            if (icon != null)
                _product.AddIconItem(icon, name);
            else
                _product.AddItem(name);
        }
        if (_productIds.Count > 0)
        {
            _product.Select(System.Math.Max(0, selected is null ? 0 : _productIds.IndexOf(selected)));
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

        var months = new List<string>();
        var figures = new List<ProductFigures>();
        for (var index = 1; index < snapshots.Count && productId is not null; index++)
        {
            months.Add(WorldText.MonthLabel(snapshots[index - 1].Date));
            figures.Add(ProductFigures.Of(snapshots[index].Flows, productId, _focus.Id)
                .Since(ProductFigures.Of(snapshots[index - 1].Flows, productId, _focus.Id)));
        }

        LineChart.Series Series(string name, int color, System.Func<ProductFigures, double> value) =>
            new(name, UiTheme.SeriesColor(color), figures.Select(value).ToList());
        _chart.SetData(months,
            [
                Series("Production", 2, f => f.Produced),
                Series("Utilisé", 0, f => f.Used),
                Series("Envoyé", 1, f => f.Sent),
                Series("Perdu en route", 6, f => f.LostInTransit),
                Series("Perdu au stockage", 3, f => f.Lost),
                Series("Stock en fin de mois", 7, f => f.Stock),
            ],
            value => value.ToString("N0"));
    }

    private void RefreshTable(IReadOnlyList<ProductFlowView> flows, string? communeId)
    {
        var snapshots = _history.Snapshots;
        var (title, now, before) = snapshots.Count >= 2
            ? ($"Mois de {WorldText.MonthLabel(snapshots[^2].Date)}", snapshots[^1].Flows, snapshots[^2].Flows)
            : ("Depuis le début de la partie", flows, (IReadOnlyList<ProductFlowView>)[]);

        _title.Text = title;
        _hint.Text = snapshots.Count < 2 ? "(la courbe commence le 1er du mois prochain)" : "";

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

        foreach (var (productId, name) in products)
        {
            var figures = ProductFigures.Of(now, productId, communeId).Since(ProductFigures.Of(before, productId, communeId));
            if (figures.Produced <= 0 && figures.Used <= 0 && figures.Sent <= 0 && figures.Lost <= 0 && figures.LostInTransit <= 0
                && figures.InTransit <= 0 && figures.Stock <= 0)
                continue;

            var row = _table.CreateItem(root);
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
}
