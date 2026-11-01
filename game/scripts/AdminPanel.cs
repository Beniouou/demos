using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Politics;
using Godot;

namespace Demos.Game;

/// <summary>
/// Menu d'administration (outil de test, pas encore l'interface du joueur) : liste les bâtiments de la partie
/// et permet de régler leur propriétaire, leurs emplois, leur salaire, leur production, leurs places,
/// leur loyer et le prix de vente de leur produit.
/// </summary>
public partial class AdminPanel : PanelContainer
{
    private static readonly Ownership[] Ownerships = [Ownership.Public, Ownership.Private, Ownership.Cooperative];

    private Simulation _simulation = null!;
    private readonly List<string> _buildingIds = [];
    private ItemList _list = null!;
    private Label _title = null!;
    private Label _info = null!;
    private OptionButton _ownership = null!;
    private OptionButton _owner = null!;
    private SpinBox _jobs = null!;
    private SpinBox _wage = null!;
    private SpinBox _perWorker = null!;
    private SpinBox _places = null!;
    private SpinBox _rent = null!;
    private SpinBox _price = null!;
    private SpinBox _posts = null!;
    private SpinBox _schoolPlaces = null!;
    private SpinBox _tuition = null!;

    public void Init(Simulation simulation)
    {
        _simulation = simulation;
        BuildLayout();
    }

    /// <summary>Recharge la liste des bâtiments (de nouveaux ont pu être construits).</summary>
    public void Refresh()
    {
        // Le parent n'est pas un Control : on se place nous-mêmes contre le bord droit de la fenêtre.
        var viewport = GetViewportRect().Size;
        Size = new Vector2(CustomMinimumSize.X, viewport.Y);
        Position = new Vector2(viewport.X - Size.X, 0);

        // De nouvelles classes ont pu naître : la liste des propriétaires possibles est reconstruite.
        _owner.Clear();
        foreach (var socialClass in _simulation.World.Classes)
            _owner.AddItem(socialClass.Name);

        var selected = SelectedBuilding()?.Id;
        _buildingIds.Clear();
        _list.Clear();

        foreach (var building in _simulation.World.Structures.Values.Where(s => s.Kind == StructureKind.Building))
        {
            _buildingIds.Add(building.Id);
            _list.AddItem(building.Name);
        }

        var index = selected is null ? 0 : _buildingIds.IndexOf(selected);
        if (_buildingIds.Count > 0)
        {
            _list.Select(System.Math.Max(0, index));
            ShowBuilding(System.Math.Max(0, index));
        }
    }

    /// <summary>Met à jour les informations en direct (travailleurs, production, coûts) du bâtiment choisi.</summary>
    public void UpdateInfo()
    {
        if (SelectedBuilding() is not { } building)
            return;

        var world = _simulation.World;
        var workers = world.Workers.GetValueOrDefault(building.Id);
        var staff = world.Classes.Sum(c => c.MembersWorkingIn(building.Id));
        var lines = new List<string>
        {
            $"Type : {(building.TypeId is { } typeId && world.BuildingTypes.TryGetValue(typeId, out var type) ? $"{type.Name} ({type.Category}, niveau {type.Level})" : "—")}",
            $"Personnel : {staff:N0} dont {workers:N0} en production",
        };

        if (building.Production is { } production)
        {
            var perDay = production.QuantityPerWorker * workers;
            lines.Add($"Production : {perDay:N1} {production.ProductId} par jour, valeur {perDay * world.Market.PriceOf(production.ProductId):N0}");
        }

        var wages = world.Classes.Sum(c => c.MembersWorkingIn(building.Id) * building.DailyWage * Demos.Domain.Economy.Staffing.WageMultiplier(c.Rank));
        if (wages > 0)
            lines.Add($"Salaires dus : {wages:N0} par jour");
        if (building.HousingPlaces > 0)
            lines.Add($"Loyers possibles : {building.HousingPlaces * building.DailyRent:N0} par jour");
        if (building.SchoolPlaces > 0)
            lines.Add($"École : jusqu'à {building.SchoolPlaces:N0} points d'éducation par jour, frais possibles {building.SchoolPlaces * building.DailyTuition:N0}");

        lines.Add($"Postes de personnages : 0 occupé sur {building.Posts} (recrutement : menu à venir)");

        lines.Add(building.Ownership == Ownership.Public
            ? $"Payé par le trésor ({world.Treasury.Balance:N0})"
            : $"Payé par {OwnerName(building.OwnerClassId)}");

        _info.Text = string.Join("\n", lines);
    }

