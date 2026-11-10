using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Production → Construire. À gauche, la commune où bâtir, puis le catalogue des bâtiments constructibles rangé par
/// catégorie (boîtes repliables). À droite, la fiche du type choisi : ce qu'il coûte, ce qui peut le payer dans la
/// commune (réserve de chantier, stock de l'État, achat avec le trésor), ce qu'il apporte ; le bouton pour lancer le
/// chantier, et les chantiers en cours dans la commune. La construction passe par le bureau du joueur.
/// </summary>
public partial class ConstructionPage : HBoxContainer, IMenuPage
{
    private static readonly Color GroupColor = new("#6b7280");

    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private readonly TreeSheet _schema = null!;
    private OptionButton _commune = null!;
    private readonly List<string> _communeIds = [];
    private Button _build = null!;
    private Label _message = null!;
    private Label _sites = null!;

    public ConstructionPage(GameView view, PlayerDesk desk)
    {
        _view = view;
        _desk = desk;
        AddThemeConstantOverride("separation", 12);
        _schema = new TreeSheet { Build = Build, Describe = Describe };

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 8);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = "Commune", CustomMinimumSize = new Vector2(90, 0) });
        _commune = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
            FitToLongestItem = false,
            ClipText = true,
        };
        _commune.ItemSelected += _ =>
        {
            _message.Visible = false;
            _schema.Invalidate();
        };
        row.AddChild(_commune);
        left.AddChild(row);
        left.AddChild(_schema.Tree);
        AddChild(left);

        AddChild(new VSeparator());

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 8);
        right.AddChild(_schema.Sheet);
        _build = new Button { Text = "Lancer le chantier dans cette commune", FocusMode = FocusModeEnum.None };
        _build.Pressed += BuildSelected;
        right.AddChild(_build);
        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        _message.AddThemeColorOverride("font_color", UiTheme.Accent);
        right.AddChild(_message);
        right.AddChild(new HSeparator());
        right.AddChild(UiTheme.Section("Chantiers en cours dans la commune"));
        _sites = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        right.AddChild(_sites);
        AddChild(right);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public ConstructionPage() { }

    public void Refresh()
    {
        FillCommunes();
        var types = _desk.BuildableTypes();
        if (_schema.Selected is null)
        {
            // Au premier affichage, les catégories sont repliées et rien n'est choisi : la fiche invite à choisir.
            _schema.Selected = "aide";
        }

        _schema.Refresh($"{SelectedCommune()}|{string.Join(";", types.Select(t => t.Id))}");
        _build.Disabled = SelectedCommune() is null || !_schema.Selected!.StartsWith("type:");

        var sites = _view.ConstructionSites().Where(s => s.CommuneId == SelectedCommune()).ToList();
        _sites.Text = sites.Count == 0
            ? "Aucun."
            : string.Join("\n", sites.Select(s => $"{s.Name} : encore {s.DaysLeft} jour(s)"));
    }

    private void FillCommunes()
    {
        var communes = _view.Communes();
        if (communes.Select(c => c.Id).SequenceEqual(_communeIds))
            return;

        var selected = SelectedCommune();
        _communeIds.Clear();
        _communeIds.AddRange(communes.Select(c => c.Id));
        _commune.Clear();
        foreach (var commune in communes)
            _commune.AddItem(commune.Name);
        if (_communeIds.Count > 0)
            _commune.Select(System.Math.Max(0, selected is null ? 0 : _communeIds.IndexOf(selected)));
        _schema.Invalidate();
    }

    private string? SelectedCommune() =>
        _commune.Selected >= 0 && _commune.Selected < _communeIds.Count ? _communeIds[_commune.Selected] : null;

    private void BuildSelected()
    {
        if (SelectedCommune() is not { } communeId || _schema.Selected is not { } key || !key.StartsWith("type:"))
            return;
        _message.Text = _desk.Build(key["type:".Length..], communeId);
        _message.Visible = true;
    }

    // ---- Le catalogue ----------------------------------------------------------------------------------------------

    private void Build(TreeItem root)
    {
        foreach (var category in _desk.BuildableTypes().GroupBy(t => t.Category))
        {
            var categoryItem = _schema.Item(root, $"categorie:{category.Key}", $"{category.Key} ({category.Count()})", GroupColor);
            foreach (var type in category.OrderBy(t => t.Level).ThenBy(t => t.Name))
                _schema.Item(categoryItem, $"type:{type.Id}", $"{type.Name}   (niveau {type.Level})", muted: !type.IsFunctional);
        }
    }

    private string Describe(string key)
    {
        if (key.StartsWith("type:") && _desk.BuildableTypes().FirstOrDefault(t => t.Id == key[5..]) is { } type)
            return DescribeType(type);
        if (key.StartsWith("categorie:"))
            return SheetText.Heading(key["categorie:".Length..].ToUpperInvariant())
                + SheetText.Muted("Choisissez un bâtiment de cette catégorie pour voir ce qu'il coûte et ce qu'il apporte.");
        return SheetText.Heading("CONSTRUIRE")
            + "Choisissez la commune, puis un bâtiment dans le catalogue. Le chantier est payé tout de suite : d'abord la "
            + "réserve de chantier de la commune, puis le stock de l'État, puis des achats avec le trésor. Le bâtiment arrive "
            + "à la fin du chantier.\n\n"
            + SheetText.Muted("En gris : les bâtiments qui n'ont pas encore d'effet dans le jeu (ni emplois, ni places, ni stockage).");
    }

    private string DescribeType(BuildableType type)
    {
        var text = new StringBuilder(SheetText.Heading(type.Name.ToUpperInvariant()));
        text.AppendLine($"{SheetText.Escape(type.Category)} · niveau {type.Level} · liste {type.Tier}");
        if (!string.IsNullOrWhiteSpace(type.Description))
            text.AppendLine(SheetText.Muted(type.Description));
        if (!type.IsFunctional)
            text.AppendLine($"[color=#{UiTheme.Danger.ToHtml(false)}]Ce bâtiment n'a pas encore d'effet dans le jeu.[/color]");

        text.Append(SheetText.Section("Coût"));
        text.AppendLine($"{type.MaterialQuantity:N0} {SheetText.Escape(type.MaterialName)}, {type.Days} jours de chantier");

        if (SelectedCommune() is { } communeId)
        {
            var available = _desk.AvailabilityOf(communeId, type.MaterialId);
            var toBuy = System.Math.Max(0, type.MaterialQuantity - available.Reserve - available.StateStock);
            var price = toBuy * available.Price;
            var possible = available.OnSale >= toBuy && available.Treasury >= price;
            text.Append(SheetText.Section("De quoi payer dans la commune"));
            text.AppendLine($"Réserve de chantier {available.Reserve:N0} · stock de l'État {available.StateStock:N0} · en vente {available.OnSale:N0}");
            text.AppendLine(toBuy <= 0
                ? "Tout est déjà là : rien à acheter."
                : $"À acheter : {toBuy:N0}, pour {price:N0} (trésor : {available.Treasury:N0}).");
            var verdict = possible ? "Le chantier peut être lancé." : "Pas assez de matériaux en vente, ou pas assez d'argent au trésor.";
            text.AppendLine($"[color=#{(possible ? UiTheme.Accent : UiTheme.Danger).ToHtml(false)}]{verdict}[/color]");
        }

        text.Append(SheetText.Section("Ce qu'il apporte"));
        var gains = new List<string>();
        if (type.Jobs > 0)
            gains.Add($"{type.Jobs:N0} emplois");
        if (type.HousingPlaces > 0)
            gains.Add($"{type.HousingPlaces:N0} places de logement");
        if (type.SchoolPlaces > 0)
            gains.Add($"{type.SchoolPlaces:N0} places d'élèves");
        if (type.StorageCapacity > 0)
            gains.Add($"stockage de {type.StorageCapacity:N0}");
        text.AppendLine(gains.Count == 0 ? SheetText.Muted("Rien pour l'instant.") : string.Join(" · ", gains));
        if (type.Recipes.Count > 0)
            text.AppendLine($"Recettes : {SheetText.Escape(string.Join(" ; ", type.Recipes))}");
        return text.ToString();
    }
}
