using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Politics;
using Godot;

namespace Demos.Game;

/// <summary>
/// Les lignes d'approvisionnement dans la fiche d'un bâtiment de la Chaîne de production : celles qui le livrent
/// (avec la part envoyée, à changer par pas de 10 %, et un bouton pour la retirer), celles qu'il livre, et l'ajout d'un
/// fournisseur parmi les bâtiments publics, de toutes les communes, qui produisent une de ses matières.
/// </summary>
public partial class ProductionChainPage
{
    private const string SupplyHint =
        "Une ligne relie deux bâtiments publics : chaque jour, une part de la production du fournisseur part d'abord au "
        + "client, avant d'être mise en vente. Le client ne reçoit que ce qu'il lui faut pour une journée à plein régime, "
        + "moins sa réserve ; le reste reste en vente. Il paie au prix du marché, sauf dans sa propre commune. Entre "
        + "communes, une part se perd en route (moins avec un chemin ou une route). Les lignes les plus anciennes passent d'abord.";

    private VBoxContainer _supply = null!;
    private string _supplyKey = "";

    /// <summary>La boîte des lignes, sous la recette ; remplie par <see cref="RefreshSupply"/>.</summary>
    private Control BuildSupplyPanel()
    {
        _supply = new VBoxContainer();
        _supply.AddThemeConstantOverride("separation", 4);
        return _supply;
    }

    /// <summary>Refait la boîte des lignes quand le bâtiment choisi ou ses lignes changent (une fois par jour au plus).</summary>
    private void RefreshSupply(ChainBuildingView? building)
    {
        var lines = building is null ? [] : _view.SupplyLines().Where(l => l.ClientId == building.Id || l.SupplierId == building.Id).ToList();
        var candidates = building is { Ownership: Ownership.Public } ? _view.SupplierCandidates(building.Id) : [];
        var key = building is null ? "" : $"{building.Id}|{_view.Today}|{string.Join(",", lines.Select(l => $"{l.Id}:{l.Share}"))}|{candidates.Count}";
        if (key == _supplyKey)
            return;
        _supplyKey = key;

        foreach (var child in _supply.GetChildren())
            child.QueueFree();
        if (building is null)
            return;

        var title = new HintText { BbcodeEnabled = true, FitContent = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeColorOverride("default_color", UiTheme.Text);
        title.Text = SheetText.Section("Lignes d'approvisionnement", SupplyHint);
        _supply.AddChild(title);

        if (building.Ownership != Ownership.Public)
        {
            _supply.AddChild(UiTheme.Caption("Les lignes ne relient que des bâtiments publics : ce bâtiment achète ses matières sur le marché."));
            return;
        }

        var incoming = lines.Where(l => l.ClientId == building.Id).ToList();
        var outgoing = lines.Where(l => l.SupplierId == building.Id).ToList();
        if (incoming.Count == 0 && outgoing.Count == 0)
            _supply.AddChild(UiTheme.Caption("Aucune ligne : il achète ses matières sur le marché de sa commune."));
        foreach (var line in incoming)
            _supply.AddChild(LineRow(line, $"← {line.SupplierName} ({line.SupplierCommune})"));
        foreach (var line in outgoing)
            _supply.AddChild(LineRow(line, $"→ {line.ClientName} ({line.ClientCommune})"));

        if (building.Inputs.Count == 0)
            return;
        var add = new HBoxContainer();
        add.AddThemeConstantOverride("separation", 8);
        var choice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, FitToLongestItem = false, ClipText = true };
        foreach (var candidate in candidates)
            choice.AddItem($"{candidate.Name} ({candidate.CommuneName}) · {candidate.ProductName}"
                + (candidate.Loss > 0 ? $" · perte {candidate.Loss:P0}" : ""));
        var create = new Button { Text = "Ajouter ce fournisseur", FocusMode = FocusModeEnum.None, Disabled = candidates.Count == 0 };
        create.Pressed += () =>
        {
            if (choice.Selected < 0 || choice.Selected >= candidates.Count)
                return;
            Report(_desk.AddSupplyLine(candidates[choice.Selected].Id, building.Id).Message);
        };
        if (candidates.Count == 0)
            choice.AddItem(incoming.Count > 0 ? "Aucun autre bâtiment public ne produit ses matières" : "Aucun bâtiment public ne produit ses matières");
        add.AddChild(choice);
        add.AddChild(create);
        _supply.AddChild(add);
    }

    /// <summary>Une ligne : de qui ou vers qui, le produit, la part (− / +), ce qui est arrivé hier, la perte, Retirer.</summary>
    private Control LineRow(SupplyLineView line, string who)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var text = $"{who} · {line.ProductName} · arrivé hier {line.DeliveredLastDay:N0}"
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

    /// <summary>
    /// Le résultat d'une action, sous la fiche ; la boîte et le schéma se refont à l'image suivante. Différé : l'action
    /// vient d'un bouton de la boîte qui va être refaite.
    /// </summary>
    private void Report(string message)
    {
        _message.Text = message;
        _message.Visible = true;
        _supplyKey = "";
        _chainKey = "";
    }

    /// <summary>Les lignes entre deux bâtiments de la commune montrée, pour les flèches du schéma.</summary>
    private IReadOnlyList<(string From, string To)> LinksIn(IReadOnlyList<ChainBuildingView> chain)
    {
        var ids = chain.Select(b => b.Id).ToHashSet();
        return _view.SupplyLines().Where(l => ids.Contains(l.SupplierId) && ids.Contains(l.ClientId)).Select(l => (l.SupplierId, l.ClientId)).ToList();
    }
}
