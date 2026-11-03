using System.Collections.Generic;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Les menus du joueur, d'après le schéma de fonctionnement : une colonne de boutons à gauche, et pour le menu
/// ouvert un panneau avec un onglet par sous-menu. Un nouveau clic sur le même bouton (ou Échap) le ferme.
/// </summary>
public partial class MainMenu : HBoxContainer
{
    /// <summary>Un onglet : son titre et son texte, ou à défaut ce qu'il contiendra plus tard.</summary>
    private sealed record Page(string Title, string Coming, System.Func<World, string>? Describe = null);

    private sealed record Menu(string Title, Page[] Pages);

    private static readonly Menu[] Menus =
    [
        new("Carte",
        [
            new("Géographie", "La carte en régions : relief, climat et ressources de chaque région."),
            new("Géopolitique", "Les entités politiques sur la carte et leurs frontières."),
            new("Production", "Où l'on produit quoi, région par région."),
            new("Transit", "Le transport des produits entre les régions."),
            new("Ressources nat.", "Les ressources naturelles de chaque région."),
        ]),
        new("Lois",
        [
            new("Code civil", "Les lois en vigueur, leur domaine d'application et leurs effets."),
            new("Éditeur de lois", "Écrire une loi : type, domaine d'application, règle."),
        ]),
        new("Structure",
        [
            new("Entité politique", "", WorldText.Communes),
            new("Organisme", "Les organismes : domaine d'application, pouvoir, postes et fonctionnement."),
        ]),
        new("Production",
        [
            new("Unités de production", "La gestion des bâtiments du joueur. En attendant, la touche A ouvre l'outil d'administration."),
            new("Chaîne de production", "Des matières premières aux produits : quelles recettes, quels bâtiments."),
            new("Statistiques", "La production et la consommation de chaque produit dans le temps."),
        ]),
        new("Recherche",
        [
            new("Projets de recherche", "Viser un objectif précis, plus cher et plus lent."),
            new("Unités de recherche", "Les bâtiments et organismes qui font avancer la recherche."),
        ]),
        new("Ressources", [new("Stocks", "", WorldText.Stocks)]),
        new("Aide", [new("Commandes", "", _ => HelpText)]),
    ];

    private const string HelpText =
        "Espace : pause ou reprise\n" +
        "1 à 4 : vitesse (un jour dure 5 s en ×1)\n" +
        "B : construire 500 places de logement d'État (200 pierre, 100 bois)\n" +
        "C : bâtir un camp de base, qui fonde une nouvelle commune\n" +
        "A : outil d'administration des bâtiments (test)\n" +
        "Échap : fermer le menu ouvert";

    private const float PanelWidth = 640;

    private World _world = null!;
    private readonly List<Button> _buttons = [];
    private PanelContainer _panel = null!;
    private Label _panelTitle = null!;
    private TabContainer _tabs = null!;
    private readonly List<Label> _pageTexts = [];
    private int _open = -1;

    public void Init(World world)
    {
        _world = world;
        AddThemeConstantOverride("separation", 0);
        MouseFilter = MouseFilterEnum.Ignore;

        var column = new PanelContainer();
        AddChild(column);
        var buttons = new VBoxContainer();
        buttons.AddThemeConstantOverride("separation", 4);
        column.AddChild(buttons);

        var group = new ButtonGroup { AllowUnpress = true };
        for (var index = 0; index < Menus.Length; index++)
        {
            var menu = index;
            var button = new Button
            {
                Text = Menus[index].Title,
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
        content.AddChild(_tabs);    }

    public bool IsOpen => _open >= 0;

    public void Close()
    {
        if (_open >= 0)
            _buttons[_open].SetPressedNoSignal(false);
        _open = -1;
        _panel.Visible = false;
    }

    /// <summary>Met à jour les textes du menu ouvert (l'état de la partie change chaque jour).</summary>
    public void Refresh()
    {
        if (_open < 0)
            return;

        var pages = Menus[_open].Pages;
        for (var index = 0; index < pages.Length; index++)
        {
            if (pages[index].Describe is { } describe && index == _tabs.CurrentTab)
                _pageTexts[index].Text = describe(_world);
        }
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
        _panelTitle.Text = Menus[menu].Title;
        BuildPages(Menus[menu]);
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
        _pageTexts.Clear();

        foreach (var page in menu.Pages)
        {
            var scroll = new ScrollContainer { Name = page.Title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            foreach (var side in new[] { "left", "right", "top", "bottom" })
                margin.AddThemeConstantOverride($"margin_{side}", 8);
            scroll.AddChild(margin);

            var text = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            if (page.Describe is null)
            {
                text.Text = $"À venir.\n\n{page.Coming}";
                text.AddThemeColorOverride("font_color", UiTheme.Muted);
            }
            margin.AddChild(text);
            _pageTexts.Add(text);
            _tabs.AddChild(scroll);
            // Le titre est posé à part : un nom de nœud ne peut pas contenir de point.
            _tabs.SetTabTitle(_tabs.GetTabCount() - 1, page.Title);
        }

        // Un menu sans sous-menu n'affiche pas de barre d'onglets.
        _tabs.TabsVisible = menu.Pages.Length > 1;
        _tabs.CurrentTab = 0;
    }
}
