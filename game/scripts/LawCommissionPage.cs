using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Politics;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Lois → Éditeur de lois : la commission des lois. À gauche, trois boîtes repliables : les changements votés, qui
/// entrent en vigueur le 1er du mois ; les brouillons ; et le catalogue des modèles, rangé par domaine. À droite, la
/// fiche : les briques, et le coût en légitimité par pôle (payé pour un vote, à payer pour un brouillon). Pas encore de
/// bouton : voter, abroger ou partir d'un modèle viendront ensuite.
/// </summary>
public partial class LawCommissionPage : HBoxContainer, IMenuPage
{
    private static readonly Color GroupColor = new("#6b7280");

    private readonly GameView _view = null!;
    private readonly TreeSheet _schema = null!;

    public LawCommissionPage(GameView view)
    {
        _view = view;
        AddThemeConstantOverride("separation", 12);
        _schema = new TreeSheet { Build = Build, Describe = Describe };
        AddChild(_schema.Tree);
        AddChild(new VSeparator());
        AddChild(_schema.Sheet);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public LawCommissionPage() { }

    public void Refresh()
    {
        var voted = _view.VotedLawChanges();
        var drafts = _view.LawDrafts();
        if (_schema.Selected is null)
        {
            // Au premier affichage, les votes, les brouillons et les modèles sont déroulés, les domaines repliés.
            _schema.Expand("votes");
            _schema.Expand("brouillons");
            _schema.Expand("modeles");
            _schema.Selected = "modeles";
        }

        _schema.Refresh(string.Join(";", voted.Concat(drafts).Select(d => $"{d.Id}:{d.Voted}:{d.Law.Name}")));
    }

    private void Build(TreeItem root)
    {
        var voted = _view.VotedLawChanges();
        var votedItem = _schema.Item(root, "votes", $"Votées, en vigueur le 1er du mois ({voted.Count})", GroupColor);
        foreach (var change in voted)
            AddChange(votedItem, change);

        var drafts = _view.LawDrafts();
        var draftsItem = _schema.Item(root, "brouillons", $"Brouillons ({drafts.Count})", GroupColor);
        foreach (var draft in drafts)
            AddChange(draftsItem, draft);

        var templates = _view.LawTemplates();
        var templatesItem = _schema.Item(root, "modeles", $"Modèles ({templates.Count})", GroupColor);
        var bricks = _view.LawBricks();
        foreach (var domain in _view.LawDomains())
        {
            var inDomain = templates.Where(t => t.Domain == domain.Id).ToList();
            if (inDomain.Count == 0)
                continue;
            var domainItem = _schema.Item(templatesItem, $"domaine:{domain.Id}", $"{domain.Name} ({inDomain.Count})", muted: true);
            foreach (var template in inDomain)
            {
                var item = _schema.Item(domainItem, $"modele:{template.Id}", template.Name);
                TreeSheet.AddPoleChips(item, PolesOf(template, bricks), template.Name);
            }
        }
    }

    private void AddChange(TreeItem parent, LawDraftView change)
    {
        var item = _schema.Item(parent, $"{(change.Voted ? "vote" : "brouillon")}:{change.Id}",
            $"{SheetText.ChangeKind(change.Kind)} : {change.Law.Name}");
        TreeSheet.AddPoleChips(item, change.Law.Orientations, change.Law.Name);
    }

    private string Describe(string key)
    {
        var bricks = _view.LawBricks();
        if (key.StartsWith("vote:") && _view.VotedLawChanges().FirstOrDefault(v => v.Id == key[5..]) is { } voted)
            return DescribeChange(voted, bricks);
        if (key.StartsWith("brouillon:") && _view.LawDrafts().FirstOrDefault(d => d.Id == key[10..]) is { } draft)
            return DescribeChange(draft, bricks);
        if (key.StartsWith("modele:") && _view.LawTemplates().FirstOrDefault(t => t.Id == key[7..]) is { } template)
            return DescribeTemplate(template, bricks);
        if (key.StartsWith("domaine:") && _view.LawDomains().FirstOrDefault(d => d.Id == key[8..]) is { } domain)
            return SheetText.Heading(domain.Name.ToUpperInvariant())
                + (domain.Regalian ? "Domaine régalien : du rôle de l'État. Voté plus bas, il coûte deux fois plus cher.\n" : "")
                + SheetText.Muted("Choisissez un modèle pour voir ses briques.");

        return key switch
        {
            "votes" => SheetText.Heading("CHANGEMENTS VOTÉS")
                + "Les lois votées ce mois-ci, déjà payées en légitimité. Elles entrent en vigueur le 1er du mois prochain.",
            "brouillons" => SheetText.Heading("BROUILLONS")
                + "Les lois en préparation dans la commission. Voter un brouillon le paie tout de suite en légitimité ; "
                + "il entre en vigueur le 1er du mois.",
            "modeles" => SheetText.Heading("MODÈLES")
                + "Des lois déjà remplies, rangées par domaine. Partir d'un modèle crée un brouillon, qu'on vote tel quel "
                + "ou dont on bouge les curseurs.",
            _ => SheetText.Muted("Ce changement n'est plus dans la commission."),
        };
    }

    private static string DescribeChange(LawDraftView change, IReadOnlyList<BrickDefinition> bricks)
    {
        var text = new StringBuilder(CivilCodePage.DescribeLaw(change.Law, bricks, $"{SheetText.ChangeKind(change.Kind)} : {change.Law.Name}"));
        text.Append(SheetText.Section(change.Voted ? "Payé en légitimité" : "Coût s'il était voté maintenant"));
        text.AppendLine(Cost(change.Cost));
        if (change.Voted)
            text.AppendLine("Entre en vigueur le 1er du mois.");
        return text.ToString();
    }

    private string DescribeTemplate(LawTemplate template, IReadOnlyList<BrickDefinition> bricks)
    {
        var text = new StringBuilder(SheetText.Heading($"MODÈLE : {template.Name.ToUpperInvariant()}"));
        text.AppendLine(SheetText.Chips(PolesOf(template, bricks)));
        if (_view.LawDomains().FirstOrDefault(d => d.Id == template.Domain) is { } domain)
            text.AppendLine($"Domaine : {SheetText.Escape(domain.Name)}{(domain.Regalian ? " (régalien)" : "")}");
        text.AppendLine($"Niveau par défaut : {SheetText.Escape(LevelName(template.Level))}");
        if (template.Description is { } description)
            text.AppendLine().AppendLine(SheetText.Escape(description));

        text.Append(SheetText.Section("Briques"));
        foreach (var setting in template.Bricks)
        {
            var definition = bricks.FirstOrDefault(b => b.Id == setting.Brick);
            var family = definition?.Family ?? BrickFamily.Parameter;
            text.AppendLine($"{SheetText.Escape(definition?.Name ?? setting.Brick)} · {SheetText.Escape(SheetText.Subject(setting.Subject))}");
            text.Append($"    {SheetText.Value(family, setting.Value)}");
            if (definition is not null)
                text.Append(SheetText.Muted($"   (par défaut : {SheetText.Value(family, definition.Default)})"));
            text.AppendLine(setting.Locked ? " · verrouillée" : "");
        }

        text.Append(SheetText.Section("Coût"));
        text.AppendLine(SheetText.Muted("Il se calcule sur le brouillon, selon le niveau choisi et les lois déjà en vigueur."));
        return text.ToString();
    }

    /// <summary>
    /// Les pôles qu'un modèle favorise : ceux de chaque brique dans le sens où son curseur s'écarte de la valeur par
    /// défaut. Dans une loi votée, l'écart se mesure depuis la valeur héritée, qui peut changer ces pôles.
    /// </summary>
    private static IReadOnlyList<Orientation> PolesOf(LawTemplate template, IReadOnlyList<BrickDefinition> bricks) =>
        template.Bricks
            .Select(s => (Setting: s, Definition: bricks.FirstOrDefault(b => b.Id == s.Brick)))
            .Where(p => p.Definition is not null && p.Setting.Value != Reference(p.Definition))
            .SelectMany(p => p.Definition!.PolesOf(p.Setting.Subject, raise: p.Setting.Value > Reference(p.Definition)))
            .Distinct()
            .ToList();

    // Un modificateur s'écarte de 0 (les modificateurs s'additionnent) ; les autres briques, de leur valeur par défaut.
    private static double Reference(BrickDefinition brick) => brick.Family == BrickFamily.Modifier ? 0 : brick.Default;

    private static string Cost(IReadOnlyDictionary<Orientation, double> cost) =>
        cost.Count == 0
            ? SheetText.Muted("Rien.")
            : string.Join("   ", cost.Select(c => $"{SheetText.Chip(c.Key)} {c.Key.Code} {c.Value:0.#}"));

    private string LevelName(string structureId) =>
        All(_view.StructureTree()).FirstOrDefault(n => n.Id == structureId)?.Name ?? structureId;

    private static IEnumerable<StructureNode> All(IEnumerable<StructureNode> nodes) =>
        nodes.SelectMany(n => All(n.Children).Prepend(n));
}
