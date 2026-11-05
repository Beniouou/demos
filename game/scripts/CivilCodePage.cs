using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Politics;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Lois → Code civil : les lois en vigueur. À gauche, rangées par domaine du droit dans des boîtes repliables, avec un
/// filtre qui descend la hiérarchie : entité politique → organisme → bâtiment ; on voit les lois votées au niveau le
/// plus précis choisi. À droite, la fiche de la loi choisie : domaine, niveau, et chaque brique avec son sujet, son
/// curseur, la valeur héritée des niveaux au-dessus, ses bornes et ses pôles.
/// </summary>
public partial class CivilCodePage : HBoxContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly TreeSheet _schema = null!;
    private readonly StructurePicker _picker = null!;

    public CivilCodePage(GameView view)
    {
        _view = view;
        AddThemeConstantOverride("separation", 12);
        _schema = new TreeSheet { Build = Build, Describe = Describe };

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 8);
        _picker = new StructurePicker(left, allowAll: true);
        _picker.Changed += _schema.Invalidate;
        left.AddChild(_schema.Tree);
        AddChild(left);

        AddChild(new VSeparator());
        AddChild(_schema.Sheet);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public CivilCodePage() { }

    public void Refresh()
    {
        _picker.Refresh(_view.StructureTree());
        var laws = ShownLaws();
        if (_schema.Selected is null)
        {
            // Au premier affichage, les domaines qui ont des lois sont déroulés et la première loi est choisie.
            foreach (var law in laws)
                _schema.Expand($"domaine:{law.Domain}");
            _schema.Selected = laws.Count > 0 ? $"loi:{laws[0].Id}" : null;
        }

        _schema.Refresh($"{_picker.MostPrecise}|{string.Join(";", laws.Select(l => $"{l.Id}:{string.Join(",", l.Orientations.Select(o => o.Code))}"))}");
    }

    /// <summary>Les lois votées dans la structure la plus précise choisie, ou toutes les lois sans choix.</summary>
    private IReadOnlyList<LawView> ShownLaws() =>
        _picker.MostPrecise is { } structureId ? _view.Laws().Where(l => l.StructureId == structureId).ToList() : _view.Laws();

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
