using System.Collections.Generic;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>Une page de menu qui suit la partie : le menu l'appelle à chaque image tant qu'elle est affichée.</summary>
public interface IMenuPage
{
    void Refresh();
}

/// <summary>
/// Les menus du joueur, d'après le schéma de fonctionnement : une colonne de boutons à gauche, et pour le menu
/// ouvert un panneau avec un onglet par sous-menu. Un nouveau clic sur le même bouton (ou Échap) le ferme.
/// </summary>
public partial class MainMenu : HBoxContainer
{
    /// <summary>Un onglet : son titre et de quoi construire sa page à l'ouverture du menu.</summary>
    private sealed record Page(string Title, System.Func<Control> Create);

    private sealed record Menu(string Title, Page[] Pages);

    private const string HelpText =
        "Espace : pause ou reprise\n" +
        "1 à 4 : vitesse (un jour dure 5 s en ×1)\n" +
        "B : construire 500 places de logement d'État (200 pierre, 100 bois)\n" +
        "C : bâtir un camp de base, qui fonde une nouvelle commune\n" +
        "Échap : fermer le menu ouvert\n\n" +
        "Un bâtiment se gère depuis Production → Unités de production : vente au privé, nationalisation, " +
        "postes ouverts, recette et chauffage. Les impôts se règlent dans Ressources → Impôts.";

    private const float PanelWidth = 640;

    private Menu[] _menus = [];
    private readonly List<Button> _buttons = [];
    private PanelContainer _panel = null!;
    private Label _panelTitle = null!;
    private TabContainer _tabs = null!;
    private int _open = -1;

    public void Init(GameView view, PlayerDesk desk)
    {
        _menus =
        [
            new("Carte",
            [
                Coming("Géographie", "La carte en régions : relief, climat et ressources de chaque région."),
                Coming("Géopolitique", "Les entités politiques sur la carte et leurs frontières."),
                Coming("Production", "Où l'on produit quoi, région par région."),
                Coming("Transit", "Le transport des produits entre les régions."),
                Coming("Ressources nat.", "Les ressources naturelles de chaque région."),
            ]),
            new("Lois",
            [
                Coming("Code civil", "Les lois en vigueur, leur domaine d'application et leurs effets."),
                Coming("Éditeur de lois", "Écrire une loi : type, domaine d'application, règle."),
            ]),
            new("Structure",
            [
                Text("Entité politique", () => WorldText.Communes(view)),
                Coming("Organisme", "Les organismes : domaine d'application, pouvoir, postes et fonctionnement."),
            ]),
            new("Production",
            [
                new("Unités de production", () => new BuildingsPage(desk)),
                Coming("Chaîne de production", "Des matières premières aux produits : quelles recettes, quels bâtiments."),
                Coming("Statistiques", "La production et la consommation de chaque produit dans le temps."),
            ]),
            new("Recherche",
            [
                Coming("Projets de recherche", "Viser un objectif précis, plus cher et plus lent."),
                Coming("Unités de recherche", "Les bâtiments et organismes qui font avancer la recherche."),
            ]),
            new("Ressources",
            [
                Text("Stocks", () => WorldText.Stocks(view)),
                new("Impôts", () => new TaxesPage(view, desk)),
            ]),
            new("Aide", [Text("Commandes", () => HelpText)]),
        ];

        AddThemeConstantOverride("separation", 0);
        MouseFilter = MouseFilterEnum.Ignore;

        var column = new PanelContainer();
        AddChild(column);
        var buttons = new VBoxContainer();
        buttons.AddThemeConstantOverride("separation", 4);
        column.AddChild(buttons);

        var group = new ButtonGroup { AllowUnpress = true };
        for (var index = 0; index < _menus.Length; index++)
        {
            var menu = index;
            var button = new Button
            {
                Text = _menus[index].Title,
                ToggleMode = true,
                ButtonGroup = group,
                FocusMode = FocusModeEnum.None,
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(140, 40),
            };
            button.Pressed += () => Toggle(menu);
            _buttons.Add(button);
            buttons.AddChild(button);
        }

        _panel = new PanelContainer { CustomMinimumSize = new Vector2(PanelWidth, 0), Visible = false };
        AddChild(_panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(content);

        var header = new HBoxContainer();
        _panelTitle = UiTheme.Title("");
        _panelTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_panelTitle);
        var close = new Button { Text = "Fermer", FocusMode = FocusModeEnum.None, TooltipText = "Échap" };
        close.Pressed += Close;
        header.AddChild(close);
        content.AddChild(header);

        _tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _tabs.GetTabBar().FocusMode = FocusModeEnum.None;
        content.AddChild(_tabs);
    }

    public bool IsOpen => _open >= 0;

    public void Close()
    {
        if (_open >= 0)
            _buttons[_open].SetPressedNoSignal(false);
        _open = -1;
        _panel.Visible = false;
    }

    /// <summary>Met à jour la page affichée (l'état de la partie change chaque jour).</summary>
    public void Refresh()
    {
        if (_open >= 0 && _tabs.GetCurrentTabControl() is IMenuPage page)
            page.Refresh();
    }

    private void Toggle(int menu)
    {
        if (menu == _open)
        {
            Close();
            return;
        }

        _open = menu;
        _buttons[menu].SetPressedNoSignal(true);
        _panelTitle.Text = _menus[menu].Title;
        BuildPages(_menus[menu]);
        _panel.Visible = true;
        Refresh();
    }

    private void BuildPages(Menu menu)
    {
        foreach (var child in _tabs.GetChildren())
        {
            _tabs.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var page in menu.Pages)
        {
            _tabs.AddChild(page.Create());
            // Le titre est posé à part : un nom de nœud ne peut pas contenir de point.
            _tabs.SetTabTitle(_tabs.GetTabCount() - 1, page.Title);
        }

        // Un menu sans sous-menu n'affiche pas de barre d'onglets.
        _tabs.TabsVisible = menu.Pages.Length > 1;
        _tabs.CurrentTab = 0;
    }

    private static Page Coming(string title, string description) =>
        new(title, () => new TextPage(null, $"À venir.\n\n{description}"));

    private static Page Text(string title, System.Func<string> describe) =>
        new(title, () => new TextPage(describe));
}
