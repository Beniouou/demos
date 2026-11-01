using System.Collections.Generic;
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

    // Constructions de démonstration : logements d'État (touche B) et camp de base (touche C).
    private static readonly MaterialCost[] HousingCost = [new("pierre", 200), new("bois", 100)];
    private static readonly MaterialCost[] BaseCampCost = [new("pierre", 100), new("bois", 100)];
    private const double BaseCampLegitimacyCost = 20;

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
            case Key.C: FoundCommune(); break;
        }
    }

    private void BuildHousing()
    {
        var id = $"logements-etat-{_housingBuilt + 1}";
        var housing = new Structure(id, $"Logements d'État {_housingBuilt + 1}", StructureKind.Building, PrototypeScenario.FirstCommuneId);

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

    private void FoundCommune()
    {
        // En attendant un vrai choix, la légitimité est prise dans le pôle le mieux fourni.
        var pole = Orientation.All.MaxBy(p => _simulation.World.Legitimacy[p]);
        var name = $"Commune {_simulation.World.Communes.Count() + 1}";

        var commune = _simulation.TryFoundCommune(name, BaseCampCost, PaymentMode.StockFirst, pole, BaseCampLegitimacyCost);
        _lastMessage = commune is null
            ? $"Camp de base impossible : il faut 100 pierre, 100 bois et {BaseCampLegitimacyCost} de légitimité."
            : $"{commune.Name} fondée (légitimité {pole.Code} dépensée).";
    }

    private string Describe()
    {
        var world = _simulation.World;
        var text = new StringBuilder();
        text.AppendLine(
            $"{_simulation.Clock.Today:dd/MM/yyyy}   {SeasonName(_simulation.Clock.Season)}   " +
            $"{(_paused ? "EN PAUSE" : SpeedLabels[_speedIndex])}");
        text.AppendLine("Espace : pause   1 à 4 : vitesse   B : logements (200 pierre, 100 bois)   C : camp de base");
        text.AppendLine();

        foreach (var socialClass in world.Classes)
        {
            text.AppendLine(
                $"{socialClass.Name,-12} {socialClass.Ideology.Name,-16} population {socialClass.Population,7:N0}   " +
                $"richesse {socialClass.Wealth,10:N0}   besoins couverts {socialClass.NeedsSatisfaction,4:P0}   " +
                $"adhésion {socialClass.Adhesion,4:P0}");
        }

        text.AppendLine();
        foreach (var commune in world.Communes)
        {
            var jobs = world.Structures.Values
                .Where(s => s.Jobs > 0 && Demos.Domain.Politics.Communes.CommuneOf(s, world.Structures) == commune.Id)
                .ToList();
            var filled = jobs.Sum(s => world.Workers.GetValueOrDefault(s.Id));
            var state = world.IsGhostTown(commune.Id) ? "   VILLE FANTÔME (bâtiments en ruine)" : "";
            text.AppendLine(
                $"{commune.Name,-18} habitants {world.Residents(commune.Id),7:N0}   " +
                $"postes pourvus {filled,6:N0} / {jobs.Sum(s => s.Jobs):N0}{state}");
        }

        text.AppendLine();
        text.Append("Légitimité :");
        foreach (var pole in Orientation.All)
            text.Append($"   {pole.Code} {world.Legitimacy[pole],7:F1}");
        text.AppendLine();
        text.AppendLine();
        text.AppendLine($"Trésor : {world.Treasury.Balance:N0}");
        text.AppendLine($"Stock de l'État : {DescribeQuantities(world.StateStock.Contents)}");
        text.AppendLine($"Marché : {DescribeQuantities(world.Market.Contents)}");
        text.AppendLine();
        text.AppendLine($"Logements construits : {_housingBuilt}   {_lastMessage}");
        return text.ToString();
    }

    private static string DescribeQuantities(IReadOnlyDictionary<string, double> quantities) =>
        quantities.Count == 0
            ? "vide"
            : string.Join("   ", quantities.Select(item => $"{item.Key} {item.Value:N0}"));

    private static string SeasonName(Season season) => season switch
    {
        Season.Winter => "Hiver",
        Season.Spring => "Printemps",
        Season.Summer => "Été",
        _ => "Automne",
    };
}
