using System.Linq;
using Demos.Application;
using Demos.Domain.Politics;
using Godot;

namespace Demos.Game;

/// <summary>
/// Les lignes d'approvisionnement d'un bâtiment, dans Production → Unités de production : celles qui le livrent (avec
/// la part envoyée, à changer par pas de 10 %, leur priorité ↑ ↓, et un bouton pour la retirer), celles qu'il livre, et l'ajout d'un
/// fournisseur parmi les bâtiments publics, de toutes les communes, qui produisent une de ses matières. Le résultat
/// d'une action remonte par <see cref="Message"/>.
/// </summary>
public partial class SupplyLinesPanel : VBoxContainer
{
    private const string SupplyHint =
        "Une ligne relie deux bâtiments publics : chaque jour, une part de la production du fournisseur part d'abord au "
        + "client, avant d'être mise en vente. Le client ne reçoit que ce qu'il lui faut pour une journée à plein régime, "
        + "moins sa réserve ; le reste reste en vente. Il paie au prix du marché, sauf dans sa propre commune. Entre "
        + "communes, une part se perd en route (moins avec un chemin ou une route). Les lignes d'un client passent dans l'ordre de leur priorité (↑ ↓).";

    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private string _key = "";

    /// <summary>Le résultat d'une action (ligne créée, retirée, part changée).</summary>
    public event System.Action<string>? Message;

