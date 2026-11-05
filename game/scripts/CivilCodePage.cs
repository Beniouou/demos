using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Politics;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Lois → Code civil : les lois en vigueur. À gauche, rangées par domaine du droit dans des boîtes repliables, avec deux
/// filtres : le niveau de structure (État, commune, organisme, bâtiment), puis laquelle à ce niveau ; à droite, la fiche
/// de la loi choisie : domaine, niveau, et chaque brique avec son sujet, son curseur, la valeur héritée des niveaux
/// au-dessus, ses bornes et ses pôles.
/// </summary>
public partial class CivilCodePage : HBoxContainer, IMenuPage
{
    /// <summary>Les niveaux du premier filtre ; null pour tous les niveaux. Le second filtre commence par « tous ».</summary>
    private static readonly (StructureNodeKind? Kind, string Name, string All)[] Levels =
    [
        (null, "Tous les niveaux", ""),
        (StructureNodeKind.State, "État", ""),
        (StructureNodeKind.Commune, "Commune", "Toutes les communes"),
        (StructureNodeKind.Organization, "Organisme", "Tous les organismes"),
        (StructureNodeKind.Building, "Bâtiment", "Tous les bâtiments"),
    ];

    private readonly GameView _view = null!;
    private readonly TreeSheet _schema = null!;
    private OptionButton _level = null!;
    private OptionButton _name = null!;
    private readonly List<string?> _nameIds = [];

    public CivilCodePage(GameView view)
    {
        _view = view;
        AddThemeConstantOverride("separation", 12);
        _schema = new TreeSheet { Build = Build, Describe = Describe };

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 8);
        _level = Filter(left, "Niveau");
        foreach (var level in Levels)
            _level.AddItem(level.Name);
        _level.Select(0);
        _level.ItemSelected += _ =>
        {
            _nameIds.Clear();
            _schema.Invalidate();
        };
        _name = Filter(left, "Nom");
        _name.ItemSelected += _ => _schema.Invalidate();
        left.AddChild(_schema.Tree);
        AddChild(left);

        AddChild(new VSeparator());
        AddChild(_schema.Sheet);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public CivilCodePage() { }

    public void Refresh()
    {
        var structures = StructuresOfLevel();
        FillNames(structures);
        var laws = ShownLaws(structures);
        if (_schema.Selected is null)
        {
            // Au premier affichage, les domaines qui ont des lois sont déroulés et la première loi est choisie.
            foreach (var law in laws)
                _schema.Expand($"domaine:{law.Domain}");
            _schema.Selected = laws.Count > 0 ? $"loi:{laws[0].Id}" : null;
        }

        _schema.Refresh($"{_level.Selected}:{SelectedName()}|{string.Join(";", laws.Select(l => $"{l.Id}:{string.Join(",", l.Orientations.Select(o => o.Code))}"))}");
    }

