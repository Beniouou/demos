using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Domain.Map;
using Demos.Domain.Politics;
using Godot;

namespace Demos.Game;

/// <summary>
/// Le serment (décidé le 25 novembre 2026, texte du 27 novembre 2026) : sur fond sombre, le papier ; le joueur recopie la
/// première et la dernière phrase en tapant par-dessus le texte en gris clair (une lettre fausse ne s'écrit pas ; ni
/// coller, ni passer), puis il signe à la souris. « Signer sans recopier » existe pour l'accessibilité, discret, jamais
/// proposé par défaut. Pas de serment en bac à sable.
/// </summary>
public partial class OathScreen : Control
{
    /// <summary>Le joueur a signé : sa signature, en traits de points de 0 à 1 dans le cadre.</summary>
    public event Action<IReadOnlyList<IReadOnlyList<Point2D>>>? Signed;

    private readonly PromiseOath _oath = null!;
    private readonly string[] _targets = [];
    private int _sentence;
    private int _typed;
    private readonly RichTextLabel _first = null!;
    private readonly RichTextLabel _last = null!;
    private readonly Label _progress = null!;
    private readonly VBoxContainer _signing = null!;
    private readonly SignaturePad _pad = null!;
    private readonly Button _sign = null!;

    public OathScreen() { }

    public OathScreen(PromiseChoice choice, IReadOnlyList<string> lawNames)
    {
        _oath = PromiseOath.Of(choice.Promise, choice.PeopleName);
        _targets = [.. _oath.ToCopy];

        Theme = UiTheme.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = new Color("#0b0c0f"), AnchorRight = 1, AnchorBottom = 1 });
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(center);
        var paper = SignedText.PaperPanel(760);
        center.AddChild(paper);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        paper.AddChild(column);

        var title = Ink(choice.Promise.Name.ToUpperInvariant(), SignedText.FadedInk, UiTheme.FontSize - 2);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(title);
        _first = Copy();
        column.AddChild(_first);
        column.AddChild(Ink(_oath.Middle, new Color("#3d3426"), UiTheme.FontSize + 2));
        _last = Copy();
        column.AddChild(_last);
        column.AddChild(Ink(_oath.Signed, new Color("#3d3426"), UiTheme.FontSize));
        column.AddChild(new HSeparator());
        for (var i = 0; i < choice.Engagements.Count; i++)
            column.AddChild(Ink($"Article {i + 1} — {choice.Engagements[i].Text}.", new Color("#3d3426"), UiTheme.FontSize));
        if (lawNames.Count > 0)
            column.AddChild(Ink($"Lois de l'An I : {string.Join(", ", lawNames)}.", new Color("#7a6a4f"), UiTheme.FontSize - 2));

        _progress = Ink("", new Color("#7a6a4f"), UiTheme.FontSize - 2);
        column.AddChild(_progress);

        _signing = new VBoxContainer { Visible = false };
        _signing.AddThemeConstantOverride("separation", 8);
        column.AddChild(_signing);
        _signing.AddChild(Ink("Tracez votre signature à la souris :", new Color("#3d3426"), UiTheme.FontSize));
        _pad = new SignaturePad { CustomMinimumSize = new Vector2(0, 120) };
        _signing.AddChild(_pad);
        var buttons = new HBoxContainer();
        _signing.AddChild(buttons);
        var clear = new Button { Text = "Effacer" };
        clear.Pressed += _pad.Clear;
        buttons.AddChild(clear);
        buttons.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _sign = new Button { Text = "Signer", Disabled = true, CustomMinimumSize = new Vector2(140, 0) };
        _sign.Pressed += () => Signed?.Invoke(_pad.Strokes);
        buttons.AddChild(_sign);
        _pad.Changed += () => _sign.Disabled = _pad.Strokes.Count == 0;

        // Pour l'accessibilité : discret, jamais le chemin par défaut.
        var skip = new LinkButton { Text = "Signer sans recopier (accessibilité)", Underline = LinkButton.UnderlineMode.OnHover };
        skip.AddThemeColorOverride("font_color", new Color("#9a8d70"));
        skip.Pressed += () =>
        {
            _sentence = _targets.Length;
            Refresh();
        };
        column.AddChild(skip);
        Refresh();
    }

    private static Label Ink(string text, Color color, int size)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    private static RichTextLabel Copy()
    {
        var label = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, SelectionEnabled = false };
        label.AddThemeFontSizeOverride("normal_font_size", UiTheme.FontSize + 4);
        return label;
    }

    /// <summary>Une lettre tapée : elle s'écrit si c'est la bonne (sans tenir compte de la casse ni de l'apostrophe typographique).</summary>
    public override void _Input(InputEvent @event)
    {
        if (_sentence >= _targets.Length || @event is not InputEventKey { Pressed: true } key || key.Unicode == 0)
            return;

        var target = _targets[_sentence];
        if (_typed < target.Length && Same((char)key.Unicode, target[_typed]))
        {
            _typed++;
            if (_typed >= target.Length)
            {
                _sentence++;
                _typed = 0;
            }

            Refresh();
        }

        GetViewport().SetInputAsHandled();
    }

    private static bool Same(char typed, char expected)
    {
        static char Plain(char c) => c is '’' or '‘' ? '\'' : char.ToLowerInvariant(c);
        return Plain(typed) == Plain(expected);
    }

    private void Refresh()
    {
        Show(_first, 0);
        Show(_last, 1);
        var total = _targets.Sum(Words);
        var done = _targets.Take(Math.Min(_sentence, _targets.Length)).Sum(Words)
            + (_sentence < _targets.Length ? Words(_targets[_sentence][.._typed]) : 0);
        _progress.Text = _sentence < _targets.Length
            ? $"Recopiez les phrases en gris : {done} mots sur {total}   ·   ni coller, ni passer"
            : "Le texte est recopié.";
        _signing.Visible = _sentence >= _targets.Length;
    }

    private static int Words(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    private void Show(RichTextLabel label, int index)
    {
        var target = _targets[index];
        var done = index < _sentence ? target.Length : index == _sentence ? _typed : 0;
        var ink = SignedText.InkColor.ToHtml(false);
        var faded = SignedText.FadedInk.ToHtml(false);
        var caret = index == _sentence ? "[color=#2b2418]|[/color]" : "";
        label.Text = $"[color=#{ink}]{Escape(target[..done])}[/color]{caret}[color=#{faded}]{Escape(target[done..])}[/color]";
    }

    private static string Escape(string text) => text.Replace("[", "[lb]");
}

