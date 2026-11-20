using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Map;
using Godot;

namespace Demos.Game;

/// <summary>
/// Communes → Transport, en lecture : les liaisons de la commune du menu (sentiers, chemins, routes, vers une autre
/// commune ou vers un de ses bâtiments posés sur la carte) avec leur longueur, leur
/// montée, qui les paie, la part de perte de transport qu'elles retirent, leur entretien et la durée d'un trajet ; la
/// capacité d'envoi de la commune et les chargements en route ; puis ce qu'a changé le dernier bilan des routes, et la règle. Les routes naissent du trafic : le joueur ne les construit pas.
/// </summary>
public partial class TransportPage : VBoxContainer, IMenuPage
{
    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private readonly CommuneFocus _focus = null!;
    private readonly RichTextLabel _text = null!;
    private readonly Label _wanted = null!;
    private readonly Label _answer = null!;

    public TransportPage(GameView view, PlayerDesk desk, CommuneFocus focus)
    {
        _view = view;
        _desk = desk;
        _focus = focus;
        AddThemeConstantOverride("separation", 6);

        // Les charrettes voulues pour la commune (décidé le 20 novembre 2026) : − et + changent le nombre ; l'État de la
        // commune achète ce qui manque.
        var carts = new HBoxContainer();
        carts.AddThemeConstantOverride("separation", 6);
        carts.AddChild(new Label { Text = "Charrettes voulues", TooltipText = CartsHint, MouseFilter = MouseFilterEnum.Pass });
        var less = new Button { Text = "−", FocusMode = FocusModeEnum.None, TooltipText = "Une charrette de moins" };
        less.Pressed += () => ChangeCarts(-1);
        carts.AddChild(less);
        _wanted = new Label { CustomMinimumSize = new Vector2(32, 0), HorizontalAlignment = HorizontalAlignment.Center };
        carts.AddChild(_wanted);
        var more = new Button { Text = "+", FocusMode = FocusModeEnum.None, TooltipText = "Une charrette de plus" };
        more.Pressed += () => ChangeCarts(1);
        carts.AddChild(more);
        _answer = UiTheme.Caption("");
        carts.AddChild(_answer);
        AddChild(carts);

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

    private const string CartsHint =
        "Une charrette ajoute de la capacité d'envoi à la commune. Elle se fabrique à l'atelier (2 planches et 1 outil) et "
        + "s'use. L'État de la commune garde le nombre voulu : il prend d'abord les charrettes qu'il a produites, puis achète "
        + "ce qui manque au marché, avec la caisse de la commune puis le trésor.";

    private void ChangeCarts(int step)
    {
        if (_focus.Id is not { } communeId)
            return;
        _answer.Text = _desk.SetWantedCarts(communeId, _view.WantedCartsOf(communeId) + step).Message;
    }

    public void Refresh()
    {
        if (_focus.Id is not { } communeId)
            return;
        var wanted = $"{_view.WantedCartsOf(communeId)}";
        if (_wanted.Text != wanted)
            _wanted.Text = wanted;
        var text = Describe();
        // Le texte n'est remis que s'il a changé : sinon la fiche remonterait en haut à chaque image.
        if (text != _text.Text)
            _text.Text = text;
    }

    private string Describe()
    {
        if (_focus.Id is not { } communeId)
            return SheetText.Muted("Aucune commune sélectionnée.");
        var settings = Balance.Current;
        // Les liaisons de la commune du menu : vers une autre commune, ou vers un de ses bâtiments posés sur la carte.
        bool Touches(string place) => place == communeId || _view.CommuneOf(place) == communeId;
        var links = _view.Roads().Where(l => Touches(l.A) || Touches(l.B)).OrderByDescending(l => l.Level).ThenBy(l => RoadText.Link(_view, l)).ToList();
        var text = new StringBuilder(SheetText.Heading($"LIAISONS ({links.Count})"));
        if (links.Count == 0)
            text.AppendLine(SheetText.Muted("Aucune pour cette commune : un sentier s'ouvre quand deux communes échangent assez en un mois, ou quand un bâtiment posé sur la carte produit assez."));
        else
        {
            var muted = UiTheme.Muted.ToHtml(false);
            var communes = _view.Communes().Select(c => c.Id).ToHashSet();
            text.Append("[table=8]");
            foreach (var header in new[] { "Liaison", "Niveau", "Longueur", "Montée", "Payée par", "Perte retirée", "Entretien / an", "Durée" })
                text.Append($"[cell padding=0,0,16,4][color=#{muted}]{header}[/color][/cell]");
            foreach (var link in links)
            {
                text.Append($"[cell padding=0,0,16,2]{SheetText.Structure(_view, link.A)} – {SheetText.Structure(_view, link.B)}[/cell]");
                text.Append($"[cell padding=0,0,16,2]{RoadText.LevelName(link.Level)}[/cell]");
                text.Append($"[cell padding=0,0,16,2]{link.Length:0.#} km[/cell]");
                text.Append($"[cell padding=0,0,16,2]{link.Climb:N0} m[/cell]");
                text.Append($"[cell padding=0,0,16,2]{SheetText.Escape(_view.NameOf(link.PayerId))}[/cell]");
                // Vers un bâtiment, pas de perte de transport : rien à retirer.
                var betweenCommunes = communes.Contains(link.A) && communes.Contains(link.B);
                text.Append($"[cell padding=0,0,16,2]{(betweenCommunes ? $"{RoadText.LossCut(link.Level):P0}" : "—")}[/cell]");
                text.Append($"[cell padding=0,0,16,2]{Roads.Upkeep(link, settings):N0}[/cell]");
                // Entre deux communes, la durée du trajet ; vers un bâtiment, le trajet est instantané.
                text.Append($"[cell padding=0,0,16,2]{(betweenCommunes ? Days(_view.TravelDays(link.A, link.B)) : "—")}[/cell]");
            }
            text.AppendLine("[/table]");
        }

        AppendCapacity(text, settings, communeId);
        AppendCarts(text, settings, communeId);
        AppendShipments(text, communeId);

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

    private static string Days(int days) => days == 1 ? "1 jour" : $"{days} jours";

    /// <summary>La capacité d'envoi de la commune vers les autres, et ce qui en a servi le dernier jour.</summary>
    private void AppendCapacity(StringBuilder text, BalanceSettings settings, string communeId)
    {
        var hint = "Ce que la commune peut envoyer par jour vers les autres communes (échanges et lignes d'approvisionnement), "
            + $"tous produits confondus : {settings.ShippingCapacityLevel1:N0} par grenier, entrepôt ou silo, "
            + $"{settings.ShippingCapacityLevel2:N0} par dépôt logistique. Quand elle ne suffit pas, chaque envoi est réduit au prorata. "
            + "Une commune sans bâtiment logistique peut recevoir d'une commune qui en a un, et lui envoyer avec la capacité de celle-ci.";
        text.Append(SheetText.Section("Capacité d'envoi", hint));
        var capacity = _view.ShippingCapacityOf(communeId);
        if (capacity.PerDay <= 0)
        {
            text.AppendLine(SheetText.Muted("Aucun bâtiment logistique : la commune n'échange qu'avec une commune qui en a un."));
            return;
        }

        var used = capacity.UsedLastDay;
        var carts = capacity.FromCarts > 0 ? $" · charrettes : {capacity.FromCarts:N0}" : "";
        text.AppendLine($"{SheetText.Escape(string.Join(", ", capacity.Buildings))} : {capacity.PerDay - capacity.FromCarts:N0}{carts} par jour · "
            + $"utilisée hier : {used:N0} ({used / capacity.PerDay:P0})");
    }

    /// <summary>Les charrettes de la commune : en service, voulues, ce qu'elles ajoutent, leur usure et leur prix.</summary>
    private void AppendCarts(StringBuilder text, BalanceSettings settings, string communeId)
    {
        text.Append(SheetText.Section("Charrettes", CartsHint));
        var cartsView = _view.CartsOf(communeId);
        text.AppendLine(cartsView.Wanted == 0 && cartsView.InService < 0.05
            ? SheetText.Muted("Aucune charrette voulue : réglez le nombre avec − et + en haut de la page.")
            : $"{cartsView.Wanted} voulues · {cartsView.InService:N1} en service · +{cartsView.InService * settings.CartCapacity:N0} par jour de capacité d'envoi · "
                + SheetText.Muted($"usure : 1 tous les {(cartsView.InService > 0 ? cartsView.UsageDays / cartsView.InService : cartsView.UsageDays):N0} jours environ · "
                    + $"prix {cartsView.Price:N2}"));
    }

    /// <summary>Les chargements partis de la commune et encore en route, puis ce qui est en route vers elle.</summary>
    private void AppendShipments(StringBuilder text, string communeId)
    {
        var hint = "Ce qui est parti vers une autre commune et n'est pas encore arrivé, après la perte en route. Un chargement "
            + "met la distance divisée par la vitesse de la liaison (moins sur un chemin, encore moins sur une route), et se "
            + "périme chaque nuit comme dans un stockage général. Il arrive le matin.";
        var leaving = _view.ShipmentsFrom(communeId);
        text.Append(SheetText.Section($"Chargements en route ({leaving.Count})", hint));
        if (leaving.Count == 0)
            text.AppendLine(SheetText.Muted("Aucun chargement ne part d'ici."));
        else
        {
            var muted = UiTheme.Muted.ToHtml(false);
            text.Append("[table=5]");
            foreach (var header in new[] { "Produit", "Quantité", "Vers", "Parti le", "Arrive le" })
                text.Append($"[cell padding=0,0,16,4][color=#{muted}]{header}[/color][/cell]");
            foreach (var shipment in leaving)
            {
                var to = shipment.ClientName is { } client ? $"{shipment.DestinationName} ({client})" : shipment.DestinationName;
                text.Append($"[cell padding=0,0,16,2]{SheetText.Escape(shipment.ProductName)}[/cell]");
                text.Append($"[cell padding=0,0,16,2]{shipment.Quantity:N0}[/cell]");
                text.Append($"[cell padding=0,0,16,2]{SheetText.Escape(to)}[/cell]");
                text.Append($"[cell padding=0,0,16,2]{shipment.DepartedOn:dd/MM/yyyy}[/cell]");
                text.Append($"[cell padding=0,0,16,2]{shipment.ArrivesOn:dd/MM/yyyy}[/cell]");
            }
            text.AppendLine("[/table]");
        }

        var coming = _view.ShipmentsTo(communeId);
        text.AppendLine(coming.Count == 0
            ? SheetText.Muted("Arrivent ici : rien.")
            : $"Arrivent ici : {string.Join(" · ", coming.GroupBy(s => s.ProductName).Select(g => $"{SheetText.Escape(g.Key)} {g.Sum(s => s.Quantity):N0}"))}");
    }
}
