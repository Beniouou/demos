using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Ressources → Stocks : vue complète des stocks de chaque commune et du trésor de l'État,
/// illustrée avec les logos officiels de chaque ressource et produit.
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

        // ---- Arborescence des stocks par commune ----
        AddChild(UiTheme.Caption("Stocks disponibles dans les communes :"));

        _tree = new Tree
        {
            HideRoot = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
            Columns = 2,
        };
        _tree.SetColumnExpand(0, true);
        _tree.SetColumnExpand(1, false);
        _tree.SetColumnCustomMinimumWidth(1, 220);
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
        // La forme : les communes et leurs produits. Elle seule refait l'arbre ; sinon les chiffres sont mis à jour sur
        // place, ce qui garde la position de la liste et ce qui est replié.
        var shape = string.Join(";", communes.Select(c => $"{c.Id}:" + string.Join(",", c.Stock.Keys.Order())));
        if (shape != _lastShape)
        {
            _lastShape = shape;
            Build(communes);
        }
        UpdateFigures(communes);
    }

    // Ce qui est déplié, gardé pour la partie (la page est refaite à chaque ouverture du menu). Au premier affichage :
    // la première commune et ses familles.
    private static readonly HashSet<string> Expanded = [];
    private static bool _expandedSet;
    private readonly Dictionary<string, TreeItem> _items = [];

    private void Build(IReadOnlyList<CommuneView> communes)
    {
        if (!_expandedSet && communes.Count > 0)
        {
            _expandedSet = true;
            Expanded.Add(CommuneKey(communes[0].Id));
            foreach (var family in System.Enum.GetValues<Demos.Domain.Economy.ProductFamily>())
                Expanded.Add(FamilyKey(communes[0].Id, family));
            Expanded.Add(FamilyKey(communes[0].Id, null));
        }

        _items.Clear();
        _tree.Clear();
        var root = _tree.CreateItem();
        var communeIcon = IconRegistry.GetBuildingIcon("mairie", "Administration", 20);

        foreach (var commune in communes)
        {
            var communeItem = Row(root, CommuneKey(commune.Id), communeIcon);
            communeItem.SetCustomColor(0, UiTheme.Accent);
            communeItem.SetCustomColor(1, UiTheme.Muted);

            if (commune.Stock.Count == 0)
            {
                var emptyItem = _tree.CreateItem(communeItem);
                emptyItem.SetText(0, "Stockage vide");
                emptyItem.SetCustomColor(0, UiTheme.Muted);
                continue;
            }

            var groups = ProductFamilies.Group(_view, commune.Stock.Keys, id => id, id => ProductFamilies.NameOfProduct(_view, id));
            foreach (var (family, products) in groups)
            {
                var familyItem = Row(communeItem, FamilyKey(commune.Id, family), IconRegistry.GetProductIcon(products[0], 18));
                familyItem.SetCustomColor(1, UiTheme.Muted);
                foreach (var productId in products)
                {
                    var productItem = Row(familyItem, ProductKey(commune.Id, productId), IconRegistry.GetProductIcon(productId, 18));
                    productItem.SetText(0, ProductFamilies.NameOfProduct(_view, productId));
                }
            }
        }
    }

    /// <summary>Une ligne de l'arbre : repliée selon ce qui est retenu, chiffres à droite (mis par UpdateFigures).</summary>
    private TreeItem Row(TreeItem parent, string key, Texture2D? icon)
    {
        var item = _tree.CreateItem(parent);
        item.SetMetadata(0, key);
        if (icon is not null)
        {
            item.SetIcon(0, icon);
            item.SetIconMaxWidth(0, 36);
        }
        item.SetTextAlignment(1, HorizontalAlignment.Right);
        // Les lignes de produits n'ont pas d'enfants : seules les communes et les familles se replient.
        item.Collapsed = !Expanded.Contains(key);
        _items[key] = item;
        return item;
    }

    private void UpdateFigures(IReadOnlyList<CommuneView> communes)
    {
        foreach (var commune in communes)
        {
            if (_items.TryGetValue(CommuneKey(commune.Id), out var communeItem))
            {
                Set(communeItem, 0, commune.Name.ToUpperInvariant());
                Set(communeItem, 1, $"Stockage {commune.Stored:N0} / {commune.StorageCapacity:N0}");
            }

            foreach (var (family, products) in ProductFamilies.Group(_view, commune.Stock.Keys, id => id, id => id))
            {
                if (_items.TryGetValue(FamilyKey(commune.Id, family), out var familyItem))
                {
                    Set(familyItem, 0, $"{ProductFamilies.NameOf(family)} ({products.Count})");
                    Set(familyItem, 1, $"{products.Sum(id => commune.Stock[id]):N0}");
                }
                foreach (var productId in products)
                {
                    if (!_items.TryGetValue(ProductKey(commune.Id, productId), out var productItem))
                        continue;
                    var stateQty = commune.StateStock.GetValueOrDefault(productId);
                    Set(productItem, 1, stateQty > 0 ? $"{commune.Stock[productId]:N0}   (État {stateQty:N0})" : $"{commune.Stock[productId]:N0}");
                }
            }
        }
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

    private static string CommuneKey(string communeId) => $"commune:{communeId}";

    private static string FamilyKey(string communeId, Demos.Domain.Economy.ProductFamily? family) =>
        $"famille:{communeId}:{ProductFamilies.KeyOf(family)}";

    private static string ProductKey(string communeId, string productId) => $"produit:{communeId}:{productId}";
}
