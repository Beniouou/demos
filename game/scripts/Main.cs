using System.Linq;
using Demos.Application;
using Demos.Domain.Economy;
using Demos.Infrastructure;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

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
    private GameView _view = null!;
    private Hud _hud = null!;
    private PopupMenu _polePicker = null!;
    private bool _paused = true;
    private int _speedIndex;
    private double _pendingDays;
    private int _housingBuilt;

    public override void _Ready()
    {
        var content = new JsonContentSource(fileName => FileAccess.GetFileAsString($"res://data/{fileName}"));
        _simulation = PrototypeScenario.Create(content);

        // Sous un CanvasLayer, l'interface se cale sur la fenêtre (sous un simple Node, elle n'aurait pas de taille).
        var interfaceLayer = new CanvasLayer();
        AddChild(interfaceLayer);

        _hud = new Hud();
        interfaceLayer.AddChild(_hud);
        // L'interface du joueur ne reçoit que la vue en lecture seule et le bureau du joueur, dont chaque décision
        // passe par la simulation : elle ne touche jamais le moteur.
        _view = new GameView(_simulation);
        _hud.Init(_view, new PlayerDesk(_simulation), SpeedLabels);
        _hud.TopBar.PauseToggled += () => _paused = !_paused;
        _hud.TopBar.SpeedChosen += speed => _speedIndex = speed;
        _hud.BuildHousingPressed += BuildHousing;
        _hud.FoundCommunePressed += ChoosePole;

        // Choix du pôle de légitimité à la fondation d'une commune : il devient sa charte de fondation.
        _polePicker = new PopupMenu { Title = "Charte de fondation : quel pôle payer ?" };
        _polePicker.IdPressed += id => FoundCommune(Orientation.All.ElementAt((int)id));
        AddChild(_polePicker);
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

        _hud.TopBar.Refresh(_view, _paused, _speedIndex);
        _hud.Menu.Refresh();
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
            case Key.C: ChoosePole(); break;
            case Key.Escape: _hud.Menu.Close(); break;
        }
    }

    private void BuildHousing()
    {
        var id = $"logements-etat-{_housingBuilt + 1}";
        var housing = _simulation.World.BuildingTypes["logements"]
            .Build(id, PrototypeScenario.FirstCommuneId, name: $"Logements d'État {_housingBuilt + 1}");

        if (_simulation.TryBuild(housing, HousingCost, PaymentMode.StockFirst))
        {
            _housingBuilt++;
            _hud.ShowMessage($"{housing.Name} construits ({_housingBuilt} en tout).");
        }
        else
        {
            _hud.ShowMessage("Construction impossible : ni le stock, ni le trésor, ni le marché ne suffisent.");
        }
    }

    /// <summary>Ouvre le choix du pôle : ceux où la légitimité ne suffit pas sont grisés.</summary>
    private void ChoosePole()
    {
        _polePicker.Clear();
        var index = 0;
        foreach (var pole in Orientation.All)
        {
            var available = _simulation.World.Legitimacy[pole];
            _polePicker.AddItem($"{pole.Code} — {WorldText.PoleName(pole)} (légitimité {available:F0})", index);
            _polePicker.SetItemDisabled(index, available < BaseCampLegitimacyCost);
            index++;
        }

        _polePicker.PopupCentered();
    }

    private void FoundCommune(Orientation pole)
    {
        var name = $"Commune {_simulation.World.Communes.Count() + 1}";

        var commune = _simulation.TryFoundCommune(
            name, PrototypeScenario.FirstCommuneId, BaseCampCost, PaymentMode.StockFirst, pole, BaseCampLegitimacyCost);
        _hud.ShowMessage(commune is null
            ? $"Camp de base impossible : il faut 100 pierre, 100 bois, {BaseCampLegitimacyCost} de légitimité " +
              $"et {Simulation.FoundingGrantOf(_simulation.World.BuildingTypes[Simulation.BaseCampTypeId]):N0} de dotation au trésor."
            : $"{commune.Name} fondée, charte {WorldText.PoleName(pole)} ({pole.Code}), caisse de {_simulation.World.CommuneFundOf(commune.Id).Balance:N0}.");
    }
}
