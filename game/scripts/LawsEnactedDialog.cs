using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Politics;
using Godot;

namespace Demos.Game;

/// <summary>
/// La fenêtre du 1er du mois : les changements de lois qui viennent d'entrer en vigueur (nouvelles lois, modifications,
/// abrogations), avec leur niveau et leurs pôles. Le jeu est mis en pause pendant qu'elle s'affiche ; Espace le relance.
/// Qui accepte ou rejette chaque loi, et ses effets attendus, viendront quand la règle sera décidée.
/// </summary>
public partial class LawsEnactedDialog : AcceptDialog
{
    private readonly RichTextLabel _text = null!;

    public LawsEnactedDialog(Theme theme)
    {
        Theme = theme;
        Title = "Lois entrées en vigueur";
        OkButtonText = "Fermer";
        Exclusive = false;
        _text = new HintText
        {
            BbcodeEnabled = true,
            FitContent = true,
            CustomMinimumSize = new Vector2(520, 0),
            SelectionEnabled = false,
        };
        _text.AddThemeColorOverride("default_color", UiTheme.Text);
        AddChild(_text);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public LawsEnactedDialog() { }

    public void ShowEnacted(GameView view, IReadOnlyList<LawDraft> enacted)
    {
        var names = Names(view.StructureTree());
        var laws = view.Laws();
        var text = new StringBuilder(SheetText.Heading($"Le {view.Today:dd/MM/yyyy}"));
        text.AppendLine(enacted.Count == 1 ? "Ce changement entre en vigueur :" : $"Ces {enacted.Count} changements entrent en vigueur :");
        foreach (var draft in enacted)
        {
            var level = names.GetValueOrDefault(draft.Law.StructureId, draft.Law.StructureId);
            var poles = laws.FirstOrDefault(l => l.Id == draft.Law.Id)?.Orientations ?? [];
            text.AppendLine($"\n{SheetText.ChangeKind(draft.Kind)} : {SheetText.Escape(draft.Law.Name)}  {SheetText.Chips(poles)}");
            text.AppendLine(SheetText.Muted($"    {level}, et tout ce que ce niveau contient"));
        }

        text.AppendLine();
        text.AppendLine(SheetText.Muted("Le jeu est en pause : fermez cette fenêtre, puis Espace pour reprendre. Les lois se lisent dans Lois → Code civil."));
        _text.Text = text.ToString();
        PopupCentered();
    }

    private static Dictionary<string, string> Names(IEnumerable<StructureNode> nodes) =>
        All(nodes).ToDictionary(n => n.Id, n => n.Name);

    private static IEnumerable<StructureNode> All(IEnumerable<StructureNode> nodes) =>
        nodes.SelectMany(n => All(n.Children).Prepend(n));
}
