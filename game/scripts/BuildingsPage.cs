using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Production → Unités de production : la liste des bâtiments, et pour celui choisi sa fiche en direct et les décisions
/// du joueur (postes ouverts d'un bâtiment public, recette, chauffage, vente au privé, nationalisation).
/// </summary>
public partial class BuildingsPage : VBoxContainer, IMenuPage
{
    private readonly PlayerDesk _desk = null!;
    private readonly List<string> _ids = [];
    private ItemList _list = null!;
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

    public BuildingsPage(PlayerDesk desk)
    {
        _desk = desk;
        AddThemeConstantOverride("separation", 8);

        _list = new ItemList { CustomMinimumSize = new Vector2(0, 170), FocusMode = FocusModeEnum.None };
        _list.ItemSelected += index => ShowSheet(_ids[(int)index]);
        AddChild(_list);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        var margin = UiTheme.Margin(8);
        scroll.AddChild(margin);
        var sheet = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        sheet.AddThemeConstantOverride("separation", 8);
        margin.AddChild(sheet);

        _name = UiTheme.Title("");
        sheet.AddChild(_name);
        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _message.AddThemeColorOverride("font_color", UiTheme.Accent);
        sheet.AddChild(_message);
        _info = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        sheet.AddChild(_info);

        BuildJobs(sheet);
        BuildRecipe(sheet);
        BuildHeating(sheet);
        BuildSale(sheet);
        BuildNationalization(sheet);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public BuildingsPage() { }

    public void Refresh()
    {
        var buildings = _desk.Buildings();
        if (!buildings.Select(b => b.Id).SequenceEqual(_ids))
            FillList(buildings);

        if (SelectedId() is not { } id || _desk.SheetOf(id) is not { } sheet)
            return;

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

    private void FillList(IReadOnlyList<BuildingEntry> buildings)
    {
        var selected = SelectedId();
        _ids.Clear();
        _list.Clear();
        foreach (var building in buildings)
        {
            _ids.Add(building.Id);
            _list.AddItem($"{building.Name}   —   {building.CommuneName}");
        }

        var index = selected is null ? 0 : System.Math.Max(0, _ids.IndexOf(selected));
        if (_ids.Count > 0)
        {
            _list.Select(index);
            ShowSheet(_ids[index]);
        }
    }

    private string? SelectedId()
    {
        var selected = _list.GetSelectedItems();
        return selected.Length > 0 && selected[0] < _ids.Count ? _ids[selected[0]] : null;
    }

    /// <summary>Remplit les réglages de la fiche pour le bâtiment choisi (à la sélection, et après chaque décision).</summary>
    private void ShowSheet(string id)
    {
        if (_desk.SheetOf(id) is not { } sheet)
            return;

        _name.Text = sheet.Name;
        _message.Visible = false;

        _jobsSection.Visible = sheet.IsPublic && sheet.JobCapacity > 0;
        _jobs.MaxValue = sheet.JobCapacity;
        _jobs.Value = sheet.Jobs;

        _recipeSection.Visible = sheet.Recipes.Count > 1;
        Fill(_recipe, sheet.Recipes, sheet.RecipeIndex);

        _heatingSection.Visible = sheet.HeatingFuels.Count > 0;
        Fill(_heating, sheet.HeatingFuels, sheet.HeatingIndex);

        _saleSection.Visible = sheet.IsPublic;
        _buyerIds.Clear();
        _buyerIds.AddRange(sheet.Buyers.Select(b => b.ClassId));
        Fill(_buyer, sheet.Buyers.Select(b => b.Label).ToList(), 0);
        _salePrice.MaxValue = System.Math.Max(0, sheet.MaximumSalePrice);
        _salePrice.Value = sheet.MaximumSalePrice;

        _nationalizationSection.Visible = !sheet.IsPublic;
        Refresh();
    }

    /// <summary>Après une décision : la fiche reprend l'état du bâtiment, et le résultat s'affiche en haut.</summary>
    private void Report(string id, string message)
    {
        ShowSheet(id);
        _message.Text = message;
        _message.Visible = true;
    }

    private void BuildJobs(VBoxContainer sheet)
    {
        _jobsSection = Section(sheet, "Postes ouverts (bâtiment public)");
        _jobsNote = Note(_jobsSection);
        var row = Row(_jobsSection);
        _jobs = new SpinBox { MinValue = 0, Step = 10, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(_jobs);
        row.AddChild(Action("Fixer à la main", () => SelectedId() is { } id ? _desk.SetJobs(id, (int)_jobs.Value) : null));
        row.AddChild(Action("Revenir à l'automatique", () => SelectedId() is { } id ? _desk.AutomaticJobs(id) : null));
    }

    private void BuildRecipe(VBoxContainer sheet)
    {
        _recipeSection = Section(sheet, "Recette");
        var row = Row(_recipeSection);
        _recipe = Option(row);
        row.AddChild(Action("Changer de recette", () => SelectedId() is { } id ? _desk.SetRecipe(id, _recipe.Selected) : null));
    }

    private void BuildHeating(VBoxContainer sheet)
    {
        _heatingSection = Section(sheet, "Chauffage");
        var row = Row(_heatingSection);
        _heating = Option(row);
        row.AddChild(Action("Changer de combustible", () => SelectedId() is { } id ? _desk.SetHeating(id, _heating.Selected) : null));
    }

    private void BuildSale(VBoxContainer sheet)
    {
        _saleSection = Section(sheet, "Vente au privé");
        _saleNote = Note(_saleSection);
        var buyerRow = Row(_saleSection);
        buyerRow.AddChild(new Label { Text = "Acheteur", CustomMinimumSize = new Vector2(80, 0) });
        _buyer = Option(buyerRow);
        var priceRow = Row(_saleSection);
        priceRow.AddChild(new Label { Text = "Prix", CustomMinimumSize = new Vector2(80, 0) });
        _salePrice = new SpinBox { MinValue = 0, Step = 100, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        priceRow.AddChild(_salePrice);
        _sell = Action("Vendre", () =>
            SelectedId() is { } id && _buyer.Selected >= 0 && _buyer.Selected < _buyerIds.Count
                ? _desk.Sell(id, _buyerIds[_buyer.Selected], _salePrice.Value)
                : null);
        priceRow.AddChild(_sell);
    }

    private void BuildNationalization(VBoxContainer sheet)
    {
        _nationalizationSection = Section(sheet, "Nationalisation");
        _nationalizationNote = Note(_nationalizationSection);
        var row = Row(_nationalizationSection);
        var buyout = Action("Racheter", () => SelectedId() is { } id ? _desk.Nationalize(id, expropriate: false) : null);
        var expropriate = Action("Exproprier", () => SelectedId() is { } id ? _desk.Nationalize(id, expropriate: true) : null);
        buyout.SizeFlagsHorizontal = expropriate.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(buyout);
        row.AddChild(expropriate);
    }

    /// <summary>Un bouton de décision : il appelle le bureau du joueur, puis affiche le résultat.</summary>
    private Button Action(string text, System.Func<string?> decide)
    {
        var button = new Button { Text = text, FocusMode = FocusModeEnum.None };
        button.Pressed += () =>
        {
            if (SelectedId() is { } id && decide() is { } message)
                Report(id, message);
        };
        return button;
    }

    private static VBoxContainer Section(VBoxContainer sheet, string title)
    {
        // Le séparateur est dans la section : il disparaît avec elle.
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", 6);
        section.AddChild(new HSeparator());
        section.AddChild(UiTheme.Section(title));
        sheet.AddChild(section);
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
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None };
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