/// <summary>Le cadre de signature : on y trace des traits à la souris, gardés en points de 0 à 1.</summary>
public partial class SignaturePad : Control
{
    private readonly List<List<Point2D>> _strokes = [];
    private bool _drawing;

    public event Action? Changed;

    public IReadOnlyList<IReadOnlyList<Point2D>> Strokes => _strokes.Select(s => (IReadOnlyList<Point2D>)s.ToList()).ToList();

    public SignaturePad() => MouseDefaultCursorShape = CursorShape.Cross;

    public void Clear()
    {
        _strokes.Clear();
        QueueRedraw();
        Changed?.Invoke();
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _drawing = button.Pressed;
                if (_drawing)
                    _strokes.Add([Normalized(button.Position)]);
                else
                    Changed?.Invoke();
                break;
            case InputEventMouseMotion motion when _drawing && _strokes.Count > 0:
                _strokes[^1].Add(Normalized(motion.Position));
                QueueRedraw();
                break;
        }
    }

    private Point2D Normalized(Vector2 position) =>
        new(Math.Clamp(position.X / Math.Max(1, Size.X), 0, 1), Math.Clamp(position.Y / Math.Max(1, Size.Y), 0, 1));

    public override void _Draw()
    {
        DrawLine(new Vector2(0, Size.Y - 1), new Vector2(Size.X, Size.Y - 1), new Color("#8a7a5c"), 1);
        foreach (var stroke in _strokes.Where(s => s.Count > 1))
            DrawPolyline(stroke.Select(p => new Vector2((float)p.X * Size.X, (float)p.Y * Size.Y)).ToArray(), SignedText.InkColor, 2.4f, antialiased: true);
    }
}
