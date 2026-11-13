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

    private Simulation _simulation = null!;
    private GameView _view = null!;
    private PlayerDesk _desk = null!;
    private Hud _hud = null!;
    private bool _paused = true;
    private int _speedIndex;
    private double _pendingDays;
    private System.Collections.Generic.IReadOnlyList<Demos.Domain.Map.RoadChange>? _lastRoadChanges;
    // Les messages des événements des journées jouées dans cette image, montrés ensemble dans la barre du bas.
    private readonly System.Collections.Generic.List<string> _events = [];
    // Le trésor et les habitants au 1er janvier, pour le bilan de l'année.
    private double _yearStartTreasury;
    private double _yearStartResidents;

    public override void _Ready()
    {
        var content = new JsonContentSource(fileName => FileAccess.GetFileAsString($"res://data/{fileName}"),
            fileName => FileAccess.GetFileAsBytes($"res://data/{fileName}"));
        _simulation = PrototypeScenario.Create(content);

        // Sous un CanvasLayer, l'interface se cale sur la fenêtre (sous un simple Node, elle n'aurait pas de taille).
        var interfaceLayer = new CanvasLayer();
        AddChild(interfaceLayer);

        _hud = new Hud();
        interfaceLayer.AddChild(_hud);
        // L'interface du joueur ne reçoit que la vue en lecture seule et le bureau du joueur, dont chaque décision
        // passe par la simulation : elle ne touche jamais le moteur.
        _view = new GameView(_simulation);
        _desk = new PlayerDesk(_simulation);
        _hud.Init(_view, _desk, SpeedLabels);
        _hud.TopBar.PauseToggled += () => _paused = !_paused;
        _hud.TopBar.SpeedChosen += speed => _speedIndex = speed;
        // Un clic sur une commune ou une préfecture de la carte ouvre sa fiche dans le menu Structure.
        _hud.Map.StructureClicked += _hud.Menu.ShowStructure;

        // Fonder une commune : Structure → Création d'entité politique (aussi par la touche C ou le bouton Camp de base),
        // puis la carte pour l'emplacement.
        _hud.FoundCommunePressed += _hud.Menu.ShowCommuneCreation;
        _hud.Menu.CommuneSiteRequested += ChooseCommuneSite;

        // Au 1er du mois, des lois votées entrent en vigueur : le jeu se met en pause et les montre (règle des lois).
        var enactedDialog = new LawsEnactedDialog(UiTheme.Create());
        AddChild(enactedDialog);
        _simulation.LawsEnacted += enacted =>
        {
            _paused = true;
            _pendingDays = 0;
            enactedDialog.ShowEnacted(_view, enacted);
        };

        // Les événements de la simulation : un message dans la barre du bas.
        _yearStartTreasury = _view.TreasuryBalance;
        _yearStartResidents = _view.TotalResidents;
        _simulation.BuildingCompleted += building => _events.Add(EventText.BuildingCompleted(_view, building));
        _simulation.CompaniesNationalizedByLaw += taken => _events.Add(EventText.NationalizedByLaw(taken));
        _simulation.SeasonPassed += () => _events.Add(EventText.SeasonStarted(_view));
        _simulation.YearPassed += () =>
        {
            _events.Add(EventText.YearEnded(_view, _view.Today.Year - 1, _yearStartTreasury, _yearStartResidents));
            _yearStartTreasury = _view.TreasuryBalance;
            _yearStartResidents = _view.TotalResidents;
        };
    }

    public override void _Process(double delta)
    {
        if (!_paused)
        {
            _pendingDays += delta / SecondsPerDay[_speedIndex];
            // La boucle s'arrête si une journée met le jeu en pause (des lois entrées en vigueur).
            while (_pendingDays >= 1 && !_paused)
            {
                _pendingDays -= 1;
                _simulation.AdvanceDay();
                ShowRoadChanges();
            }

            // Plusieurs événements le même jour (le 1er janvier : routes, saison, bilan) : tous sur la même ligne.
            if (_events.Count > 0)
            {
                _hud.ShowMessage(string.Join("  ·  ", _events));
                _events.Clear();
            }
        }

        _hud.TopBar.Refresh(_view, _paused, _speedIndex);
        _hud.Menu.Refresh();
        _hud.Map.Refresh(_view);
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
            case Key.C: _hud.Menu.ShowCommuneCreation(); break;
            case Key.Escape when _hud.Map.IsPlacing:
                _hud.Map.CancelPlacement();
                _hud.ShowMessage("Placement annulé.");
                break;
            case Key.Escape: _hud.Menu.Close(); break;
        }
    }

    /// <summary>
    /// Après un bilan des routes (1er du mois, 1er janvier), ce qui a changé s'affiche dans la barre du bas. La simulation
    /// remplace sa liste à chaque bilan : une autre liste, non vide, est un nouveau bilan à montrer.
    /// </summary>
    private void ShowRoadChanges()
    {
        var changes = _view.RoadChanges;
        if (ReferenceEquals(changes, _lastRoadChanges))
            return;
        _lastRoadChanges = changes;
        if (changes.Count > 0)
            _events.Add(RoadText.Message(_view, changes));
    }

    /// <summary>
    /// La fenêtre de fondation est remplie : le menu se ferme et la carte passe en mode pose. Un clic sur un emplacement
    /// permis fonde la commune ; le résultat s'affiche dans la barre du bas.
    /// </summary>
    private void ChooseCommuneSite(string name, string payingCommuneId, Orientation pole)
    {
        _hud.Menu.Close();
        _hud.Map.BeginPlacement(new Demos.Game.Map.MapCanvas.PlacementRequest(
            $"camp de base de {name}, à {Demos.Domain.Map.MapScale.Format(Balance.Current.CommuneMinDistance)} au moins des autres communes",
            [],
            payingCommuneId,
            0,
            _desk.CommuneSiteProblem,
            position =>
            {
                var answer = _desk.FoundCommune(name, payingCommuneId, pole, position);
                if (answer.Id is { } communeId)
                    _hud.Map.AddCommune(communeId, name.Trim(),
                        new Demos.Domain.Map.MapCoordinates((int)(position.X / 16.6), (int)(position.Y / 16.0)));
                _hud.ShowMessage(answer.Message);
            },
            Balance.Current.CommuneMinDistance));
    }
}
