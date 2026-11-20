using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Game.Map;
using Godot;

namespace Demos.Game;

/// <summary>
/// Communes → Construire, dans la commune du menu (au-dessus des onglets). À gauche, le catalogue des bâtiments
/// constructibles rangé par catégorie (boîtes repliables). À droite, la fiche du type choisi : ce qu'il coûte, ce qui peut le payer dans la
/// commune (réserve de chantier, stock de l'État, achat avec le trésor), ce qu'il apporte ; le bouton pour lancer le
/// chantier, et les chantiers en cours dans la commune. Un bâtiment d'extraction (camp de bûcheron, ferme, carrière, mine)
/// se pose sur la carte : le bouton ferme le menu et passe la carte en mode placement. La construction passe par le
/// bureau du joueur.
/// </summary>
public partial class ConstructionPage : HBoxContainer, IMenuPage
{
    private static readonly Color GroupColor = new("#6b7280");

    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private readonly TreeSheet _schema = null!;
    private readonly MapCanvas _map = null!;
    private readonly System.Action _closeMenu = null!;
    private readonly System.Action<string> _report = null!;
    private readonly CommuneFocus _focus = null!;
    private string? _shownCommune;
    private Button _build = null!;
    private Label _message = null!;
    private Label _sites = null!;

    public ConstructionPage(GameView view, PlayerDesk desk, MapCanvas map, System.Action closeMenu, System.Action<string> report, CommuneFocus focus)
    {
        _view = view;
        _desk = desk;
        _map = map;
        _closeMenu = closeMenu;
        _report = report;
        _focus = focus;
        AddThemeConstantOverride("separation", 12);
        _schema = new TreeSheet { Build = Build, Describe = Describe };

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 8);
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
        _build.Text = SelectedType() is { } typeId && _view.ExtractionZonesFor(typeId).Count > 0
            ? "Choisir l'emplacement sur la carte"
            : "Lancer le chantier dans cette commune";

        var sites = _view.ConstructionSites().Where(s => s.CommuneId == SelectedCommune()).ToList();
        _sites.Text = sites.Count == 0
            ? "Aucun."
            : string.Join("\n", sites.Select(s => $"{s.Name} : encore {s.DaysLeft} jour(s)"));
    }

    /// <summary>Une autre commune choisie au-dessus des onglets : le message de la précédente s'efface.</summary>
    private void FillCommunes()
    {
        if (SelectedCommune() == _shownCommune)
            return;
        _shownCommune = SelectedCommune();
        _message.Visible = false;
        _schema.Invalidate();
    }

    /// <summary>La commune où bâtir : celle du menu Communes, au-dessus des onglets.</summary>
    private string? SelectedCommune() => _focus.Id;

    private string? SelectedType() =>
        _schema.Selected is { } key && key.StartsWith("type:") ? key["type:".Length..] : null;

    /// <summary>
    /// Lance le chantier ; pour un bâtiment d'extraction, ferme le menu et passe la carte en mode placement : le chantier
    /// est lancé au clic sur un emplacement permis, et son résultat s'affiche dans la barre du bas.
    /// </summary>
    private void BuildSelected()
    {
        if (SelectedCommune() is not { } communeId || SelectedType() is not { } typeId)
            return;

        var zones = _view.ExtractionZonesFor(typeId);
        if (zones.Count == 0)
        {
            _message.Text = _desk.Build(typeId, communeId);
            _message.Visible = true;
            return;
        }

        var name = _desk.BuildableTypes().FirstOrDefault(t => t.Id == typeId)?.Name ?? typeId;
        var place = string.Join(" ou ", zones.Select(Demos.Domain.Map.ExtractionSites.ZoneName));
        _closeMenu();
        _map.BeginPlacement(new MapCanvas.PlacementRequest(
            $"{name}, sur {place}, à {Demos.Domain.Map.MapScale.Format(_view.ExtractionMaxDistance)} de la commune au plus",
            zones,
            communeId,
            _view.ExtractionMaxDistance,
            position => _view.ExtractionSiteProblem(typeId, communeId, position),
            position => _report(_desk.Build(typeId, communeId, position))));
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
            + "Choisissez un bâtiment dans le catalogue : il sera bâti dans la commune affichée au-dessus des onglets (◀ ▶ pour en changer). Le chantier est payé tout de suite : d'abord la "
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

        var zones = _view.ExtractionZonesFor(type.Id);
        if (zones.Count > 0)
        {
            text.Append(SheetText.Section("Emplacement"));
            text.AppendLine($"Sur la carte, sur {string.Join(" ou ", zones.Select(Demos.Domain.Map.ExtractionSites.ZoneName))}, "
                + $"à {Demos.Domain.Map.MapScale.Format(_view.ExtractionMaxDistance)} de la commune au plus. Le bouton ouvre la carte pour le choisir.");
        }

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
