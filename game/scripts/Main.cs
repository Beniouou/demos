using System.Text;
using Demos.Application;
using Demos.Infrastructure;
using Godot;

namespace Demos.Game;

/// <summary>
/// Adaptateur d'entrée Godot : gère l'horloge réelle, la pause et les vitesses,
/// et appelle la simulation jour par jour. Aucune règle du jeu ici.
/// </summary>
public partial class Main : Node
{
    // Jours de jeu par seconde réelle, pour les vitesses ×1, ×2 et ×5.
    private static readonly double[] DaysPerSecond = [1, 2, 5];
    private static readonly string[] SpeedLabels = ["×1", "×2", "×5"];

    private Simulation _simulation = null!;
    private Label _status = null!;
    private bool _paused = true;
    private int _speedIndex;
    private double _pendingDays;

    public override void _Ready()
    {
        _status = GetNode<Label>("Status");
        var content = new JsonContentSource(fileName => FileAccess.GetFileAsString($"res://data/{fileName}"));
        _simulation = PrototypeScenario.Create(content);
    }

    public override void _Process(double delta)
    {
        if (!_paused)
        {
            _pendingDays += delta * DaysPerSecond[_speedIndex];
            while (_pendingDays >= 1)
            {
                _pendingDays -= 1;
                _simulation.AdvanceDay();
            }
        }

        _status.Text = Describe();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        switch (key.Keycode)
        {
            case Key.Space: _paused = !_paused; break;
            case Key.Key1: _speedIndex = 0; break;
            case Key.Key2: _speedIndex = 1; break;
            case Key.Key3: _speedIndex = 2; break;
        }
    }

    private string Describe()
    {
        var text = new StringBuilder();
        text.AppendLine($"{_simulation.Clock.Today:dd/MM/yyyy}   {(_paused ? "EN PAUSE" : SpeedLabels[_speedIndex])}");
        text.AppendLine("Espace : pause   1 / 2 / 3 : vitesse");
        text.AppendLine();

        foreach (var socialClass in _simulation.World.Classes)
        {
            text.AppendLine(
                $"{socialClass.Name,-12} {socialClass.Ideology.Name,-16} " +
                $"population {socialClass.Population,10:N0}   approbation {socialClass.Approval,5:F1}");
        }

        text.AppendLine();
        text.AppendLine($"Produits chargés : {_simulation.World.Products.Count}");
        return text.ToString();
    }
}