    private void BuildLayout()
    {
        CustomMinimumSize = new Vector2(620, 0);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.13f, 0.13f, 0.16f) });

        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 12);
        AddChild(margin);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 12);
        margin.AddChild(columns);

        _list = new ItemList { CustomMinimumSize = new Vector2(230, 0), SizeFlagsVertical = SizeFlags.ExpandFill };
        _list.ItemSelected += index => ShowBuilding((int)index);
        columns.AddChild(_list);

        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        columns.AddChild(form);

        form.AddChild(new Label { Text = "ADMINISTRATION (touche A)" });
        _title = new Label();
        form.AddChild(_title);

        _ownership = AddOption(form, "Propriétaire", ["État (public)", "Privé", "Coopérative"]);
        _owner = AddOption(form, "Classe propriétaire", _simulation.World.Classes.Select(c => c.Name));
        _jobs = AddSpin(form, "Emplois de production", 0, 100_000, 10);
        _wage = AddSpin(form, "Salaire de base par jour", 0, 1_000, 0.1);
        _perWorker = AddSpin(form, "Production par travailleur", 0, 100, 0.01);
        _places = AddSpin(form, "Places de logement", 0, 100_000, 10);
        _rent = AddSpin(form, "Loyer par place et par jour", 0, 1_000, 0.05);
        _schoolPlaces = AddSpin(form, "Places d'élèves", 0, 100_000, 10);
        _tuition = AddSpin(form, "Frais de scolarité par jour", 0, 1_000, 0.05);
        _price = AddSpin(form, "Prix du produit au marché", 0, 10_000, 0.1);
        _posts = AddSpin(form, "Postes de personnages", 0, 20, 1);

        _ownership.ItemSelected += _ => _owner.Disabled = _ownership.Selected == 0;

        var apply = new Button { Text = "Appliquer" };
        apply.Pressed += Apply;
        form.AddChild(apply);

        _info = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        form.AddChild(_info);
    }

    private static OptionButton AddOption(VBoxContainer form, string label, IEnumerable<string> items)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(200, 0) });
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var item in items)
            option.AddItem(item);
        row.AddChild(option);
        form.AddChild(row);
        return option;
    }

    private static SpinBox AddSpin(VBoxContainer form, string label, double min, double max, double step)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(200, 0) });
        var spin = new SpinBox { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(spin);
        form.AddChild(row);
        return spin;
    }

    private Structure? SelectedBuilding()
    {
        var selected = _list.GetSelectedItems();
        return selected.Length > 0 && selected[0] < _buildingIds.Count
            ? _simulation.World.Structures.GetValueOrDefault(_buildingIds[selected[0]])
            : null;
    }

    private void ShowBuilding(int index)
    {
        var building = _simulation.World.Structures[_buildingIds[index]];
        var classes = _simulation.World.Classes;

        _title.Text = building.Name;
        _ownership.Select(System.Array.IndexOf(Ownerships, building.Ownership));
        var ownerIndex = classes.ToList().FindIndex(c => c.Id == building.OwnerClassId);
        _owner.Select(ownerIndex < 0 ? 0 : ownerIndex);
        _owner.Disabled = building.Ownership == Ownership.Public;

        _jobs.Value = building.Jobs;
        _wage.Value = building.DailyWage;
        _places.Value = building.HousingPlaces;
        _rent.Value = building.DailyRent;
        _schoolPlaces.Value = building.SchoolPlaces;
        _tuition.Value = building.DailyTuition;
        _posts.Value = building.Posts;

        var production = building.Production;
        _perWorker.Editable = _price.Editable = production is not null;
        _perWorker.Value = production?.QuantityPerWorker ?? 0;
        _price.Value = production is { } p ? _simulation.World.Market.PriceOf(p.ProductId) : 0;

        UpdateInfo();
    }

    private void Apply()
    {
        if (SelectedBuilding() is not { } building)
            return;

        var ownership = Ownerships[_ownership.Selected];
        var updated = building with
        {
            Ownership = ownership,
            OwnerClassId = ownership == Ownership.Public ? null : _simulation.World.Classes[_owner.Selected].Id,
            Jobs = (int)_jobs.Value,
            DailyWage = _wage.Value,
            Production = building.Production is { } production ? production with { QuantityPerWorker = _perWorker.Value } : null,
            HousingPlaces = (int)_places.Value,
            DailyRent = _rent.Value,
            SchoolPlaces = (int)_schoolPlaces.Value,
            DailyTuition = _tuition.Value,
            Posts = (int)_posts.Value,
        };

        _simulation.UpdateBuilding(updated);
        if (updated.Production is { } changed)
            _simulation.World.Market.SetPrice(changed.ProductId, _price.Value);

        UpdateInfo();
    }

    private string OwnerName(string? classId) =>
        classId is not null && _simulation.World.ClassesById.TryGetValue(classId, out var owner)
            ? $"{owner.Name} ({owner.Wealth:N0})"
            : "personne";
}