    public SupplyLinesPanel(GameView view, PlayerDesk desk)
    {
        _view = view;
        _desk = desk;
        AddThemeConstantOverride("separation", 4);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public SupplyLinesPanel() { }

    /// <summary>Refait la boîte quand le bâtiment ou ses lignes changent (une fois par jour au plus).</summary>
    public void Refresh(string? buildingId)
    {
        var flows = buildingId is null ? null : _view.BuildingFlows(buildingId);
        var ownership = buildingId is null ? null : _view.OwnershipOf(buildingId);
        var lines = flows is null ? [] : _view.SupplyLines().Where(l => l.ClientId == buildingId || l.SupplierId == buildingId).ToList();
        var candidates = flows is not null && ownership == Ownership.Public ? _view.SupplierCandidates(buildingId!) : [];
        var key = flows is null ? "" : $"{buildingId}|{flows.ProductId}|{_view.Today}|{string.Join(",", lines.Select(l => $"{l.Id}:{l.Share}"))}|{candidates.Count}";
        if (key == _key)
            return;
        _key = key;

        foreach (var child in GetChildren())
            child.QueueFree();
        Visible = flows is not null && (flows.ProductId is not null || lines.Count > 0);
        if (!Visible)
            return;

        var title = new HintText { BbcodeEnabled = true, FitContent = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeColorOverride("default_color", UiTheme.Text);
        title.Text = SheetText.Section("Lignes d'approvisionnement", SupplyHint);
        AddChild(title);

        if (ownership != Ownership.Public)
        {
            AddChild(Note("Les lignes ne relient que des bâtiments publics : ce bâtiment achète ses matières sur le marché."));
            return;
        }

        var incoming = lines.Where(l => l.ClientId == buildingId).ToList();
        var outgoing = lines.Where(l => l.SupplierId == buildingId).ToList();
        if (incoming.Count == 0 && outgoing.Count == 0)
            AddChild(Note("Aucune ligne : il achète ses matières et vend sa production sur le marché de sa commune."));
        // Les lignes qui le livrent, dans l'ordre de leur priorité ; ↑ ↓ le changent s'il en a plusieurs.
        for (var index = 0; index < incoming.Count; index++)
        {
            var line = incoming[index];
            AddChild(LineRow(line, $"← {line.SupplierName} ({line.SupplierCommune})", incoming.Count > 1 ? (index > 0, index < incoming.Count - 1) : null));
        }
        foreach (var line in outgoing)
            AddChild(LineRow(line, $"→ {line.ClientName} ({line.ClientCommune})"));

        if (!flows!.HasMaterials)
            return;
        var add = new HBoxContainer();
        add.AddThemeConstantOverride("separation", 8);
        var choice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, FitToLongestItem = false, ClipText = true };
        foreach (var candidate in candidates)
            choice.AddIconItem(UiTheme.Square(UiTheme.OwnershipColor(Ownership.Public), 12, 12), $"{candidate.Name} ({candidate.CommuneName})"
                + (candidate.Loss > 0 ? $" · perte {candidate.Loss:P0}" : ""));
        if (candidates.Count == 0)
            choice.AddItem(incoming.Count > 0 ? "Aucun autre bâtiment public ne produit ses matières" : "Aucun bâtiment public ne produit ses matières");
        var create = new Button { Text = "Ajouter ce fournisseur", FocusMode = FocusModeEnum.None, Disabled = candidates.Count == 0 };
        create.Pressed += () =>
        {
            if (choice.Selected >= 0 && choice.Selected < candidates.Count)
                Report(_desk.AddSupplyLine(candidates[choice.Selected].Id, buildingId!).Message);
        };
        add.AddChild(choice);
        add.AddChild(create);
        AddChild(add);
    }

    private static Label Note(string text)
    {
        var label = UiTheme.Caption(text);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    /// <summary>Une ligne : de qui ou vers qui, ce qui est arrivé hier, la perte, la part (− / +), Retirer.</summary>
    private Control LineRow(SupplyLineView line, string who, (bool Up, bool Down)? order = null)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        if (order is { } moves)
        {
            var up = new Button { Text = "↑", FocusMode = FocusModeEnum.None, TooltipText = "Priorité plus haute : cette ligne passe avant", Disabled = !moves.Up };
            up.Pressed += () => Report(_desk.MoveSupplyLine(line.Id, earlier: true).Message);
            row.AddChild(up);
            var down = new Button { Text = "↓", FocusMode = FocusModeEnum.None, TooltipText = "Priorité plus basse : cette ligne passe après", Disabled = !moves.Down };
            down.Pressed += () => Report(_desk.MoveSupplyLine(line.Id, earlier: false).Message);
            row.AddChild(down);
        }
        // Les lignes ne relient que des bâtiments d'État : le carré de l'État devant l'autre bâtiment.
        row.AddChild(new TextureRect { Texture = UiTheme.Square(UiTheme.OwnershipColor(Ownership.Public), 12, 12), StretchMode = TextureRect.StretchModeEnum.KeepCentered });
        var text = $"{who} · arrivé hier {line.DeliveredLastDay:N0}"
            + (line.Loss > 0 ? $" · perte {line.Loss:P0}" : "")
            + (line.IsActive ? "" : " · ne livre plus (recette changée)");
        row.AddChild(new Label { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true, TooltipText = text, MouseFilter = MouseFilterEnum.Pass });

        var less = new Button { Text = "−", FocusMode = FocusModeEnum.None, TooltipText = "Envoyer 10 % de sa production en moins", Disabled = line.Share <= 0.05 };
        less.Pressed += () => Report(_desk.SetSupplyLineShare(line.Id, System.Math.Round(line.Share - 0.1, 2)).Message);
        row.AddChild(less);
        row.AddChild(new Label { Text = $"{line.Share:P0}", CustomMinimumSize = new Vector2(48, 0), HorizontalAlignment = HorizontalAlignment.Center,
            TooltipText = "La part de la production du fournisseur que la ligne envoie", MouseFilter = MouseFilterEnum.Pass });
        var more = new Button { Text = "+", FocusMode = FocusModeEnum.None, TooltipText = "Envoyer 10 % de sa production en plus", Disabled = line.Share >= 1 };
        more.Pressed += () => Report(_desk.SetSupplyLineShare(line.Id, System.Math.Round(line.Share + 0.1, 2)).Message);
        row.AddChild(more);

        var remove = new Button { Text = "Retirer", FocusMode = FocusModeEnum.None };
        remove.Pressed += () => Report(_desk.RemoveSupplyLine(line.Id).Message);
        row.AddChild(remove);
        return row;
    }

    /// <summary>Le résultat d'une action ; la boîte se refait à l'image suivante (elle contient le bouton pressé).</summary>
    private void Report(string message)
    {
        _key = "";
        Message?.Invoke(message);
    }
}
