using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Politics;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Un schéma repliable et sa fiche, comme dans le menu Structure : chaque boîte se replie sur son titre, et un clic sur
/// le titre la déroule (ou la replie) et affiche sa fiche. Les boîtes ont une clé (texte) ; la page dit comment
/// remplir le schéma et décrire une clé. Le schéma n'est refait que quand sa forme change, en gardant ce qui était
/// déroulé et choisi.
/// </summary>
public sealed class TreeSheet
{
    private readonly Tree _tree;
    private readonly RichTextLabel _sheet;
    private readonly HashSet<string> _expanded = [];
    private string _shape = "";

    public TreeSheet()
    {
        _tree = new Tree
        {
            HideRoot = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        _tree.AddThemeColorOverride("font_color", UiTheme.Text);
        _tree.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _tree.ItemMouseSelected += (_, _) => Choose(_tree.GetSelected());
        // La petite flèche replie aussi une boîte : on s'en souvient quand le schéma est refait.
        _tree.ItemCollapsed += item =>
        {
            if (item.GetMetadata(0).AsString() is not { Length: > 0 } key)
                return;
            if (item.Collapsed)
                _expanded.Remove(key);
            else
                _expanded.Add(key);
        };

        _sheet = new HintText
        {
            BbcodeEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectionEnabled = false,
        };
        _sheet.AddThemeColorOverride("default_color", UiTheme.Text);
    }

    public Tree Tree => _tree;
    public RichTextLabel Sheet => _sheet;

    /// <summary>La boîte choisie (sa clé), dont la fiche est affichée.</summary>
    public string? Selected { get; set; }

    /// <summary>Remplit le schéma : la page ajoute ses boîtes sous la racine invisible.</summary>
    public System.Action<TreeItem> Build { get; set; } = _ => { };

    /// <summary>Le texte BBCode de la fiche d'une clé.</summary>
    public System.Func<string, string> Describe { get; set; } = _ => "";

    public void Expand(string key) => _expanded.Add(key);

    /// <summary>
    /// Met à jour la page : refait le schéma si sa forme a changé (une loi votée, un filtre changé…), puis la fiche,
    /// qui suit la partie à chaque image.
    /// </summary>
    public void Refresh(string shape)
    {
        if (shape != _shape)
        {
            _shape = shape;
            _tree.Clear();
            Build(_tree.CreateItem());
        }

        _sheet.Text = Selected is { } key ? Describe(key) : SheetText.Muted("Choisissez un élément dans le schéma.");
    }

    /// <summary>Force le schéma à se refaire au prochain rafraîchissement (un filtre a changé).</summary>
    public void Invalidate() => _shape = "\0";

    /// <summary>Une boîte du schéma : son titre, un repère de couleur à gauche (ou rien), et sa clé.</summary>
    public TreeItem Item(TreeItem parent, string key, string text, Color? marker = null, bool muted = false)
    {
        var item = _tree.CreateItem(parent);
        item.SetText(0, text);
        item.SetMetadata(0, key);
        if (marker is { } color)
            item.SetIcon(0, UiTheme.Square(color, 6, 18));
        if (muted)
            item.SetCustomColor(0, UiTheme.Muted);
        item.Collapsed = !_expanded.Contains(key);
        if (key == Selected)
            item.Select(0);
        return item;
    }

    /// <summary>Les pastilles des pôles, à droite du titre ; le texte de la bulle s'affiche au survol.</summary>
    public static void AddPoleChips(TreeItem item, IEnumerable<Orientation> poles, string tooltip)
    {
        var id = item.GetButtonCount(0);
        foreach (var pole in poles.Distinct())
            // Un bouton actif garde sa vraie couleur (désactivé, il serait terni) ; un clic dessus ne fait rien.
            item.AddButton(0, UiTheme.Square(UiTheme.PoleColor(pole), 12, 12), id++, disabled: false,
                tooltipText: $"{tooltip} : {WorldText.PoleName(pole)} ({pole.Code})");
    }

    private void Choose(TreeItem? item)
    {
        if (item?.GetMetadata(0).AsString() is not { Length: > 0 } key)
            return;

        if (item.GetChildCount() > 0)
            item.Collapsed = !item.Collapsed;
        Selected = key;
        _sheet.Text = Describe(key);
    }
}
