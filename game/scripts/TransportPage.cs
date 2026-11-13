using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game;

/// <summary>
/// Production → Transport, en lecture : les liaisons entre communes (sentiers, chemins, routes) avec leur longueur, leur
/// montée, qui les paie, la part de perte de transport qu'elles retirent et leur entretien ; puis ce qu'a changé le
/// dernier bilan des routes, et la règle. Les routes naissent du trafic : le joueur ne les construit pas.
/// </summary>
public partial class TransportPage : VBoxContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly RichTextLabel _text = null!;

    public TransportPage(GameView view)
    {
        _view = view;
        _text = new HintText
        {
            BbcodeEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectionEnabled = false,
        };
        _text.AddThemeColorOverride("default_color", UiTheme.Text);
        AddChild(_text);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public TransportPage() { }

    public void Refresh()
    {
        var text = Describe();
        // Le texte n'est remis que s'il a changé : sinon la fiche remonterait en haut à chaque image.
        if (text != _text.Text)
            _text.Text = text;
    }

    private string Describe()
    {
        var settings = Balance.Current;
        var links = _view.Roads().OrderByDescending(l => l.Level).ThenBy(l => RoadText.Link(_view, l)).ToList();
        var text = new StringBuilder(SheetText.Heading($"LIAISONS ({links.Count})"));
        if (links.Count == 0)
            text.AppendLine(SheetText.Muted("Aucune pour l'instant : un sentier s'ouvre quand deux communes échangent assez en un mois, ou quand un bâtiment posé sur la carte produit assez."));
        else
        {
            var muted = UiTheme.Muted.ToHtml(false);
            var communes = _view.Communes().Select(c => c.Id).ToHashSet();
            text.Append("[table=7]");
            foreach (var header in new[] { "Liaison", "Niveau", "Longueur", "Montée", "Payée par", "Perte retirée", "Entretien / an" })
                text.Append($"[cell padding=0,0,16,4][color=#{muted}]{header}[/color][/cell]");
            foreach (var link in links)
            {
                text.Append($"[cell padding=0,0,16,2]{SheetText.Escape(RoadText.Link(_view, link))}[/cell]");
                text.Append($"[cell padding=0,0,16,2]{RoadText.LevelName(link.Level)}[/cell]");
                text.Append($"[cell padding=0,0,16,2]{link.Length:0.#} km[/cell]");
                text.Append($"[cell padding=0,0,16,2]{link.Climb:N0} m[/cell]");
                text.Append($"[cell padding=0,0,16,2]{SheetText.Escape(_view.NameOf(link.PayerId))}[/cell]");
                // Vers un bâtiment, pas de perte de transport : rien à retirer.
                var betweenCommunes = communes.Contains(link.A) && communes.Contains(link.B);
                text.Append($"[cell padding=0,0,16,2]{(betweenCommunes ? $"{RoadText.LossCut(link.Level):P0}" : "—")}[/cell]");
                text.Append($"[cell padding=0,0,16,2]{Roads.Upkeep(link, settings):N0}[/cell]");
            }
            text.AppendLine("[/table]");
        }

        text.Append(SheetText.Section("Dernier bilan des routes"));
        var lines = RoadText.Lines(_view, _view.RoadChanges);
        text.AppendLine(lines.Count == 0
            ? SheetText.Muted("Rien n'a changé au dernier 1er du mois.")
            : string.Join("\n", lines.Select(SheetText.Escape)));

        text.Append(SheetText.Section("La règle"));
        text.AppendLine(SheetText.Muted(
            "Une liaison relie deux communes, ou un bâtiment posé sur la carte et sa commune (ce qu'il produit fait le trafic ; "
            + "cette liaison ne retire aucune perte, car la production arrive entière au marché, et c'est la commune qui la paie). "
            + $"Le trafic d'un mois crée un sentier (dès {settings.RoadTrailTraffic:N0} échangés), puis un chemin "
            + $"({settings.RoadPathTraffic:N0}), puis une route ({settings.RoadRoadTraffic:N0}) : un niveau par mois au plus, au 1er du mois, "
            + "si celle qui paie a l'argent (la plus petite structure qui contient les deux communes ; pour une préfecture, l'État paie pour l'instant). "
            + $"Un chemin retire {RoadText.LossCut(RoadLevel.Path):P0} de la perte de transport, une route {RoadText.LossCut(RoadLevel.Road):P0}. "
            + "L'entretien se paie chaque 1er janvier ; impayé, la liaison descend d'un niveau. Le tracé suit le relief."));
        return text.ToString();
    }
}
