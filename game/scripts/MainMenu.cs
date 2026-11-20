using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Game.Map;
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

    /// <summary>Un menu et ses onglets ; un menu large prend presque toute la place de la carte (deux colonnes).</summary>
    private sealed record Menu(string Title, Page[] Pages, bool Wide = false);

    private const string HelpText =
        "Espace : pause ou reprise\n" +
        "1 à 4 : vitesse (un jour dure 5 s en ×1)\n" +
        "C : fonder une commune (Structure → Création d'entité politique), puis choisir l'emplacement sur la carte\n" +
        "Échap : fermer le menu ouvert\n" +
        "Clic sur une commune de la carte : le menu Communes sur elle ; sur une préfecture : sa fiche dans le menu Structure\n" +
        "Menu Communes : ◀ ▶ passent d'une commune à l'autre, la carte se centre sur elle\n\n" +
        "Un bâtiment se construit dans Communes → Construire, et se gère dans Communes → Unités de production : " +
        "vente au privé, nationalisation, " +
        "postes ouverts, recette et chauffage. Les impôts se changent par une loi (menu Lois) ; " +
        "Ressources → Revenus montre ce qu'ils rapportent.";

    private const float PanelWidth = 640;

    private Menu[] _menus = [];
    private readonly List<Button> _buttons = [];
    private PanelContainer _panel = null!;
    private Label _panelTitle = null!;
    private TabContainer _tabs = null!;
    private int _open = -1;
    private GameView _view = null!;
    private MapCanvas _map = null!;

    private const string CommunesMenu = "Communes";

    // Au-dessus des onglets du menu Communes : ◀, le nom de la commune choisie, ▶.
    private HBoxContainer _communeBar = null!;
    private Label _communeName = null!;
    private Button _previous = null!;
    private Button _next = null!;

    /// <summary>La commune du menu Communes, gardée d'un onglet à l'autre et quand le menu se ferme.</summary>
    public CommuneFocus Focus { get; } = new();

    /// <summary>Un message pour la barre du bas (le résultat d'une action finie menu fermé, comme un placement sur la carte).</summary>
    public event System.Action<string>? Message;

    /// <summary>
    /// Fonder une commune (Structure → Création d'entité politique) : le formulaire est rempli (nom, commune qui paie,
    /// charte) ; reste à choisir l'emplacement sur la carte.
    /// </summary>
    public event System.Action<string, string, Demos.Domain.Politics.Orientation>? CommuneSiteRequested;

    /// <summary>Ouvre Structure → Création d'entité politique sur le formulaire de la commune (touche C, bouton Camp de base).</summary>
    public void ShowCommuneCreation()
    {
        var structure = System.Array.FindIndex(_menus, m => m.Title == "Structure");
        if (structure < 0)
            return;
        if (_open != structure)
            Toggle(structure);
        _tabs.CurrentTab = 1;
        if (_tabs.GetCurrentTabControl() is EntityCreationPage page)
            page.ShowCommune();
    }

    public void Init(GameView view, PlayerDesk desk, MapCanvas map)
    {
        _view = view;
        _map = map;
        _menus =
        [
            new("Carte",
            [
                new("Filtres", () => new MapPage(map)),
            ]),
            new("Lois",
            [
                new("Code civil", () => new CivilCodePage(view, desk)),
                new("Éditeur de lois", () => new LawCommissionPage(view, desk)),
            ], Wide: true),
            new("Structure",
            [
                new("Gestion des entités politiques", () => new StructurePage(view, desk, ProductionHistory)),
                new("Création d'entité politique", () => new EntityCreationPage(view, desk,
                    (name, payerId, pole) => CommuneSiteRequested?.Invoke(name, payerId, pole))),
                Coming("Organisme", "Les organismes : domaine d'application, pouvoir, postes et fonctionnement."),
            ], Wide: true),
            new("Population",
            [
                new("Vue d'ensemble", () => new PopulationOverviewPage(view)),
                new("Groupes sociaux", () => new PopulationGroupsPage(view)),
                new("Évolution", () => new PopulationHistoryPage(view, History)),
            ], Wide: true),
            // Le menu d'une commune (décidé le 20 novembre 2026) : tous ses onglets montrent la commune choisie au-dessus d'eux
            // (Focus), la même quand on change d'onglet.
            new(CommunesMenu,
            [
                new("Unités de production", () => new BuildingsPage(view, desk, ProductionHistory, Focus)),
                new("Construire", () => new ConstructionPage(view, desk, map, Close, message => Message?.Invoke(message), Focus)),
                new("Transport", () => new TransportPage(view, Focus)),
                new("Chaîne de production", () => new ProductionChainPage(view, ShowBuilding, Focus)),
                new("Statistiques", () => new ProductionStatsPage(view, ProductionHistory, Focus)),
            ], Wide: true),
            new("Recherche",
            [
                Coming("Projets de recherche", "Viser un objectif précis, plus cher et plus lent."),
                Coming("Unités de recherche", "Les bâtiments et organismes qui font avancer la recherche."),
            ]),
            new("Ressources",
            [
                Text("Stocks", () => WorldText.Stocks(view)),
                new("Revenus", () => new RevenuePage(view, desk)),
            ], Wide: true),
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

        // Le menu Communes : la commune choisie au-dessus des onglets, et des flèches pour passer à une autre.
        _communeBar = new HBoxContainer { Visible = false };
        _communeBar.AddThemeConstantOverride("separation", 8);
        _previous = new Button { Text = "◀", FocusMode = FocusModeEnum.None, TooltipText = "Commune précédente", CustomMinimumSize = new Vector2(40, 0) };
        _previous.Pressed += () => Focus.Step(_view.Communes(), -1);
        _communeBar.AddChild(_previous);
        _communeName = UiTheme.Title("");
        _communeName.HorizontalAlignment = HorizontalAlignment.Center;
        _communeName.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _communeBar.AddChild(_communeName);
        _next = new Button { Text = "▶", FocusMode = FocusModeEnum.None, TooltipText = "Commune suivante", CustomMinimumSize = new Vector2(40, 0) };
        _next.Pressed += () => Focus.Step(_view.Communes(), 1);
        _communeBar.AddChild(_next);
        content.AddChild(_communeBar);

        _tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _tabs.GetTabBar().FocusMode = FocusModeEnum.None;
        content.AddChild(_tabs);
    }

    public bool IsOpen => _open >= 0;

    /// <summary>Le relevé mensuel de la population, montré dans Population → Évolution (pris par <see cref="Main"/>).</summary>
    public PopulationHistory History { get; } = new();

    /// <summary>Le relevé mensuel des produits, montré dans Communes → Statistiques (pris par <see cref="Main"/>).</summary>
    public ProductionHistory ProductionHistory { get; } = new();

    public void Close()
    {
        if (_open >= 0)
            _buttons[_open].SetPressedNoSignal(false);
        _open = -1;
        _panel.Visible = false;
        _communeBar.Visible = false;
        SetWide(false);
    }

    /// <summary>Un menu large s'étend sur la place de la carte, dont il laisse un quart visible.</summary>
    private void SetWide(bool wide)
    {
        SizeFlagsHorizontal = wide ? SizeFlags.ExpandFill : SizeFlags.Fill;
        SizeFlagsStretchRatio = wide ? 3 : 1;
        _panel.SizeFlagsHorizontal = wide ? SizeFlags.ExpandFill : SizeFlags.Fill;
    }

    /// <summary>Met à jour la page affichée (l'état de la partie change chaque jour).</summary>
    public void Refresh()
    {
        if (_centerOn is { } pending && Engine.GetProcessFrames() > pending.Frame + 1)
        {
            _centerOn = null;
            _map.CenterOnCommune(pending.Id);
        }
        if (_communeBar.Visible)
        {
            // La commune choisie (la première tant qu'aucune ne l'est), et les flèches s'il y en a plusieurs.
            var communes = _view.Communes();
            var current = Focus.Current(communes);
            _communeName.Text = current?.Name ?? "Aucune commune";
            _previous.Disabled = _next.Disabled = communes.Count < 2;
        }
        if (_open >= 0 && _tabs.GetCurrentTabControl() is IMenuPage page)
            page.Refresh();
    }

    /// <summary>
    /// Ouvre le menu Communes sur une commune (cliquée sur la carte) ; s'il est déjà ouvert, il garde son onglet et passe
    /// à cette commune.
    /// </summary>
    public void ShowCommune(string communeId)
    {
        Focus.Select(communeId);
        var communes = System.Array.FindIndex(_menus, m => m.Title == CommunesMenu);
        if (communes >= 0 && _open != communes)
            Toggle(communes);
    }

    /// <summary>
    /// Un clic sur la carte : une commune ouvre le menu Communes sur elle ; une préfecture, sa fiche dans le menu
    /// Structure.
    /// </summary>
    public void ShowClicked(string structureId)
    {
        if (_view.Communes().Any(c => c.Id == structureId))
            ShowCommune(structureId);
        else
            ShowStructure(structureId);
    }

    /// <summary>
    /// Ouvre le menu Structure sur la fiche d'une structure (une commune ou une préfecture cliquée sur la carte), sans le
    /// refermer s'il est déjà ouvert.
    /// </summary>
    public void ShowStructure(string structureId)
    {
        var structure = System.Array.FindIndex(_menus, m => m.Title == "Structure");
        if (structure < 0)
            return;
        if (_open != structure)
            Toggle(structure);
        _tabs.CurrentTab = 0;
        if (_tabs.GetCurrentTabControl() is StructurePage page)
            page.Show(structureId);
    }

    /// <summary>
    /// Ouvre Communes → Unités de production sur un bâtiment (depuis la Chaîne de production ou le petit schéma) : la
    /// commune du menu devient la sienne.
    /// </summary>
    public void ShowBuilding(string buildingId)
    {
        if (_view.CommuneOf(buildingId) is { } communeId)
            Focus.Select(communeId);
        var production = System.Array.FindIndex(_menus, m => m.Title == CommunesMenu);
        if (production < 0)
            return;
        if (_open != production)
            Toggle(production);
        _tabs.CurrentTab = 0;
        if (_tabs.GetCurrentTabControl() is BuildingsPage page)
            page.Show(buildingId);
    }

    private void Toggle(int menu)
    {
        if (menu == _open)
        {
            Close();
            return;
        }

        // Un menu ouvert sans clic sur son bouton (depuis la carte) : le bouton du menu d'avant se relève aussi.
        if (_open >= 0)
            _buttons[_open].SetPressedNoSignal(false);
        _open = menu;
        _buttons[menu].SetPressedNoSignal(true);
        _panelTitle.Text = _menus[menu].Title;
        _communeBar.Visible = _menus[menu].Title == CommunesMenu;
        SetWide(_menus[menu].Wide);
        BuildPages(_menus[menu]);
        _panel.Visible = true;
        // Le menu Communes s'ouvre sur sa commune (la dernière choisie) : la carte se centre sur elle, à l'image suivante,
        // une fois le menu élargi (avant, la carte a encore toute la largeur et la commune sortirait de sa part visible).
        if (_communeBar.Visible && Focus.Current(_view.Communes()) is { } commune)
            _centerOn = (commune.Id, Engine.GetProcessFrames());
        Refresh();
    }

    // Une commune sur laquelle centrer la carte, et l'image où on l'a demandé.
    private (string Id, ulong Frame)? _centerOn;

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
