using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Shared;
using Demos.Infrastructure;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;
using Structure = Demos.Domain.Politics.Structure;
using StructureKind = Demos.Domain.Politics.StructureKind;

namespace Demos.Game;

/// <summary>
/// Adaptateur d'entrée Godot : gère l'horloge réelle, la pause et les vitesses,
/// et appelle la simulation jour par jour. Aucune règle du jeu ici.
/// </summary>
public partial class Main : Node
{
    // Durée réelle d'un jour de jeu : 5 s en ×1, 2 s en ×2, 0,5 s en ×3, 0,1 s en ×4.
    private static readonly double[] SecondsPerDay = [5, 2, 0.5, 0.1];
    private static readonly string[] SpeedLabels = ["×1", "×2", "×3", "×4"];

    // Construction de démonstration (touche B) : des logements d'État dans la commune 1.
    private static readonly MaterialCost[] HousingCost = [new("pierre", 200), new("bois", 100)];

    private Simulation _simulation = null!;
    private Label _status = null!;
    private bool _paused = true;
    private int _speedIndex;
    private double _pendingDays;
    private int _housingBuilt;
    private string _lastMessage = "";

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
            _pendingDays += delta / SecondsPerDay[_speedIndex];
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
            case Key.Key4: _speedIndex = 3; break;
            case Key.B: BuildHousing(); break;
        }
    }

    private void BuildHousing()
    {
        var id = $"logements-etat-{_housingBuilt + 1}";
        var housing = new Structure(id, $"Logements d'État {_housingBuilt + 1}", StructureKind.Building, "commune-1");

        if (_simulation.TryBuild(housing, HousingCost, PaymentMode.StockFirst))
        {
            _housingBuilt++;
            _lastMessage = $"{housing.Name} construits.";
        }
        else
        {
            _lastMessage = "Construction impossible : ni le stock, ni le trésor, ni le marché ne suffisent.";
        }
    }

    private string Describe()
    {
        var text = new StringBuilder();
        text.AppendLine(
            $"{_simulation.Clock.Today:dd/MM/yyyy}   {SeasonName(_simulation.Clock.Season)}   " +
            $"{(_paused ? "EN PAUSE" : SpeedLabels[_speedIndex])}");
        text.AppendLine("Espace : pause   1 à 4 : vitesse   B : construire des logements (200 pierre, 100 bois)");
        text.AppendLine();

        foreach (var socialClass in _simulation.World.Classes)
        {
            text.AppendLine(
                $"{socialClass.Name,-12} {socialClass.Ideology.Name,-16} " +
                $"population {socialClass.Population,10:N0}   adhésion {socialClass.Adhesion,4:P0}");
        }

        text.AppendLine();
        text.Append("Légitimité :");
        foreach (var pole in Orientation.All)
            text.Append($"   {pole.Code} {_simulation.World.Legitimacy[pole],8:F1}");
        text.AppendLine();
        text.AppendLine($"Trésor : {_simulation.World.Treasury.Balance:N0}");
        text.AppendLine($"Stock de l'État : {DescribeInventory(_simulation.World.StateStock)}");
        text.AppendLine($"Marché : {DescribeInventory(_simulation.World.Market.Stock)}");
        text.AppendLine();
        text.AppendLine($"Logements construits : {_housingBuilt}   {_lastMessage}");
        return text.ToString();
    }

    private static string DescribeInventory(Inventory inventory) =>
        inventory.Contents.Count == 0
            ? "vide"
            : string.Join("   ", inventory.Contents.Select(item => $"{item.Key} {item.Value:N0}"));

    private static string SeasonName(Season season) => season switch
    {
        Season.Winter => "Hiver",
        Season.Spring => "Printemps",
        Season.Summer => "Été",
        _ => "Automne",
    };
}
