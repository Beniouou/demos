using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Production → Unités de production, en deux boîtes comme le menu Structure. À gauche, les réglages : en haut le choix
/// du bâtiment en descendant la hiérarchie (entité politique → organisme → bâtiment), et en dessous les décisions du
/// joueur sur ce bâtiment (postes ouverts d'un bâtiment public, recette, chauffage, vente au privé, nationalisation).
/// À droite, les informations du bâtiment, en direct.
/// </summary>
public partial class BuildingsPage : HBoxContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private readonly StructurePicker _picker = null!;
    // Le bâtiment dont les réglages sont remplis ; au départ, aucun (même pas « aucun bâtiment »).
    private string? _shownId = "\0";
    private Label _name = null!;
    private Label _info = null!;
    private Label _message = null!;

    private VBoxContainer _jobsSection = null!;
    private SpinBox _jobs = null!;
    private Label _jobsNote = null!;
    private VBoxContainer _recipeSection = null!;
    private OptionButton _recipe = null!;
    private VBoxContainer _heatingSection = null!;
    private OptionButton _heating = null!;
    private VBoxContainer _saleSection = null!;
    private OptionButton _buyer = null!;
    private SpinBox _salePrice = null!;
    private Button _sell = null!;
    private Label _saleNote = null!;
    private readonly List<string> _buyerIds = [];
    private VBoxContainer _nationalizationSection = null!;
    private Label _nationalizationNote = null!;
    private Label _noDecision = null!;

    public BuildingsPage(GameView view, PlayerDesk desk)
    {
        _view = view;
        _desk = desk;
        AddThemeConstantOverride("separation", 12);

        // Boîte de gauche : le choix du bâtiment, de l'entité politique au bâtiment, puis les réglages.
        var settings = Column(this);
        _picker = new StructurePicker(settings, allowAll: false);
        _picker.Changed += ShowSheet;
        // Sous le choix du bâtiment, la légende des carrés de couleur de son propriétaire.
        var legendRow = Row(settings);
        legendRow.AddChild(new Control { CustomMinimumSize = new Vector2(90, 0) });
        legendRow.AddChild(UiTheme.OwnershipLegend());
        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        _message.AddThemeColorOverride("font_color", UiTheme.Accent);
        settings.AddChild(_message);

        BuildJobs(settings);
        BuildRecipe(settings);
        BuildHeating(settings);
        BuildSale(settings);
        BuildNationalization(settings);
        _noDecision = new Label { Text = "Aucun réglage pour ce bâtiment.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _noDecision.AddThemeColorOverride("font_color", UiTheme.Muted);
        settings.AddChild(_noDecision);

        AddChild(new VSeparator());

        // Boîte de droite : les informations du bâtiment choisi.
        var information = Column(this);
        _name = UiTheme.Title("");
        information.AddChild(_name);
        _info = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        information.AddChild(_info);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public BuildingsPage() { }

    public void Refresh()
    {
        // Une commune fondée ou un bâtiment construit : les menus déroulants sont refaits, le choix en cours gardé.
        _picker.Refresh(_view.StructureTree());
        if (SelectedId() != _shownId)
            ShowSheet();

        if (SelectedId() is not { } id || _desk.SheetOf(id) is not { } sheet)
        {
            _name.Text = "";
            _info.Text = "Aucun bâtiment ici.";
            return;
        }

        // Seules les informations suivent la partie ; les réglages en cours de saisie ne bougent pas.
        _info.Text = string.Join("\n", sheet.Info);
        _jobsNote.Text = $"Capacité {sheet.JobCapacity:N0}. Aujourd'hui {sheet.Jobs:N0} postes ouverts"
            + (sheet.JobsSetByPlayer ? ", fixés à la main." : ", réglés automatiquement (licenciements et embauches chaque mois).");
        _saleNote.Text =
            $"Au plus {sheet.MaximumSalePrice:N0} (un an de bénéfice attendu). Coûte {Simulation.SaleLegitimacyCost} de légitimité LI " +
            $"(disponible : {sheet.SaleLegitimacyAvailable:F1}).";
        _sell.Disabled = _buyerIds.Count == 0;
        if (sheet is { Buyout: { } buyout, Expropriation: { } expropriation })
            _nationalizationNote.Text =
                $"Entreprise {(sheet.IsStruggling ? "en difficulté" : "qui paie ses salaires")}.\n" +
                $"Rachat : {buyout.Price:N0} du trésor et {buyout.LegitimacyCost} de légitimité PR.\n" +
                $"Expropriation : sans payer, {expropriation.LegitimacyCost} de légitimité PR.\n" +
                $"(Légitimité PR disponible : {sheet.NationalizationLegitimacyAvailable:F1})";
    }

    private string? SelectedId() => _picker.BuildingId;

    /// <summary>Remplit les réglages pour le bâtiment choisi (à la sélection, et après chaque décision).</summary>
    private void ShowSheet()
    {
        _message.Visible = false;
        _shownId = SelectedId();
        var sheet = _shownId is { } id ? _desk.SheetOf(id) : null;
        _name.Text = sheet?.Name ?? "";

        _jobsSection.Visible = sheet is { IsPublic: true, JobCapacity: > 0 };
        _recipeSection.Visible = sheet?.Recipes.Count > 1;
        _heatingSection.Visible = sheet?.HeatingFuels.Count > 0;
        _saleSection.Visible = sheet?.IsPublic == true;
        _nationalizationSection.Visible = sheet?.IsPublic == false;
        _noDecision.Visible = sheet is not null && !_jobsSection.Visible && !_recipeSection.Visible
            && !_heatingSection.Visible && !_saleSection.Visible && !_nationalizationSection.Visible;
        if (sheet is null)
            return;

        _jobs.MaxValue = sheet.JobCapacity;
        _jobs.Value = sheet.Jobs;
        Fill(_recipe, sheet.Recipes, sheet.RecipeIndex);
        Fill(_heating, sheet.HeatingFuels, sheet.HeatingIndex);
        _buyerIds.Clear();
        _buyerIds.AddRange(sheet.Buyers.Select(b => b.ClassId));
        Fill(_buyer, sheet.Buyers.Select(b => b.Label).ToList(), 0);
        _salePrice.MaxValue = System.Math.Max(0, sheet.MaximumSalePrice);
        _salePrice.Value = sheet.MaximumSalePrice;
        Refresh();
    }

    /// <summary>Après une décision : la fiche reprend l'état du bâtiment, et le résultat s'affiche sous le choix.</summary>
    private void Report(string message)
    {
        ShowSheet();
        _message.Text = message;
        _message.Visible = true;
    }

    private void BuildJobs(VBoxContainer settings)
    {
        _jobsSection = Section(settings, "Postes ouverts (bâtiment public)");
        _jobsNote = Note(_jobsSection);
        var row = Row(_jobsSection);
        _jobs = new SpinBox { MinValue = 0, Step = 10, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(_jobs);
        row.AddChild(Action("Fixer à la main", id => _desk.SetJobs(id, (int)_jobs.Value)));
        row.AddChild(Action("Revenir à l'automatique", _desk.AutomaticJobs));
    }

    private void BuildRecipe(VBoxContainer settings)
    {
        _recipeSection = Section(settings, "Recette");
        var row = Row(_recipeSection);
        _recipe = Option(row);
        row.AddChild(Action("Changer de recette", id => _desk.SetRecipe(id, _recipe.Selected)));
    }

    private void BuildHeating(VBoxContainer settings)
    {
        _heatingSection = Section(settings, "Chauffage");
        var row = Row(_heatingSection);
        _heating = Option(row);
        row.AddChild(Action("Changer de combustible", id => _desk.SetHeating(id, _heating.Selected)));
    }

    private void BuildSale(VBoxContainer settings)
    {
        _saleSection = Section(settings, "Vente au privé");
        _saleNote = Note(_saleSection);
        var buyerRow = Row(_saleSection);
        buyerRow.AddChild(new Label { Text = "Acheteur", CustomMinimumSize = new Vector2(80, 0) });
        _buyer = Option(buyerRow);
        var priceRow = Row(_saleSection);
        priceRow.AddChild(new Label { Text = "Prix", CustomMinimumSize = new Vector2(80, 0) });
        _salePrice = new SpinBox { MinValue = 0, Step = 100, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        priceRow.AddChild(_salePrice);
        _sell = Action("Vendre", id =>
            _buyer.Selected >= 0 && _buyer.Selected < _buyerIds.Count ? _desk.Sell(id, _buyerIds[_buyer.Selected], _salePrice.Value) : null);
        priceRow.AddChild(_sell);
    }

    private void BuildNationalization(VBoxContainer settings)
    {
        _nationalizationSection = Section(settings, "Nationalisation");
        _nationalizationNote = Note(_nationalizationSection);
        var row = Row(_nationalizationSection);
        var buyout = Action("Racheter", id => _desk.Nationalize(id, expropriate: false));
        var expropriate = Action("Exproprier", id => _desk.Nationalize(id, expropriate: true));
        buyout.SizeFlagsHorizontal = expropriate.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(buyout);
        row.AddChild(expropriate);
    }

    /// <summary>Un bouton de décision sur le bâtiment choisi : il appelle le bureau du joueur, puis affiche le résultat.</summary>
    private Button Action(string text, System.Func<string, string?> decide)
    {
        var button = new Button { Text = text, FocusMode = FocusModeEnum.None };
        button.Pressed += () =>
        {
            if (SelectedId() is { } id && decide(id) is { } message)
                Report(message);
        };
        return button;
    }

    /// <summary>Une des deux boîtes : elle défile si son contenu dépasse la hauteur du menu.</summary>
    private static VBoxContainer Column(HBoxContainer page)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        page.AddChild(scroll);
        var margin = UiTheme.Margin(8);
        scroll.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        return column;
    }

    /// <summary>Un menu déroulant de choix, avec son intitulé à gauche.</summary>
    private static OptionButton Choice(VBoxContainer column, string label)
    {
        var row = Row(column);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(80, 0) });
        return Option(row);
    }

    private static VBoxContainer Section(VBoxContainer column, string title)
    {
        // Le séparateur est dans la section : il disparaît avec elle.
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", 6);
        section.AddChild(new HSeparator());
        section.AddChild(UiTheme.Section(title));
        column.AddChild(section);
        return section;
    }

    private static HBoxContainer Row(VBoxContainer section)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        section.AddChild(row);
        return row;
    }

    private static Label Note(VBoxContainer section)
    {
        var note = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        note.AddThemeColorOverride("font_color", UiTheme.Muted);
        section.AddChild(note);
        return note;
    }

    private static OptionButton Option(HBoxContainer row)
    {
        // Le menu ne prend pas la largeur de son plus long choix, et coupe le texte s'il le faut : sinon un long nom de
        // recette élargirait sa boîte aux dépens de l'autre.
        var option = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
            FitToLongestItem = false,
            ClipText = true,
        };
        row.AddChild(option);
        return option;
    }

    private static void Fill(OptionButton option, IReadOnlyList<string> items, int selected)
    {
        option.Clear();
        foreach (var item in items)
            option.AddItem(item);
        if (items.Count > 0)
            option.Select(System.Math.Clamp(selected, 0, items.Count - 1));
    }
}
