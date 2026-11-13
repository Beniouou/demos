using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Politics;
using Godot;
using Rank = Demos.Domain.Population.Rank;

namespace Demos.Game;

/// <summary>
/// Ressources → Revenus, en lecture seule : les impôts se règlent par les lois (brique « Impôt sur la richesse »), plus
/// ici. À gauche, le choix de l'entité politique (l'État ou une commune), son trésor ou sa caisse, ses taux de base et les
/// lois sur l'impôt qui s'y appliquent. À droite, pour chaque classe, son impôt du jour selon les lois et ce qu'elle a
/// payé depuis le début de la saison. Un raccourci prépare une loi d'impôt au niveau de l'entité choisie : le brouillon
/// se règle et se vote dans Lois → Éditeur de lois.
/// </summary>
public partial class RevenuePage : HBoxContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private OptionButton _entity = null!;
    private readonly List<string> _entityIds = [];
    private RichTextLabel _summary = null!;
    private RichTextLabel _classes = null!;
    private Button _taxLaw = null!;
    private Label _message = null!;

    public RevenuePage(GameView view, PlayerDesk desk)
    {
        _view = view;
        _desk = desk;
        AddThemeConstantOverride("separation", 12);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 8);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = "Entité", CustomMinimumSize = new Vector2(90, 0), TooltipText = "Entité politique : l'État ou une commune" });
        _entity = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
            FitToLongestItem = false,
            ClipText = true,
        };
        row.AddChild(_entity);
        left.AddChild(row);
        _entity.ItemSelected += _ => _message.Visible = false;
        _summary = Sheet();
        left.AddChild(_summary);
        _taxLaw = new Button { FocusMode = FocusModeEnum.None, TooltipText = "Crée un brouillon de loi d'impôt à ce niveau, à régler et voter dans Lois → Éditeur de lois." };
        _taxLaw.Pressed += () =>
        {
            if (SelectedEntity() is not { } entityId)
                return;
            _message.Text = _desk.DraftTaxLaw(entityId).Message;
            _message.Visible = true;
        };
        left.AddChild(_taxLaw);
        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        _message.AddThemeColorOverride("font_color", UiTheme.Accent);
        left.AddChild(_message);
        AddChild(left);

        AddChild(new VSeparator());
        _classes = Sheet();
        AddChild(_classes);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public RevenuePage() { }

    public void Refresh()
    {
        var communes = _view.Communes();
        FillEntities(communes);
        var entityId = SelectedEntity();
        var commune = communes.FirstOrDefault(c => c.Id == entityId);
        _taxLaw.Text = commune is null ? "Préparer une loi d'impôt pour l'État" : $"Préparer une loi d'impôt pour {commune.Name}";
        _taxLaw.Disabled = entityId is null;

        _summary.Text = Summary(entityId, commune);
        // L'État prélève dans toutes les communes ; une commune, chez elle seulement.
        _classes.Text = Classes(commune is null ? communes : [commune], commune is not null);
    }

    private string? SelectedEntity() =>
        _entity.Selected >= 0 && _entity.Selected < _entityIds.Count ? _entityIds[_entity.Selected] : null;

    /// <summary>L'État puis chaque commune ; refait quand une commune est fondée, le choix en cours gardé.</summary>
    private void FillEntities(IReadOnlyList<CommuneView> communes)
    {
        var state = _view.StructureTree().FirstOrDefault();
        var entities = new List<(string Id, string Name)>();
        if (state is not null)
            entities.Add((state.Id, state.Name));
        entities.AddRange(communes.Select(c => (c.Id, c.Name)));
        if (entities.Select(e => e.Id).SequenceEqual(_entityIds))
            return;

        var selected = _entity.Selected >= 0 && _entity.Selected < _entityIds.Count ? _entityIds[_entity.Selected] : null;
        _entityIds.Clear();
        _entityIds.AddRange(entities.Select(e => e.Id));
        _entity.Clear();
        foreach (var (_, name) in entities)
            _entity.AddItem(name);
        _entity.Select(System.Math.Max(0, selected is null ? 0 : _entityIds.IndexOf(selected)));
    }

    private string Summary(string? entityId, CommuneView? commune)
    {
        var text = new StringBuilder();
        if (commune is null)
        {
            text.Append(SheetText.Heading("TRÉSOR DE L'ÉTAT"));
            text.AppendLine($"Solde : {_view.TreasuryBalance:N0}");
            text.AppendLine($"Impôt d'État de base : {_view.StateTaxRate:P2} de la richesse par jour, dans toutes les communes");
        }
        else
        {
            text.Append(SheetText.Heading($"CAISSE DE {commune.Name.ToUpperInvariant()}"));
            text.AppendLine($"Solde : {commune.Fund:N0}");
            text.AppendLine($"Impôt communal de base : {commune.TaxRate:P2} de la richesse par jour");
            text.AppendLine(SheetText.Muted($"S'y ajoute l'impôt d'État, {_view.StateTaxRate:P2} par jour, versé au trésor."));
        }

        text.Append(SheetText.Section("Lois sur l'impôt"));
        var laws = entityId is null ? [] : _view.LawsApplyingTo(entityId)
            .Where(l => l.Bricks?.Any(b => b.Id == Bricks.Tax) == true)
            .ToList();
        if (laws.Count == 0)
            text.AppendLine(SheetText.Muted("Aucune : chacun paie le taux de base. Les impôts se changent par une loi (domaine Impôts)."));
        foreach (var law in laws)
        {
            text.AppendLine($"{SheetText.Escape(law.Name)}  {SheetText.Muted($"({law.StructureName})")}  {SheetText.Chips(law.Orientations)}");
            foreach (var brick in law.Bricks!.Where(b => b.Id == Bricks.Tax))
                text.AppendLine($"    {SheetText.Escape(SheetText.Subject(brick.Subject))} : {SheetText.Value(BrickFamily.Modifier, brick.Value)}");
        }

        text.Append(SheetText.Section("Recettes et dépenses"));
        text.AppendLine(SheetText.Muted(
            "À venir : le détail par source (impôts, ventes, salaires, énergie…) demande que le moteur enregistre "
            + "chaque entrée et chaque sortie d'argent."));
        return text.ToString();
    }

    private string Classes(IReadOnlyList<CommuneView> communes, bool withCommuneTax)
    {
        var text = new StringBuilder(SheetText.Heading("IMPÔT DE CHAQUE CLASSE"));
        text.AppendLine(SheetText.Muted(withCommuneTax
            ? "Taux du jour (État + commune), selon les lois, et impôts payés depuis le début de la saison."
            : "Taux d'impôt d'État du jour, selon les lois, et impôts payés depuis le début de la saison (État et commune)."));
        foreach (var commune in communes)
        {
            text.Append(SheetText.Section(commune.Name));
            foreach (var socialClass in commune.Classes.OrderBy(c => c.Rank))
            {
                // Les lois multiplient l'impôt d'État et l'impôt communal de la classe par le même facteur.
                var stateRate = socialClass.TaxFactor * _view.StateTaxRate;
                var rate = withCommuneTax ? stateRate + socialClass.TaxFactor * commune.TaxRate : stateRate;
                var change = socialClass.TaxFactor == 1 ? "" : SheetText.Muted($"  ({socialClass.TaxFactor - 1:+0 %;−0 %} par les lois)");
                text.AppendLine($"{SheetText.Escape(socialClass.Name)}");
                text.AppendLine($"    {RankName(socialClass.Rank)} · {rate:P2} par jour{change} · payé cette saison : {socialClass.SeasonTaxes:N0}");
            }
        }
        return text.ToString();
    }

    private static string RankName(Rank rank) => rank switch
    {
        Rank.Workers => "ouvriers",
        Rank.Intermediate => "professions intermédiaires",
        _ => "cadres",
    };

    private static RichTextLabel Sheet()
    {
        var sheet = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectionEnabled = false,
        };
        sheet.AddThemeColorOverride("default_color", UiTheme.Text);
        return sheet;
    }
}
