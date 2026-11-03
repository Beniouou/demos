using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Ressources → Impôts : l'impôt d'État, versé au trésor, et l'impôt de chaque commune, versé à sa caisse. Chacun est
/// une part de la richesse des habitants, prélevée chaque jour.
/// </summary>
public partial class TaxesPage : ScrollContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private MarginContainer? _root;
    private VBoxContainer _content = null!;
    private Label _stateNote = null!;
    private Label _message = null!;
    private readonly List<string> _communeIds = [];
    private readonly Dictionary<string, Label> _communeNotes = [];

    public TaxesPage(GameView view, PlayerDesk desk)
    {
        _view = view;
        _desk = desk;
        HorizontalScrollMode = ScrollMode.Disabled;
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public TaxesPage() { }

    public void Refresh()
    {
        var communes = _view.Communes();
        if (_root is null || !communes.Select(c => c.Id).SequenceEqual(_communeIds))
            Build(communes);

        _stateNote.Text = $"Aujourd'hui {_view.StateTaxRate:P2} de la richesse par jour, versé au trésor ({_view.TreasuryBalance:N0}).";
        foreach (var commune in communes)
            _communeNotes[commune.Id].Text = $"Aujourd'hui {commune.TaxRate:P2} par jour, versé à sa caisse ({commune.Fund:N0}).";
    }

    /// <summary>Construit la page ; à refaire quand une commune est fondée. Les cases reprennent les taux en vigueur.</summary>
    private void Build(IReadOnlyList<CommuneView> communes)
    {
        // Un ScrollContainer n'a qu'un enfant : l'ancienne page sort tout de suite de l'arbre.
        if (_root is not null)
        {
            RemoveChild(_root);
            _root.QueueFree();
        }
        _communeIds.Clear();
        _communeNotes.Clear();

        _root = UiTheme.Margin(8);
        AddChild(_root);
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 8);
        _root.AddChild(_content);

        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        _message.AddThemeColorOverride("font_color", UiTheme.Accent);
        _content.AddChild(_message);

        _content.AddChild(UiTheme.Section("Impôt d'État"));
        _stateNote = Note();
        TaxRow("État", _view.StateTaxRate, rate =>
        {
            _desk.SetStateTaxRate(rate);
            return $"Impôt d'État fixé à {rate:P2} par jour.";
        });

        _content.AddChild(new HSeparator());
        _content.AddChild(UiTheme.Section("Impôt des communes"));
        foreach (var commune in communes)
        {
            _communeIds.Add(commune.Id);
            var name = commune.Name;
            var id = commune.Id;
            _content.AddChild(new Label { Text = name });
            _communeNotes[id] = Note();
            TaxRow(name, commune.TaxRate, rate =>
            {
                _desk.SetCommuneTaxRate(id, rate);
                return $"Impôt de {name} fixé à {rate:P2} par jour.";
            });
        }
    }

    /// <summary>Une case en pour cent par jour et son bouton ; le taux appliqué est une part (0,003 pour 0,3 %).</summary>
    private void TaxRow(string name, double current, System.Func<double, string> apply)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var percent = new SpinBox { MinValue = 0, MaxValue = 100, Step = 0.01, Suffix = "% par jour", Value = current * 100, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(percent);
        var button = new Button { Text = "Appliquer", FocusMode = FocusModeEnum.None, TooltipText = $"Impôt : {name}" };
        button.Pressed += () =>
        {
            _message.Text = apply(percent.Value / 100);
            _message.Visible = true;
        };
        row.AddChild(button);
        _content.AddChild(row);
    }

    private Label Note()
    {
        var note = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        note.AddThemeColorOverride("font_color", UiTheme.Muted);
        _content.AddChild(note);
        return note;
    }
}
