using Demos.Application;
using Demos.Domain.Economy;
using Godot;

namespace Demos.Game;

/// <summary>
/// Les réglages d'achat et de vente d'un bâtiment public, dans Communes → Unités de production (décidés le 19
/// novembre 2026) : n'acheter que par ses lignes, ne pas acheter aux entreprises privées ou aux SCOP (grisé si une loi
/// l'interdit), garder des jours de production hors vente, ne pas envoyer vers d'autres communes. Caché pour un
/// bâtiment privé ou une SCOP. Le résultat d'une action remonte par <see cref="Message"/>.
/// </summary>
public partial class PurchaseSalePanel : VBoxContainer
{
    private const string LawHint = "Une loi « Choix des fournisseurs publics » l'interdit ici : ce refus ne s'applique pas.";

    private readonly GameView _view = null!;
    private readonly PlayerDesk _desk = null!;
    private readonly CheckBox _onlyLines = null!;
    private readonly CheckBox _refusePrivate = null!;
    private readonly CheckBox _refuseCooperative = null!;
    private readonly SpinBox _reserve = null!;
    private readonly CheckBox _noExport = null!;
    private string? _buildingId;
    private string _key = "";

    /// <summary>Le résultat d'une action.</summary>
    public event System.Action<string>? Message;

    public PurchaseSalePanel(GameView view, PlayerDesk desk)
    {
        _view = view;
        _desk = desk;
        AddThemeConstantOverride("separation", 4);

        var title = new HintText { BbcodeEnabled = true, FitContent = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeColorOverride("default_color", UiTheme.Text);
        title.Text = SheetText.Section("Achats et ventes (bâtiment public)",
            "Le privé et les SCOP décident seuls. Un bâtiment public prend d'abord ce que ses lignes lui livrent, dans l'ordre de leur priorité, puis le stock de l'État de sa commune, puis achète au marché aux vendeurs permis.");
        AddChild(title);

        _onlyLines = Check("N'acheter que par ses lignes d'approvisionnement",
            "Il n'achète plus rien au marché de sa commune ; il se sert toujours de sa réserve et du stock de l'État de sa commune.",
            on => s => s with { OnlyFromLines = on });
        _refusePrivate = Check("Ne pas acheter aux entreprises privées", "Au marché, il n'achète pas aux entreprises privées.",
            on => s => s with { RefusePrivate = on });
        _refuseCooperative = Check("Ne pas acheter aux SCOP", "Au marché, il n'achète pas aux SCOP.",
            on => s => s with { RefuseCooperative = on });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label
        {
            Text = "Réserve hors vente",
            TooltipText = "Jours de sa production gardés hors de la vente : les habitants, les autres bâtiments et les échanges ne les prennent pas ; ses lignes, les chantiers et les bâtiments de l'État de la commune, si.",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        _reserve = new SpinBox { MinValue = 0, MaxValue = SupplySettings.MaxReserveDays, Step = 1, Suffix = "jours", CustomMinimumSize = new Vector2(120, 0) };
        _reserve.ValueChanged += value => Apply(s => s with { ReserveDays = (int)value });
        row.AddChild(_reserve);
        AddChild(row);

        _noExport = Check("Ne pas envoyer vers d'autres communes",
            "Les échanges entre communes ne prennent pas ce produit de l'État dans sa commune (le réglage vaut pour tout ce produit de l'État dans la commune).",
            on => s => s with { NoExport = on });
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public PurchaseSalePanel() { }

    private CheckBox Check(string text, string tooltip, System.Func<bool, System.Func<SupplySettings, SupplySettings>> change)
    {
        var check = new CheckBox { Text = text, TooltipText = tooltip, FocusMode = FocusModeEnum.None };
        check.Toggled += on => Apply(change(on));
        AddChild(check);
        return check;
    }

    private void Apply(System.Func<SupplySettings, SupplySettings> change)
    {
        if (_buildingId is null)
            return;
        Message?.Invoke(_desk.ChangeSupplySettings(_buildingId, change).Message);
        _key = "";
    }

    /// <summary>Montre les réglages du bâtiment ; ne remet les cases que quand le bâtiment ou ses réglages changent.</summary>
    public void Refresh(string? buildingId)
    {
        var view = buildingId is null ? null : _view.SupplySettingsOf(buildingId);
        Visible = view is not null;
        _buildingId = view is null ? null : buildingId;
        var key = view is null ? "" : $"{buildingId}|{view}";
        if (view is null || key == _key)
            return;
        _key = key;

        var settings = view.Settings;
        _onlyLines.SetPressedNoSignal(settings.OnlyFromLines);
        _onlyLines.Disabled = !view.HasRecipe;
        SetRefusal(_refusePrivate, settings.RefusePrivate, view.MayRefusePrivate, "Au marché, il n'achète pas aux entreprises privées.");
        SetRefusal(_refuseCooperative, settings.RefuseCooperative, view.MayRefuseCooperative, "Au marché, il n'achète pas aux SCOP.");
        _reserve.SetValueNoSignal(settings.ReserveDays);
        _reserve.Editable = view.HasRecipe;
        _noExport.SetPressedNoSignal(settings.NoExport);
        _noExport.Disabled = !view.HasRecipe;
    }

    /// <summary>Une case de refus : grisée si une loi l'interdit et qu'elle n'est pas cochée ; la loi dans la bulle.</summary>
    private static void SetRefusal(CheckBox check, bool on, bool allowed, string tooltip)
    {
        check.SetPressedNoSignal(on);
        check.Disabled = !allowed && !on;
        check.TooltipText = allowed ? tooltip : LawHint;
        check.Modulate = allowed ? Colors.White : new Color(1, 1, 1, 0.6f);
    }
}
