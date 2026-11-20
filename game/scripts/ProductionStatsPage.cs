using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Communes → Statistiques, en lecture : pour la commune du menu (au-dessus des onglets), la courbe mois par mois du produit
/// choisi (production, utilisé, envoyé vers une autre commune, perdu en route, perdu au stockage, et le stock en fin de mois) ; dessous, un tableau
/// de tous les produits pour le dernier mois fini (depuis le début de la partie tant qu'aucun mois n'est fini).
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
    private readonly RichTextLabel _text = null!;
    private string _chartKey = "";

    public ProductionStatsPage(GameView view, ProductionHistory history, CommuneFocus focus)
    {
        _view = view;
        _history = history;
        AddThemeConstantOverride("separation", 8);
        _focus = focus;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = "Produit", CustomMinimumSize = new Vector2(90, 0) });
        _product = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, FitToLongestItem = false };
        row.AddChild(_product);
        AddChild(row);

        AddChild(_chart);
        _text = new HintText
        {
            BbcodeEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectionEnabled = false,
        };
        _text.AddThemeColorOverride("default_color", UiTheme.Text);
        AddChild(_text);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public ProductionStatsPage() { }

    public void Refresh()
    {
        // Les produits de la commune du menu Communes (au-dessus des onglets).
        var flows = _view.ProductFlows().Where(f => f.CommuneId == _focus.Id).ToList();
        FillProducts(flows);
        RefreshChart();
        var text = Describe(flows, _focus.Id);
        // Le texte n'est remis que s'il a changé : sinon il remonterait en haut à chaque image.
        if (text != _text.Text)
            _text.Text = text;
    }

    private string? SelectedProduct => _product.Selected >= 0 && _product.Selected < _productIds.Count ? _productIds[_product.Selected] : null;

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
        foreach (var (_, name) in products)
            _product.AddItem(name);
        if (_productIds.Count > 0)
            _product.Select(System.Math.Max(0, selected is null ? 0 : _productIds.IndexOf(selected)));
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

    private string Describe(IReadOnlyList<ProductFlowView> flows, string? communeId)
    {
        var snapshots = _history.Snapshots;
        // Le dernier mois fini : entre les deux derniers relevés. Sans mois fini, depuis le début de la partie.
        var (title, now, before) = snapshots.Count >= 2
            ? ($"Mois de {WorldText.MonthLabel(snapshots[^2].Date)}", snapshots[^1].Flows, snapshots[^2].Flows)
            : ("Depuis le début de la partie", flows, (IReadOnlyList<ProductFlowView>)[]);

        var text = new StringBuilder(SheetText.Section(title, HistoryHint));
        if (snapshots.Count < 2)
            text.AppendLine(SheetText.Muted("La courbe commence quand le premier mois est fini (le 1er du mois prochain)."));

        var products = flows.Select(f => (f.ProductId, f.ProductName)).Distinct().OrderBy(p => p.ProductName).ToList();
        if (products.Count == 0)
            return text + SheetText.Muted("Aucun produit encore.");

        text.Append("[table=9]");
        foreach (var (header, hint) in new[]
                 {
                     ("Produit", ""), ("Production", ProducedHint), ("Utilisé", UsedHint), ("Envoyé", SentHint),
                     ("Perdu en route", LostInTransitHint), ("Perdu au stockage", LostHint), ("En route", InTransitHint), ("Stock", StockHint),
                     ("Prix", PriceHint),
                 })
            text.Append(PopulationText.Cell(hint == "" ? header : SheetText.Hint(header, hint), header: true));
        foreach (var (productId, name) in products)
        {
            var figures = ProductFigures.Of(now, productId, communeId).Since(ProductFigures.Of(before, productId, communeId));
            if (figures.Produced <= 0 && figures.Used <= 0 && figures.Sent <= 0 && figures.Lost <= 0 && figures.LostInTransit <= 0
                && figures.InTransit <= 0 && figures.Stock <= 0)
                continue;
            text.Append(PopulationText.Cell(SheetText.Escape(name)));
            text.Append(PopulationText.Cell($"{figures.Produced:N0}"));
            text.Append(PopulationText.Cell($"{figures.Used:N0}"));
            text.Append(PopulationText.Cell($"{figures.Sent:N0}"));
            text.Append(PopulationText.Cell($"{figures.LostInTransit:N0}"));
            text.Append(PopulationText.Cell($"{figures.Lost:N0}"));
            text.Append(PopulationText.Cell($"{figures.InTransit:N0}"));
            text.Append(PopulationText.Cell($"{figures.Stock:N0}"));
            text.Append(PopulationText.Cell($"{figures.Price:N2}"));
        }
        text.AppendLine("[/table]");
        return text.ToString();
    }
}
