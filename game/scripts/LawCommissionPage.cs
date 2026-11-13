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
/// fiche : les briques, et le coût en légitimité par pôle (payé pour un vote, à payer pour un brouillon). Sous la fiche,
/// les décisions, qui passent par le bureau du joueur : créer un brouillon depuis un modèle (au niveau choisi) ou une loi
/// vierge (éditeur libre : nom, domaine, niveau) ; dans un brouillon, renommer la loi, ajouter ou retirer des briques,
/// choisir leur sujet, régler curseurs et cases « verrouillée », le voter ou le supprimer ; annuler un vote. Amender ou
/// abroger une loi en vigueur se fait depuis le code civil.
/// </summary>
public partial class LawCommissionPage : HBoxContainer, IMenuPage
{
    private static readonly Color GroupColor = new("#6b7280");

    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private readonly TreeSheet _schema = null!;
    private readonly VBoxContainer _actions = null!;
    private readonly Label _message = null!;
    // La clé dont les décisions sont affichées : elles ne sont refaites que quand le choix change (sinon un curseur
    // perdrait la souris pendant qu'on le tire).
    private string? _actionsKey;

    public LawCommissionPage(GameView view, PlayerDesk desk)
    {
        _view = view;
        _desk = desk;
        AddThemeConstantOverride("separation", 12);
        _schema = new TreeSheet { Build = Build, Describe = Describe };
        AddChild(_schema.Tree);
        AddChild(new VSeparator());

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 8);
        right.AddChild(_schema.Sheet);
        _actions = new VBoxContainer();
        _actions.AddThemeConstantOverride("separation", 6);
        right.AddChild(_actions);
        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        _message.AddThemeColorOverride("font_color", UiTheme.Accent);
        right.AddChild(_message);
        AddChild(right);
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

