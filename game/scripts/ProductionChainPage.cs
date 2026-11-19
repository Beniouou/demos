using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Production → Chaîne de production : le schéma de la chaîne d'une commune (sans ligne, un bâtiment s'approvisionne dans sa
/// propre commune), un produit à suivre, et la fiche du bâtiment choisi d'un clic : sa recette, à changer ici comme dans Unités
/// de production, sa production, ses matières (à plein régime, en stock) et la part de production que ses matières et
/// son énergie ont permise, ses lignes d'approvisionnement (ProductionChainPage.Supply.cs) ; un bouton ouvre ses autres
/// réglages dans Unités de production.
/// </summary>
public partial class ProductionChainPage : VBoxContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private readonly System.Action<string> _manage = null!;
    private readonly CommuneChoice _commune = new(allowAll: false);
    private readonly OptionButton _follow = null!;
    private readonly List<string> _followIds = [];
    private readonly ChainDiagram _diagram = new();
    private readonly RichTextLabel _sheet = null!;
    private readonly HBoxContainer _recipeRow = null!;
    private readonly OptionButton _recipe = null!;
    private readonly Button _manageButton = null!;
    private readonly Label _message = null!;
    private string _chainKey = "";
    private string? _recipeShownFor = "\0";

    public ProductionChainPage(GameView view, PlayerDesk desk, System.Action<string> manage)
    {
        _view = view;
        _desk = desk;
        _manage = manage;
        AddThemeConstantOverride("separation", 8);
        AddChild(_commune.Row());

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = "Suivre", CustomMinimumSize = new Vector2(90, 0), TooltipText = "Garder en vue un produit, ses fournisseurs et ses clients" });
        _follow = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, FitToLongestItem = false };
        row.AddChild(_follow);
        AddChild(row);

        var legend = UiTheme.OwnershipLegend();
        legend.AddChild(new TextureRect { Texture = UiTheme.Square(UiTheme.Accent, 12, 12), StretchMode = TextureRect.StretchModeEnum.KeepCentered });
        legend.AddChild(UiTheme.Caption("cadre orange : des matières ont manqué le dernier jour"));
        legend.AddChild(new Control { CustomMinimumSize = new Vector2(8, 0) });
        legend.AddChild(new TextureRect { Texture = UiTheme.Square(UiTheme.Text, 16, 3), StretchMode = TextureRect.StretchModeEnum.KeepCentered });
        legend.AddChild(UiTheme.Caption("ligne d'approvisionnement"));
        AddChild(legend);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(_diagram);
        AddChild(scroll);
        _diagram.BuildingChosen += _ => ShowRecipe(force: true);

        AddChild(new HSeparator());
        var details = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 6);
        details.AddChild(column);
        AddChild(details);

        _sheet = new HintText
        {
            BbcodeEnabled = true,
            FitContent = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectionEnabled = false,
        };
        _sheet.AddThemeColorOverride("default_color", UiTheme.Text);
        column.AddChild(_sheet);

        _recipeRow = new HBoxContainer();
        _recipeRow.AddThemeConstantOverride("separation", 8);
        _recipeRow.AddChild(new Label { Text = "Recette", CustomMinimumSize = new Vector2(90, 0) });
        _recipe = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, FitToLongestItem = false };
        _recipe.ItemSelected += index =>
        {
            if (_diagram.Selected is not { } id)
                return;
            _message.Text = _desk.SetRecipe(id, (int)index);
            _message.Visible = true;
            _chainKey = "";
            _supplyKey = "";
            ShowRecipe(force: true);
        };
        _recipeRow.AddChild(_recipe);
        column.AddChild(_recipeRow);
        column.AddChild(BuildSupplyPanel());

        _manageButton = new Button { Text = "Gérer dans Unités de production", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        _manageButton.Pressed += () =>
        {
            if (_diagram.Selected is { } id)
                _manage(id);
        };
        column.AddChild(_manageButton);

        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        _message.AddThemeColorOverride("font_color", UiTheme.Accent);
        column.AddChild(_message);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public ProductionChainPage() { }

    public void Refresh()
    {
        _commune.Fill(_view.Communes());
        var chain = _commune.SelectedId is { } communeId ? _view.ProductionChain(communeId) : [];
        FillFollow(chain);
        // Si le bâtiment choisi n'est plus dans la commune montrée (autre commune choisie), plus de fiche.
        if (_diagram.Selected is { } selected && chain.All(b => b.Id != selected))
            _diagram.Selected = null;

        // Le schéma n'est refait que quand ses chiffres ou les choix changent (une fois par jour au plus).
        var links = LinksIn(chain);
        var key = $"{_commune.SelectedId}|{FollowedProduct}|{_view.Today}|{string.Join(",", chain.Select(b => b.Id + b.ProductId))}|{string.Join(",", links)}";
        if (key != _chainKey)
        {
            _chainKey = key;
            _diagram.SetChain(chain, NeedName, FollowedProduct, links);
        }

        var building = chain.FirstOrDefault(b => b.Id == _diagram.Selected);
        var text = building is null ? SheetText.Muted("Cliquez sur un bâtiment du schéma pour voir sa fiche et changer sa recette.") : Describe(building);
        if (text != _sheet.Text)
            _sheet.Text = text;
        _manageButton.Visible = building is not null;
        ShowRecipe(force: false);
        RefreshSupply(building);
    }

    private string? FollowedProduct => _follow.Selected > 0 && _follow.Selected <= _followIds.Count ? _followIds[_follow.Selected - 1] : null;

    /// <summary>« Tous les produits » puis chaque produit fait ou utilisé dans la commune, le choix gardé.</summary>
    private void FillFollow(IReadOnlyList<ChainBuildingView> chain)
    {
        var products = chain.Select(b => (b.ProductId, b.ProductName))
            .Concat(chain.SelectMany(b => b.Inputs.Select(i => (i.ProductId, i.ProductName))))
            .Distinct().OrderBy(p => p.Item2).ToList();
        if (products.Select(p => p.ProductId).SequenceEqual(_followIds))
            return;

        var selected = FollowedProduct;
        _followIds.Clear();
        _followIds.AddRange(products.Select(p => p.ProductId));
        _follow.Clear();
        _follow.AddItem("Tous les produits");
        foreach (var (_, name) in products)
            _follow.AddItem(name);
        _follow.Select(selected is null ? 0 : System.Math.Max(0, _followIds.IndexOf(selected) + 1));
    }

    /// <summary>Le menu des recettes du bâtiment choisi ; caché s'il n'en a qu'une. Refait quand le choix change.</summary>
    private void ShowRecipe(bool force)
    {
        var id = _diagram.Selected;
        if (!force && id == _recipeShownFor)
            return;
        if (id != _recipeShownFor)
            _message.Visible = false;
        _recipeShownFor = id;

        var sheet = id is null ? null : _desk.SheetOf(id);
        _recipeRow.Visible = sheet is { Recipes.Count: > 1 };
        if (sheet is null)
            return;
        _recipe.Clear();
        foreach (var recipe in sheet.Recipes)
            _recipe.AddItem(recipe);
        _recipe.Select(sheet.RecipeIndex);
    }

    private static string Describe(ChainBuildingView building)
    {
        var text = new StringBuilder(SheetText.OwnerSquare(building.Ownership)).Append(' ').Append(SheetText.Heading(building.Name));
        text.AppendLine($"Produit : {SheetText.Escape(building.ProductName)} · {building.ProducedToday:N0} le dernier jour · "
            + $"{building.Workers:N0} travailleurs de production");
        if (building.NeedId is { } need)
            text.AppendLine(SheetText.Muted($"Couvre le besoin des habitants : {NeedName(need).ToLowerInvariant()}."));

        text.Append(SheetText.Section("Matières", "Un bâtiment prend ses matières dans le stockage de sa commune : d'abord ce qu'il y possède, puis il achète le reste. Sans matière ou sans argent, il produit moins, en proportion."));
        if (building.Inputs.Count == 0)
            text.AppendLine(SheetText.Muted("Aucune : il produit à partir de rien d'autre que son travail."));
        foreach (var input in building.Inputs)
            text.AppendLine($"{SheetText.Escape(input.ProductName)} : {input.PerUnit:0.##} par unité · {input.NeededPerDay:N0} par jour à plein régime · "
                + $"{input.Stock:N0} en stock dans la commune" + (input.Reserved > 0 ? $" · {input.Reserved:N0} en réserve (livrés par ses lignes)" : ""));
        if (building.Inputs.Count > 0)
            text.AppendLine(building.SupplyShare < 0.995
                ? $"[color=#{UiTheme.Accent.ToHtml(false)}]Les matières n'ont permis que {building.SupplyShare:P0} de la production le dernier jour.[/color]"
                : "Les matières ont suffi le dernier jour.");
        text.AppendLine($"Énergie couverte : {building.EnergyShare:P0}");
        return text.ToString();
    }

    /// <summary>Le nom d'un besoin des habitants, pour la dernière colonne du schéma.</summary>
    private static string NeedName(string needId) => needId switch
    {
        "Food" => "Se nourrir",
        "Clothing" => "S'habiller",
        "Furniture" => "Se meubler",
        "Housing" => "Se loger",
        "Administration" => "Administration",
        "Security" => "Sécurité",
        "Energy" => "Énergie (bâtiments)",
        "Leisure" => "Se distraire",
        "Comfort" => "Confort",
        _ => needId,
    };
}
