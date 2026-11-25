using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Économie → Stocks (décidé le 21 novembre 2026) : le trésor de l'État, puis chaque produit rangé par famille avec son
/// total dans toutes les communes ; déplié, un produit montre sa répartition entre les communes. En bas, le stockage de
/// chaque commune.
/// </summary>
public partial class StocksPage : VBoxContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly Label _treasuryLabel = null!;
    private readonly Label _taxLabel = null!;
    private readonly Tree _tree = null!;
    private string _lastShape = "";

    public StocksPage(GameView view)
    {
        _view = view;
        AddThemeConstantOverride("separation", 10);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        // ---- En-tête : Trésor de l'État ----
        var headerPanel = new PanelContainer();
        var headerMargin = UiTheme.Margin(8);
        headerPanel.AddChild(headerMargin);
        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 12);
        headerMargin.AddChild(headerRow);

        var treasuryIcon = IconRegistry.GetTreasuryIcon(24);
        if (treasuryIcon != null)
        {
            var iconRect = new TextureRect
            {
                Texture = treasuryIcon,
                CustomMinimumSize = new Vector2(treasuryIcon.GetWidth(), treasuryIcon.GetHeight()),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            };
            headerRow.AddChild(iconRect);
        }

        _treasuryLabel = UiTheme.Title("Trésor de l'État : —");
        headerRow.AddChild(_treasuryLabel);

        headerRow.AddChild(new VSeparator());

        _taxLabel = UiTheme.Caption("Impôt d'État : —");
        headerRow.AddChild(_taxLabel);

        AddChild(headerPanel);

        // ---- Les stocks par produit, puis leur répartition entre les communes ----
        _tree = new Tree
        {
            HideRoot = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
            Columns = 4,
        };
        var headers = new[] { ("Produit", 0), ("Total", 110), ("Répartition", 150), ("dont État", 110) };
        for (var i = 0; i < headers.Length; i++)
        {
            _tree.SetColumnTitle(i, headers[i].Item1);
            _tree.SetColumnExpand(i, i == 0);
            if (i > 0)
                _tree.SetColumnCustomMinimumWidth(i, headers[i].Item2);
        }
        _tree.SetColumnTitlesVisible(true);
        _tree.AddThemeColorOverride("font_color", UiTheme.Text);
        _tree.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _tree.ItemCollapsed += OnItemCollapsed;
        AddChild(_tree);
    }

    // Constructeur sans paramètre requis par Godot pour le rechargement C#
    public StocksPage() { }

    public void Refresh()
    {
        _treasuryLabel.Text = $"Trésor de l'État : {_view.TreasuryBalance:N0}";
        _taxLabel.Text = $"Impôt d'État : {_view.StateTaxRate:P2} par jour";

        var communes = _view.Communes();
        var stocks = StocksByProduct(communes);
        // La forme : les produits et, pour chacun, ses communes dans l'ordre de leur part. Elle seule refait l'arbre ;
        // sinon les chiffres sont mis à jour sur place, ce qui garde la position de la liste et ce qui est déplié.
        var shape = string.Join(";", stocks.Select(s => $"{s.Key}:" + string.Join(",", s.Value.Select(c => c.Commune.Id))))
            + "|" + string.Join(",", communes.Select(c => c.Id));
        if (shape != _lastShape)
        {
            _lastShape = shape;
            Build(communes, stocks);
        }
        UpdateFigures(communes, stocks);
    }

    /// <summary>Chaque produit en stock et les communes qui en ont, de la plus grosse part à la plus petite.</summary>
    private static Dictionary<string, List<(CommuneView Commune, double Quantity)>> StocksByProduct(IReadOnlyList<CommuneView> communes) =>
        communes
            .SelectMany(c => c.Stock.Where(s => s.Value > 0).Select(s => (ProductId: s.Key, Commune: c, Quantity: s.Value)))
            .GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Quantity).ThenBy(s => s.Commune.Name)
                .Select(s => (s.Commune, s.Quantity)).ToList());

    // Ce qui est déplié, gardé pour la partie (la page est refaite à chaque ouverture du menu). Au départ : les
    // familles dépliées, les produits et le stockage repliés.
    private static readonly HashSet<string> Expanded = [];
    private static bool _expandedSet;
    private readonly Dictionary<string, TreeItem> _items = [];

    private void Build(IReadOnlyList<CommuneView> communes, Dictionary<string, List<(CommuneView Commune, double Quantity)>> stocks)
    {
        if (!_expandedSet)
        {
            _expandedSet = true;
            foreach (var family in System.Enum.GetValues<Demos.Domain.Economy.ProductFamily>())
                Expanded.Add(FamilyKey(family));
            Expanded.Add(FamilyKey(null));
        }

        _items.Clear();
        _tree.Clear();
        var root = _tree.CreateItem();

        if (stocks.Count == 0)
        {
            var empty = _tree.CreateItem(root);
            empty.SetText(0, "Aucun stock dans les communes.");
            empty.SetCustomColor(0, UiTheme.Muted);
        }

        var groups = ProductFamilies.Group(_view, stocks.Keys, id => id, id => ProductFamilies.NameOfProduct(_view, id));
        foreach (var (family, products) in groups)
        {
            var familyItem = Row(root, FamilyKey(family), null);
            familyItem.SetText(0, $"{ProductFamilies.NameOf(family)} ({products.Count})");
            familyItem.SetCustomColor(0, UiTheme.Accent);
            foreach (var productId in products)
            {
                var productItem = Row(familyItem, ProductKey(productId), IconRegistry.GetProductIcon(productId, 18));
                productItem.SetText(0, ProductFamilies.NameOfProduct(_view, productId));
                productItem.SetCustomColor(2, UiTheme.Muted);
                foreach (var (commune, _) in stocks[productId])
                {
                    var shareItem = Row(productItem, ShareKey(productId, commune.Id), null);
                    shareItem.SetText(0, commune.Name);
                    for (var c = 0; c < _tree.Columns; c++)
                        shareItem.SetCustomColor(c, UiTheme.Muted);
                }
            }
        }

        // Le stockage des communes, en bas : ce qui est stocké sur la capacité.
        var storageItem = Row(root, StorageKey, IconRegistry.GetCategoryIcon("Logistique", 18));
        storageItem.SetText(0, "Stockage des communes");
        storageItem.SetCustomColor(0, UiTheme.Accent);
        var communeIcon = IconRegistry.GetBuildingIcon("mairie", "Administration", 18);
        foreach (var commune in communes)
        {
            var communeItem = Row(storageItem, CommuneStorageKey(commune.Id), communeIcon);
            communeItem.SetText(0, commune.Name);
        }
    }

    /// <summary>Une ligne de l'arbre : dépliée selon ce qui est retenu, chiffres à droite (mis par UpdateFigures).</summary>
    private TreeItem Row(TreeItem parent, string key, Texture2D? icon)
    {
        var item = _tree.CreateItem(parent);
        item.SetMetadata(0, key);
        if (icon is not null)
        {
            item.SetIcon(0, icon);
            item.SetIconMaxWidth(0, 36);
        }
        for (var c = 1; c < _tree.Columns; c++)
            item.SetTextAlignment(c, HorizontalAlignment.Right);
        item.Collapsed = !Expanded.Contains(key);
        _items[key] = item;
        return item;
    }

    private void UpdateFigures(IReadOnlyList<CommuneView> communes, Dictionary<string, List<(CommuneView Commune, double Quantity)>> stocks)
    {
        foreach (var (productId, shares) in stocks)
        {
            var total = shares.Sum(s => s.Quantity);
            if (_items.TryGetValue(ProductKey(productId), out var productItem))
            {
                Set(productItem, 1, $"{total:N0}");
                Set(productItem, 2, shares.Count == 1 ? $"{shares[0].Commune.Name} seule" : $"{shares.Count} communes");
                Set(productItem, 3, $"{shares.Sum(s => s.Commune.StateStock.GetValueOrDefault(productId)):N0}");
            }
            foreach (var (commune, quantity) in shares)
            {
                if (!_items.TryGetValue(ShareKey(productId, commune.Id), out var shareItem))
                    continue;
                var part = total > 0 ? quantity / total : 0;
                Set(shareItem, 1, $"{quantity:N0}");
                Set(shareItem, 2, $"{part:P0}");
                shareItem.SetIcon(2, Bar(part));
                Set(shareItem, 3, $"{commune.StateStock.GetValueOrDefault(productId):N0}");
            }
        }

        foreach (var commune in communes)
        {
            if (!_items.TryGetValue(CommuneStorageKey(commune.Id), out var storageItem))
                continue;
            var fill = commune.StorageCapacity > 0 ? commune.Stored / commune.StorageCapacity : 0;
            Set(storageItem, 1, $"{commune.Stored:N0} / {commune.StorageCapacity:N0}");
            Set(storageItem, 2, $"{fill:P0}");
            storageItem.SetIcon(2, Bar(fill));
        }
    }

    // Une petite barre de part (0 à 100 %), une image par pour cent, gardée.
    private static readonly Dictionary<int, Texture2D> Bars = [];

    private static Texture2D Bar(double part)
    {
        var percent = (int)System.Math.Round(System.Math.Clamp(part, 0, 1) * 100);
        if (Bars.TryGetValue(percent, out var cached))
            return cached;
        const int width = 60, height = 6;
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(UiTheme.Muted with { A = 0.35f });
        var filled = (int)System.Math.Round(width * percent / 100.0);
        if (filled > 0)
            image.FillRect(new Rect2I(0, 0, filled, height), UiTheme.Accent);
        var texture = ImageTexture.CreateFromImage(image);
        Bars[percent] = texture;
        return texture;
    }

    private static void Set(TreeItem item, int column, string text)
    {
        if (item.GetText(column) != text)
            item.SetText(column, text);
    }

    private void OnItemCollapsed(TreeItem item)
    {
        if (item.GetMetadata(0).AsString() is not { Length: > 0 } key)
            return;
        if (item.Collapsed)
            Expanded.Remove(key);
        else
            Expanded.Add(key);
    }

    private const string StorageKey = "stockage";

    private static string CommuneStorageKey(string communeId) => $"stockage:{communeId}";

    private static string FamilyKey(Demos.Domain.Economy.ProductFamily? family) => $"famille:{ProductFamilies.KeyOf(family)}";

    private static string ProductKey(string productId) => $"produit:{productId}";

    private static string ShareKey(string productId, string communeId) => $"part:{productId}:{communeId}";
}
