using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Communes → Chaîne de production, en lecture (décidé le 19 novembre 2026) : le schéma de la chaîne d'une commune,
/// en bandes par besoin, et un produit à suivre. Un clic sur un bâtiment ouvre Communes → Unités de production sur
/// lui : c'est là qu'il se règle (recette, lignes d'approvisionnement…). Un bâtiment qui a une ligne
/// d'approvisionnement avec une autre commune a un cadre extérieur bleu (même préfecture) ou violet (autre préfecture).
/// </summary>
public partial class ProductionChainPage : VBoxContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly System.Action<string> _manage = null!;
    private readonly CommuneFocus _focus = null!;
    private readonly OptionButton _follow = null!;
    private readonly List<string> _followIds = [];
    private readonly ChainDiagram _diagram = new();
    private string _chainKey = "";

    public ProductionChainPage(GameView view, System.Action<string> manage, CommuneFocus focus)
    {
        _view = view;
        _manage = manage;
        AddThemeConstantOverride("separation", 8);
        _focus = focus;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = "Suivre", CustomMinimumSize = new Vector2(90, 0), TooltipText = "Garder en vue un produit, ses fournisseurs et ses clients" });
        _follow = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, FitToLongestItem = false };
        row.AddChild(_follow);
        AddChild(row);

        var legend = UiTheme.OwnershipLegend();
        legend.AddChild(new TextureRect { Texture = UiTheme.Square(UiTheme.Accent, 12, 12), StretchMode = TextureRect.StretchModeEnum.KeepCentered });
        legend.AddChild(UiTheme.Caption("cadre orange : des matières ont manqué le dernier jour"));
        AddChild(legend);
        var lines = UiTheme.RelationLegend();
        lines.AddChild(new TextureRect { Texture = UiTheme.Square(UiTheme.Text, 16, 3), StretchMode = TextureRect.StretchModeEnum.KeepCentered });
        lines.AddChild(UiTheme.Caption("ligne d'approvisionnement"));
        AddChild(lines);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(_diagram);
        AddChild(scroll);
        // Un clic sur un bâtiment : il se règle dans Unités de production.
        _diagram.BuildingChosen += id => _manage(id);

        var hint = UiTheme.Caption("Cliquez sur un bâtiment pour le régler dans Unités de production (recette, lignes d'approvisionnement, postes…).");
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(hint);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public ProductionChainPage() { }

    public void Refresh()
    {
        // La commune du menu Communes, au-dessus des onglets.
        var communeId = _focus.Id;
        var chain = communeId is null ? [] : _view.ProductionChain(communeId);
        var plain = communeId is null ? [] : _view.BuildingsWithoutRecipe(communeId);
        FillFollow(chain);

        // Le schéma n'est refait que quand ses chiffres ou les choix changent (une fois par jour au plus).
        var (links, outside) = Lines(chain, communeId);
        var key = $"{communeId}|{FollowedProduct}|{_view.Today}|{string.Join(",", chain.Select(b => b.Id + b.ProductId))}"
            + $"|{string.Join(",", plain.Select(b => b.Id))}|{string.Join(",", links)}|{string.Join(",", outside)}";
        if (key == _chainKey)
            return;
        _chainKey = key;
        _diagram.SetChain(chain, plain, FlowText.NeedName, FollowedProduct, links, outside);
    }

    /// <summary>
    /// Les lignes entre deux bâtiments de la commune (flèches blanches), et pour chaque bâtiment relié à une autre
    /// commune, la plus lointaine de ces communes (son cadre extérieur).
    /// </summary>
    private (IReadOnlyList<(string From, string To)> Links, IReadOnlyDictionary<string, PlaceRelation> Outside) Lines(
        IReadOnlyList<ChainBuildingView> chain, string? communeId)
    {
        var ids = chain.Select(b => b.Id).ToHashSet();
        var links = new List<(string, string)>();
        var outside = new Dictionary<string, PlaceRelation>();
        foreach (var line in _view.SupplyLines())
        {
            if (ids.Contains(line.SupplierId) && ids.Contains(line.ClientId))
            {
                links.Add((line.SupplierId, line.ClientId));
                continue;
            }
            foreach (var (here, there) in new[] { (line.SupplierId, line.ClientId), (line.ClientId, line.SupplierId) })
            {
                if (!ids.Contains(here) || communeId is null || _view.CommuneOf(there) is not { } otherCommune)
                    continue;
                var relation = _view.RelationBetween(communeId, otherCommune);
                if (relation > outside.GetValueOrDefault(here))
                    outside[here] = relation;
            }
        }
        return (links, outside);
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
}