        // La forme ne doit jamais être vide : TreeSheet part d'une forme vide et ne construirait pas le schéma (sans
        // brouillon ni vote, la page restait blanche).
        _schema.Refresh($"commission|{_view.LawTemplates().Count}|" + string.Join(";", voted.Concat(drafts).Select(d =>
            $"{d.Id}:{d.Voted}:{d.Law.Name}:{string.Join(",", d.Law.Orientations.Select(o => o.Code))}")));
        if (_schema.Selected != _actionsKey)
        {
            _message.Visible = false;
            BuildActions();
        }
    }

    // ---- Les décisions -----------------------------------------------------------------------------------------------

    private void BuildActions()
    {
        _actionsKey = _schema.Selected;
        foreach (var child in _actions.GetChildren())
        {
            _actions.RemoveChild(child);
            child.QueueFree();
        }

        var key = _actionsKey ?? "";
        if (key == "vierge")
            BlankActions();
        else if (key.StartsWith("modele:") && _view.LawTemplates().FirstOrDefault(t => t.Id == key[7..]) is { } template)
            TemplateActions(template);
        else if (key.StartsWith("brouillon:") && _view.LawDrafts().FirstOrDefault(d => d.Id == key[10..]) is { } draft)
            DraftActions(draft);
        else if (key.StartsWith("vote:") && _view.VotedLawChanges().Any(v => v.Id == key[5..]))
            _actions.AddChild(ActionButton("Annuler le vote (la légitimité est rendue)", () => Report(_desk.CancelVote(key[5..]), $"brouillon:{key[5..]}")));
        _actions.Visible = _actions.GetChildCount() > 0;
    }

    private void TemplateActions(LawTemplate template)
    {
        _actions.AddChild(new HSeparator());
        var level = LevelMenu(template.Level, null);
        var create = ActionButton("Créer un brouillon depuis ce modèle", () =>
        {
            var answer = _desk.DraftFromTemplate(template.Id, level.levelId());
            Report(answer.Message, answer.Id is { } id ? $"brouillon:{id}" : null);
        });
        _actions.AddChild(create);
    }

    /// <summary>Éditeur libre : le nom, le domaine et le niveau d'une loi vierge, puis le bouton qui crée son brouillon.</summary>
    private void BlankActions()
    {
        _actions.AddChild(new HSeparator());
        var name = NameField("Nouvelle loi", null);
        var domains = _view.LawDomains();
        var domain = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, FitToLongestItem = false };
        foreach (var d in domains)
            domain.AddItem(d.Regalian ? $"{d.Name} (régalien)" : d.Name);
        _actions.AddChild(Labeled("Domaine", domain));
        var level = LevelMenu(_view.StructureTree().FirstOrDefault()?.Id ?? "", null);
        _actions.AddChild(ActionButton("Créer le brouillon", () =>
        {
            if (domain.Selected < 0 || level.levelId() is not { } levelId)
                return;
            var answer = _desk.DraftBlank(name.Text, domains[domain.Selected].Id, levelId);
            Report(answer.Message, answer.Id is { } id ? $"brouillon:{id}" : null);
        }));
    }

    private void DraftActions(LawDraftView draft)
    {
        _actions.AddChild(new HSeparator());
        if (draft.Kind != LawChangeKind.Repeal)
            NameField(draft.Law.Name, text => _desk.RenameDraft(draft.Id, text));
        if (draft.Kind == LawChangeKind.Enact)
            LevelMenu(draft.Law.StructureId, id => Show(_desk.SetDraftLevel(draft.Id, id)));

        if (draft.Kind != LawChangeKind.Repeal)
        {
            var bricks = _view.LawBricks();
            var index = 0;
            foreach (var brick in draft.Law.Bricks ?? [])
            {
                if (bricks.FirstOrDefault(b => b.Id == brick.Id) is { } definition)
                    BrickEditor(draft.Id, index, brick, definition);
                index++;
            }
            AddBrickRow(draft, bricks);
        }

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(ActionButton($"Voter (en vigueur {_desk.NextEnactmentDay()})", () =>
        {
            var message = _desk.Vote(draft.Id);
            Report(message, _view.VotedLawChanges().Any(v => v.Id == draft.Id) ? $"vote:{draft.Id}" : _schema.Selected);
        }));
        row.AddChild(ActionButton("Supprimer le brouillon", () => Report(_desk.DeleteDraft(draft.Id), "brouillons")));
        _actions.AddChild(row);
    }

    /// <summary>Le menu des briques du domaine de la loi et le bouton qui en ajoute une (trois au plus).</summary>
    private void AddBrickRow(LawDraftView draft, IReadOnlyList<BrickDefinition> bricks)
    {
        var inDomain = bricks.Where(b => b.Domain == draft.Law.Domain).ToList();
        var full = (draft.Law.Bricks?.Count ?? 0) >= LawCatalogue.MaxBricksPerLaw;
        var menu = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, FitToLongestItem = false, Disabled = full };
        foreach (var brick in inDomain)
            menu.AddItem(brick.Name);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(menu);
        var add = ActionButton(full ? $"{LawCatalogue.MaxBricksPerLaw} briques au plus" : "Ajouter la brique", () =>
        {
            if (menu.Selected >= 0)
                Report(_desk.AddDraftBrick(draft.Id, inDomain[menu.Selected].Id), _schema.Selected);
        });
        add.Disabled = full || inDomain.Count == 0;
        row.AddChild(add);
        _actions.AddChild(row);
    }

    /// <summary>
    /// Une brique d'un brouillon : son nom, le menu de son sujet et le bouton pour la retirer ; puis son curseur (ou la
    /// case « autorisé » d'une autorisation) et sa case « verrouillée ».
    /// </summary>
    private void BrickEditor(string draftId, int index, BrickView brick, BrickDefinition definition)
    {
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        header.AddChild(new Label { Text = definition.Name, CustomMinimumSize = new Vector2(180, 0), TooltipText = definition.Description ?? "", MouseFilter = MouseFilterEnum.Pass });
        var choices = _desk.SubjectChoices(definition.Id);
        var subject = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, FitToLongestItem = false };
        foreach (var choice in choices)
            subject.AddItem(choice.Label);
        var current = choices.ToList().FindIndex(c => c.Subject == brick.Subject);
        if (current < 0)
        {
            // Un sujet de modèle qui regroupe plusieurs valeurs (« carriere,mine ») : gardé tel quel, en tête du menu.
            subject.AddItem(SheetText.Subject(brick.Subject));
            current = choices.Count;
        }
        subject.Select(current);
        subject.ItemSelected += i =>
        {
            if (i < choices.Count)
                Report(_desk.SetDraftBrickSubject(draftId, index, choices[(int)i].Subject), _schema.Selected);
        };
        header.AddChild(subject);
        header.AddChild(ActionButton("Retirer", () => Report(_desk.RemoveDraftBrick(draftId, index), _schema.Selected)));
        _actions.AddChild(header);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        if (definition.Family == BrickFamily.Permission)
        {
            var allowed = new CheckBox { Text = "autorisé", ButtonPressed = brick.Value >= 0.5, FocusMode = FocusModeEnum.None };
            allowed.Toggled += on => Show(_desk.SetDraftBrick(draftId, index, on ? 1 : 0));
            row.AddChild(allowed);
        }
        else
        {
            var value = new Label { CustomMinimumSize = new Vector2(70, 0), Text = SheetText.Value(definition.Family, brick.Value) };
            var slider = new HSlider
            {
                MinValue = definition.Min,
                MaxValue = definition.Max,
                // Une graduation au centième de l'écart entre les bornes : un réglage d'affichage, pas une règle.
                Step = (definition.Max - definition.Min) / 100,
                Value = brick.Value,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                FocusMode = FocusModeEnum.None,
            };
            slider.ValueChanged += v =>
            {
                value.Text = SheetText.Value(definition.Family, v);
                Show(_desk.SetDraftBrick(draftId, index, v));
            };
            row.AddChild(slider);
            row.AddChild(value);
        }

        var locked = new CheckBox
        {
            Text = "verrouillée",
            ButtonPressed = brick.Locked,
            FocusMode = FocusModeEnum.None,
            TooltipText = "Verrouillée, la brique ne peut plus être changée par les niveaux en dessous.",
        };
        locked.Toggled += on => Show(_desk.SetDraftBrickLocked(draftId, index, on));
        row.AddChild(locked);
        _actions.AddChild(row);
    }

    /// <summary>
    /// Le menu du niveau d'une loi : l'État, les préfectures et les communes, en retrait sous ce qui les contient.
    /// Renvoie de quoi lire le niveau choisi.
    /// </summary>
    private (OptionButton menu, System.Func<string?> levelId) LevelMenu(string selected, System.Action<string>? changed)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = "Niveau", CustomMinimumSize = new Vector2(90, 0) });
        var menu = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, FitToLongestItem = false };
        var ids = new List<string>();
        foreach (var (node, depth) in Levels(_view.StructureTree(), 0))
        {
            menu.AddItem($"{new string(' ', depth * 4)}{node.Name}");
            ids.Add(node.Id);
        }
        menu.Select(System.Math.Max(0, ids.IndexOf(selected)));
        if (changed is not null)
            menu.ItemSelected += i => changed(ids[(int)i]);
        row.AddChild(menu);
        _actions.AddChild(row);
        return (menu, () => menu.Selected >= 0 && menu.Selected < ids.Count ? ids[menu.Selected] : null);
    }

    /// <summary>Le champ du nom d'une loi ; le changement est pris à Entrée ou quand on quitte le champ.</summary>
    private LineEdit NameField(string name, System.Action<string>? changed)
    {
        var field = new LineEdit { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        // Le nom est pris à chaque lettre (un nom vide est ignoré) : rien ne se perd si on clique ailleurs.
        if (changed is not null)
            field.TextChanged += text =>
            {
                if (!string.IsNullOrWhiteSpace(text))
                    changed(text);
            };
        _actions.AddChild(Labeled("Nom", field));
        return field;
    }

    private static HBoxContainer Labeled(string label, Control control)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(90, 0) });
        row.AddChild(control);
        return row;
    }

    private static IEnumerable<(StructureNode Node, int Depth)> Levels(IEnumerable<StructureNode> nodes, int depth) =>
        nodes.Where(n => n.Kind is StructureNodeKind.State or StructureNodeKind.Prefecture or StructureNodeKind.Commune)
            .SelectMany(n => Levels(n.Children, depth + 1).Prepend((n, depth)));

    private static Button ActionButton(string text, System.Action pressed)
    {
        var button = new Button { Text = text, FocusMode = FocusModeEnum.None };
        button.Pressed += pressed;
        return button;
    }

    /// <summary>Affiche un message ; un message vide (un réglage réussi) n'efface pas le précédent.</summary>
    private void Show(string message)
    {
        if (message.Length == 0)
            return;
        _message.Text = message;
        _message.Visible = true;
    }

    /// <summary>Affiche le résultat d'une décision et choisit ce qu'elle a créé ou déplacé (un brouillon, un vote).</summary>
    private void Report(string message, string? select)
    {
        // Après le signal du bouton : les décisions refaites retirent ce bouton de la page.
        Callable.From(() =>
        {
            if (select is not null)
            {
                _schema.Expand("votes");
                _schema.Expand("brouillons");
                _schema.Selected = select;
            }
            _schema.Invalidate();
            _actionsKey = "\0";
            Refresh();
            Show(message);
        }).CallDeferred();
    }

    private void Build(TreeItem root)
    {
        _schema.Item(root, "vierge", "+ Nouvelle loi vierge", UiTheme.Accent);

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
            "vierge" => SheetText.Heading("NOUVELLE LOI VIERGE")
                + "Une loi faite de zéro : son nom, son domaine du droit et son niveau (l'État, une préfecture, une commune). "
                + $"Le brouillon naît sans brique ; ajoutez-en une à {LawCatalogue.MaxBricksPerLaw}, du même domaine, puis "
                + "choisissez pour chacune à qui elle s'applique et réglez son curseur.\n\n"
                + SheetText.Muted("Un domaine régalien voté sous l'État coûte deux fois plus cher."),
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
        text.Append(change.Voted
            ? SheetText.Section("Payé en légitimité", $"{LawHints.Paid} {LawHints.Cost()}")
            : SheetText.Section("Coût s'il était voté maintenant", LawHints.Cost()));
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
            text.AppendLine($"Domaine : {SheetText.Escape(domain.Name)}{(domain.Regalian ? " " + SheetText.Hint("(régalien)", LawHints.Regalian) : "")}");
        text.AppendLine($"{SheetText.Hint("Niveau par défaut", LawHints.Level + " Il se change avant de créer le brouillon.")} : {SheetText.Escape(LevelName(template.Level))}");
        if (template.Description is { } description)
            text.AppendLine().AppendLine(SheetText.Escape(description));

        text.Append(SheetText.Section("Briques"));
        foreach (var setting in template.Bricks)
        {
            var definition = bricks.FirstOrDefault(b => b.Id == setting.Brick);
            var family = definition?.Family ?? BrickFamily.Parameter;
            text.AppendLine($"{SheetText.Hint(SheetText.Escape(definition?.Name ?? setting.Brick), definition?.Description ?? setting.Brick)} · "
                + SheetText.Hint(SheetText.Escape(SheetText.Subject(setting.Subject)), LawHints.Subject));
            text.Append($"    {SheetText.Value(family, setting.Value)}");
            if (definition is not null)
                text.Append(SheetText.Muted($"   (par défaut : {SheetText.Value(family, definition.Default)})", LawHints.Default));
            text.AppendLine(setting.Locked ? " · " + SheetText.Hint("verrouillée", LawHints.Locked(true)) : "");
        }

        text.Append(SheetText.Section("Coût", LawHints.Cost()));
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
