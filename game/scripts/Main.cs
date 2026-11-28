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

    // Vrai une fois la partie créée, après l'écran « Nouvelle partie ».
    private bool _started;

    // Le contenu du jeu, gardé pour la refondation (les Promesses et les engagements du catalogue).
    private JsonContentSource _content = null!;

    /// <summary>
    /// D'abord l'écran « Nouvelle partie » (carte, mode, puis La Promesse et le serment ; 24 et 27 novembre 2026) ; la
    /// partie n'est créée qu'après ces choix.
    /// </summary>
    public override void _Ready()
    {
        // « -- --carte <nom> » en ligne de commande : la partie commence tout de suite sur cette carte (essais, captures) ;
        // avec « --exil <promesse> » (« eg-ec-co »), en départ d'exil avec cette Promesse et son premier engagement par axe.
        var args = OS.GetCmdlineUserArgs();
        var asked = System.Array.IndexOf(args, "--carte");
        if (asked >= 0 && asked + 1 < args.Length)
        {
            var exile = TestExile(args);
            StartGame(new NewGameChoice(args[asked + 1], exile, exile is null ? null : StartingLaws.None));
            return;
        }

        var layer = new CanvasLayer();
        AddChild(layer);
        var screen = new NewGameScreen();
        screen.Init();
        layer.AddChild(screen);
        // Essais : « --assistant » passe l'écran du mode, en mode avec La Promesse (captures).
        if (System.Array.IndexOf(args, "--assistant") >= 0)
            screen.CallDeferred(NewGameScreen.MethodName.SkipToPromise);
        screen.Started += choice =>
        {
            layer.QueueFree();
            StartGame(choice);
        };
    }

    /// <summary>Le départ d'exil des essais en ligne de commande (« --exil eg-ec-co »), ou null.</summary>
    private static ExileStart? TestExile(string[] args)
    {
        var asked = System.Array.IndexOf(args, "--exil");
        if (asked < 0 || asked + 1 >= args.Length)
            return null;
        var content = new JsonContentSource(fileName => FileAccess.GetFileAsString($"res://data/{fileName}"));
        var promise = content.LoadPromises().FirstOrDefault(p => p.Id == args[asked + 1]);
        if (promise is null)
            return null;
        var catalogue = content.LoadEngagements();
        var engagements = promise.Orientations.Select(pole => catalogue.First(e => e.Kept && e.Orientation == pole)).ToList();
        return new ExileStart("Essai", promise, engagements);
    }

    /// <summary>Crée la partie (carte, départ, Promesse, lois de l'An I), puis l'écran de jeu.</summary>
    private void StartGame(NewGameChoice choice)
    {
        _content = new JsonContentSource(fileName => FileAccess.GetFileAsString($"res://data/{fileName}"),
            fileName => FileAccess.GetFileAsBytes($"res://data/{fileName}"), choice.MapName);
        var content = _content;
        _simulation = PrototypeScenario.Create(content, choice.Laws, choice.Exile);

        // Sous un CanvasLayer, l'interface se cale sur la fenêtre (sous un simple Node, elle n'aurait pas de taille).
        var interfaceLayer = new CanvasLayer();
        AddChild(interfaceLayer);

        _hud = new Hud();
        interfaceLayer.AddChild(_hud);
        // L'interface du joueur ne reçoit que la vue en lecture seule et le bureau du joueur, dont chaque décision
        // passe par la simulation : elle ne touche jamais le moteur.
        _view = new GameView(_simulation);
        _desk = new PlayerDesk(_simulation);
        _hud.Init(_view, _desk, SpeedLabels, content.MapName);
        _hud.TopBar.PauseToggled += () => _paused = !_paused;
        _hud.TopBar.SpeedChosen += speed => _speedIndex = speed;
        // Un clic sur une commune de la carte ouvre le menu Communes sur elle ; sur une préfecture, sa fiche dans Structure.
        _hud.Map.StructureClicked += _hud.Menu.ShowClicked;
        // La carte suit la commune du menu Communes (flèches, clic, bâtiment d'une autre commune).
        _hud.Menu.Focus.Changed += _hud.Map.CenterOnCommune;

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
        // La Promesse : une trahison dans la barre du bas ; le pacte déchiré met en pause et ouvre ses trois choix.
        _simulation.PromiseBetrayed += (engagement, cause) =>
            _events.Add($"La Promesse est trahie : « {engagement.Text} » ({cause}). L'encre de l'article pâlit.");
        _simulation.PactTorn += ShowPactTorn;
        _simulation.CompaniesNationalizedByLaw += taken => _events.Add(EventText.NationalizedByLaw(_view, taken));
        _simulation.SeasonPassed += () => _events.Add(EventText.SeasonStarted(_view));
        _simulation.YearPassed += () =>
        {
            _events.Add(EventText.YearEnded(_view, _view.Today.Year - 1, _yearStartTreasury, _yearStartResidents));
            _yearStartTreasury = _view.TreasuryBalance;
            _yearStartResidents = _view.TotalResidents;
        };
        _started = true;

        // L'arrivée en exil : le jeu est en pause, la fenêtre dit ce que les exilés ont et ce qui leur manque.
        if (choice.Exile is not null)
        {
            var arrival = new AcceptDialog
            {
                Theme = UiTheme.Create(),
                Title = "Arrivée en exil",
                OkButtonText = "Commencer",
                DialogAutowrap = true,
                DialogText = ArrivalText.Of(_view, PrototypeScenario.FirstCommuneId),
                MinSize = new Vector2I(620, 0),
            };
            AddChild(arrival);
            arrival.PopupCentered();
        }

        // Essais : « --menu "La Promesse" » ouvre ce menu dès le départ (captures).
        var args = OS.GetCmdlineUserArgs();
        var menu = System.Array.IndexOf(args, "--menu");
        if (menu >= 0 && menu + 1 < args.Length)
            _hud.Menu.OpenMenu(args[menu + 1]);
    }

    /// <summary>
    /// Le pacte vient de se déchirer : pause, et les trois choix (terminer avec un bilan, continuer sans promesse,
    /// refonder avec un nouveau serment).
    /// </summary>
    private void ShowPactTorn()
    {
        _paused = true;
        _pendingDays = 0;
        var layer = new CanvasLayer { Layer = 10 };
        AddChild(layer);
        var screen = new PactTornScreen(_view);
        layer.AddChild(screen);
        screen.Ended += () =>
        {
            var summary = new AcceptDialog
            {
                Theme = UiTheme.Create(),
                Title = "Fin de la partie",
                OkButtonText = "Nouvelle partie",
                DialogAutowrap = true,
                DialogText = $"Le pacte est déchiré en l'An {_view.Today.Year - 1899} de l'exil.\n\n"
                    + $"Habitants : {_view.TotalResidents:N0}. Trésor : {_view.TreasuryBalance:N0}.",
                MinSize = new Vector2I(480, 0),
            };
            layer.AddChild(summary);
            summary.Confirmed += () => GetTree().ReloadCurrentScene();
            summary.Canceled += () => GetTree().ReloadCurrentScene();
            summary.PopupCentered();
        };
        screen.Continued += () =>
        {
            _hud.ShowMessage(_desk.ContinueWithoutPromise());
            layer.QueueFree();
        };
        screen.Refounding += () =>
        {
            screen.Visible = false;
            var wizard = new PromiseWizard(_content.LoadPromises(), _content.LoadEngagements(), _content.LoadLawCatalogue(),
                withLaws: false, peopleName: _view.Promise()?.PeopleName ?? "");
            layer.AddChild(wizard);
            wizard.Cancelled += () =>
            {
                wizard.QueueFree();
                screen.Visible = true;
            };
            wizard.Finished += chosen =>
            {
                wizard.QueueFree();
                var oath = new OathScreen(chosen, []);
                layer.AddChild(oath);
                oath.Signed += signature =>
                {
                    _hud.ShowMessage(_desk.Refound(chosen.Promise, chosen.Engagements, signature));
                    layer.QueueFree();
                };
            };
        };
    }

    public override void _Process(double delta)
    {
        if (!_started)
            return;

        if (!_paused)
        {
            _pendingDays += delta / SecondsPerDay[_speedIndex];
            // La boucle s'arrête si une journée met le jeu en pause (des lois entrées en vigueur).
            while (_pendingDays >= 1 && !_paused)
            {
                _pendingDays -= 1;
                _simulation.AdvanceDay();
                // Les relevés des produits (Communes → Statistiques) et de la population (Population → Évolution) : chaque jour
                // joué, tassés à un par mois au-delà d'un an (décidé le 21 novembre 2026).
                _hud.Menu.ProductionHistory.Record(_view);
                _hud.Menu.History.Record(_view);
                // Le stock de légitimité (Population → Légitimité) : chaque jour, tassé à un par mois au-delà de 30 jours.
                _hud.Menu.LegitimacyHistory.Record(_view);
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
        if (!_started)
            return;
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
            // Avant de fonder : le jeu prévient si fonder trahit La Promesse (« aucune nouvelle terre »), sans l'interdire.
            position => PromiseWarning.Check(this, _view.PromiseBreachesOfFounding(), "Fonder", "fonder une commune", () =>
            {
                var answer = _desk.FoundCommune(name, payingCommuneId, pole, position);
                if (answer.Id is { } communeId)
                    _hud.Map.AddCommune(communeId, name.Trim(),
                        new Demos.Domain.Map.MapCoordinates((int)(position.X / 16.6), (int)(position.Y / 16.0)));
                _hud.ShowMessage(answer.Message);
            }),
            Balance.Current.CommuneMinDistance));
    }
}
