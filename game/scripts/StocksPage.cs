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
        };
        _tree.AddThemeColorOverride("font_color", UiTheme.Text);
        _tree.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        AddChild(_tree);
    }

    // Constructeur sans paramètre requis par Godot pour le rechargement C#
    public StocksPage() { }

    public void Refresh()
    {
        _treasuryLabel.Text = $"Trésor de l'État : {_view.TreasuryBalance:N0}";
        _taxLabel.Text = $"Impôt d'État : {_view.StateTaxRate:P2} par jour";

        var communes = _view.Communes();
        var shape = $"{_view.TreasuryBalance}|" + string.Join(";", communes.Select(c =>
            $"{c.Id}:{c.Stored}:{c.StorageCapacity}:" + string.Join(",", c.Stock.Select(s => $"{s.Key}={s.Value}"))));

        if (shape == _lastShape)
            return;
        _lastShape = shape;

        _tree.Clear();
        var root = _tree.CreateItem();

        foreach (var commune in communes)
        {
            var communeItem = _tree.CreateItem(root);
            var communeIcon = IconRegistry.GetBuildingIcon("mairie", "Administration", 20);
            communeItem.SetIcon(0, communeIcon);
            communeItem.SetIconMaxWidth(0, 36);
            communeItem.SetText(0, $"{commune.Name.ToUpperInvariant()}   —   Stockage {commune.Stored:N0} / {commune.StorageCapacity:N0}");
            communeItem.SetCustomColor(0, UiTheme.Accent);

            if (commune.Stock.Count == 0)
            {
                var emptyItem = _tree.CreateItem(communeItem);
                emptyItem.SetText(0, "  (Stockage vide)");
                emptyItem.SetCustomColor(0, UiTheme.Muted);
                continue;
            }

            foreach (var item in commune.Stock.OrderBy(i => i.Key))
            {
                var productItem = _tree.CreateItem(communeItem);
                var productIcon = IconRegistry.GetProductIcon(item.Key, 20);
                productItem.SetIcon(0, productIcon);
                productItem.SetIconMaxWidth(0, 36);

                var stateQty = commune.StateStock.GetValueOrDefault(item.Key);
                var stateDetail = stateQty > 0 ? $"   (dont État : {stateQty:N0})" : "";
                var formattedName = FormatName(item.Key);

                productItem.SetText(0, $"{formattedName} : {item.Value:N0}{stateDetail}");
            }
        }
    }

    private static string FormatName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;
        // Met la première lettre en majuscule proprement
        var clean = raw.Replace('-', ' ');
        return char.ToUpperInvariant(clean[0]) + clean[1..];
    }
}