    private static OptionButton Filter(VBoxContainer column, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(80, 0) });
        var option = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None,
            FitToLongestItem = false,
            ClipText = true,
        };
        row.AddChild(option);
        column.AddChild(row);
        return option;
    }

    /// <summary>
    /// Les structures du niveau choisi, avec leur nom dans le second filtre (un bâtiment ou un organisme suivi de sa
    /// commune) ; aucune pour tous les niveaux.
    /// </summary>
    private List<(string Id, string Name)> StructuresOfLevel()
    {
        var kind = Levels[System.Math.Max(0, _level.Selected)].Kind;
        var found = new List<(string Id, string Name)>();
        void Visit(StructureNode node, string? commune)
        {
            if (node.Kind == kind)
                found.Add((node.Id, commune is null || node.Kind is StructureNodeKind.State or StructureNodeKind.Commune
                    ? node.Name : $"{node.Name} ({commune})"));
            foreach (var child in node.Children)
                Visit(child, node.Kind == StructureNodeKind.Commune ? node.Name : commune);
        }
        if (kind is not null)
            foreach (var root in _view.StructureTree())
                Visit(root, null);
        return found;
    }

    /// <summary>
    /// Le second filtre : « tous » puis chaque structure du niveau, refait quand le niveau change ou qu'une structure
    /// apparaît. Grisé pour tous les niveaux et pour l'État, qui est seul de son niveau.
    /// </summary>
    private void FillNames(List<(string Id, string Name)> structures)
    {
        var level = Levels[System.Math.Max(0, _level.Selected)];
        var ids = new List<string?> { null };
        ids.AddRange(structures.Select(s => s.Id));
        if (ids.SequenceEqual(_nameIds))
            return;

        var selected = SelectedName();
        _nameIds.Clear();
        _nameIds.AddRange(ids);
        _name.Clear();
        _name.AddItem(level.Kind is null or StructureNodeKind.State ? level.Name : level.All);
        foreach (var (_, name) in structures)
            _name.AddItem(name);
        _name.Select(System.Math.Max(0, _nameIds.IndexOf(selected)));
        _name.Disabled = level.Kind is null or StructureNodeKind.State;
        _schema.Invalidate();
    }

    private string? SelectedName() => _name.Selected > 0 && _name.Selected < _nameIds.Count ? _nameIds[_name.Selected] : null;

    /// <summary>Les lois du filtre : celles de la structure choisie, ou de toutes les structures du niveau.</summary>
    private IReadOnlyList<LawView> ShownLaws(List<(string Id, string Name)> structures)
    {
        var laws = _view.Laws();
        if (Levels[System.Math.Max(0, _level.Selected)].Kind is null)
            return laws;

        var allowed = SelectedName() is { } one ? [one] : structures.Select(s => s.Id).ToHashSet();
        return laws.Where(l => allowed.Contains(l.StructureId)).ToList();
    }

    private IReadOnlyList<LawView> ShownLaws() => ShownLaws(StructuresOfLevel());

    private void Build(TreeItem root)
    {
        var laws = ShownLaws();
        foreach (var domain in _view.LawDomains())
        {
            var inDomain = laws.Where(l => l.Domain == domain.Id).ToList();
            var item = _schema.Item(root, $"domaine:{domain.Id}",
                $"{domain.Name} ({inDomain.Count}){(domain.Regalian ? "   régalien" : "")}", GroupColor, muted: inDomain.Count == 0);
            foreach (var law in inDomain)
            {
                var lawItem = _schema.Item(item, $"loi:{law.Id}", $"{law.Name}   ({law.StructureName})");
                TreeSheet.AddPoleChips(lawItem, law.Orientations, law.Name);
            }
        }
    }

    private static readonly Color GroupColor = new("#6b7280");

    private string Describe(string key)
    {
        if (key.StartsWith("domaine:") && _view.LawDomains().FirstOrDefault(d => d.Id == key[8..]) is { } domain)
            return DescribeDomain(domain);
        if (key.StartsWith("loi:") && _view.Laws().FirstOrDefault(l => l.Id == key[4..]) is { } law)
            return DescribeLaw(law, _view.LawBricks());
        return SheetText.Muted("Cette loi n'est plus en vigueur.");
    }

    private string DescribeDomain(LawDomain domain)
    {
        var text = new StringBuilder(SheetText.Heading(domain.Name.ToUpperInvariant()));
        if (domain.Regalian)
            text.AppendLine("Domaine régalien : du rôle de l'État. Voté plus bas, il coûte deux fois plus cher.");
        var laws = _view.Laws().Where(l => l.Domain == domain.Id).ToList();
        text.Append(SheetText.Section($"Lois en vigueur ({laws.Count})"));
        if (laws.Count == 0)
            text.AppendLine(SheetText.Muted("Aucune."));
        foreach (var law in laws)
            text.AppendLine($"{SheetText.Escape(law.Name)}  {SheetText.Muted($"({law.StructureName})")}  {SheetText.Chips(law.Orientations)}");
        return text.ToString();
    }

    /// <summary>La fiche d'une loi : aussi utilisée par la commission pour ses brouillons et ses votes.</summary>
    public static string DescribeLaw(LawView law, IReadOnlyList<BrickDefinition> catalogue, string? title = null)
    {
        var text = new StringBuilder(SheetText.Heading((title ?? law.Name).ToUpperInvariant()));
        text.AppendLine(SheetText.Chips(law.Orientations));
        text.AppendLine($"Niveau : {SheetText.Escape(law.StructureName)}");
        if (law.RegalianBelowState)
            text.AppendLine("Loi régalienne votée sous l'État : elle a coûté deux fois plus cher.");

        text.Append(SheetText.Section("Briques"));
        foreach (var brick in law.Bricks ?? [])
            text.Append(DescribeBrick(brick, catalogue.FirstOrDefault(b => b.Id == brick.Id)));

        text.Append(SheetText.Section("S'applique à"));
        text.AppendLine($"{SheetText.Escape(law.StructureName)}, et tout ce que ce niveau contient.");
        return text.ToString();
    }

    private static string DescribeBrick(BrickView brick, BrickDefinition? definition)
    {
        var family = definition?.Family ?? BrickFamily.Parameter;
        var text = new StringBuilder();
        text.AppendLine($"{SheetText.Escape(brick.Name)} · {SheetText.Escape(SheetText.Subject(brick.Subject))}");
        text.Append($"    {SheetText.Value(family, brick.Value)}");
        if (definition is not null)
            text.Append(SheetText.Muted(family == BrickFamily.Permission
                ? $"   (hérité : {SheetText.Value(family, brick.Reference)})"
                : $"   (hérité : {SheetText.Value(family, brick.Reference)} ; de {SheetText.Value(family, definition.Min)} à {SheetText.Value(family, definition.Max)})"));
        text.AppendLine();
        text.Append($"    écart {brick.Intensity:P0}");
        text.Append(brick.Locked ? " · verrouillée" : " · non verrouillée");
        if (brick.Poles.Count > 0)
            text.Append($" · {SheetText.Chips(brick.Poles)}");
        text.AppendLine();
        if (definition?.Description is { } description)
            text.AppendLine(SheetText.Muted($"    {description}"));
        return text.ToString();
    }
}
