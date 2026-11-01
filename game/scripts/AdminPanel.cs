using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Godot;

namespace Demos.Game;

/// <summary>
/// Menu d'administration (outil de test, pas encore l'interface du joueur) : liste les bâtiments de la partie
/// et permet de régler leur propriétaire, leurs emplois, leur salaire, leur production, leurs places,
/// leur loyer, le prix de vente de leur produit, et la gestion des stockages : ce que chacun accepte et la
/// quantité maximale de chaque produit. On y règle aussi l'impôt d'État et l'impôt de la commune du bâtiment.
/// </summary>
public partial class AdminPanel : PanelContainer
{
    private static readonly Ownership[] Ownerships = [Ownership.Public, Ownership.Private, Ownership.Cooperative];
    private static readonly StorageKind[] StorageKinds = [StorageKind.General, StorageKind.Food, StorageKind.NonFood, StorageKind.Specific];

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
    private OptionButton _schoolLevel = null!;
    private SpinBox _pointsPerPupil = null!;
    private SpinBox _pollution = null!;
    private SpinBox _storage = null!;
    private OptionButton _storageKind = null!;
    private OptionButton _storageProduct = null!;
    private OptionButton _limitProduct = null!;
    private CheckBox _limited = null!;
    private SpinBox _limit = null!;
    private SpinBox _stateTax = null!;
    private SpinBox _communeTax = null!;

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
            _owner.AddItem($"{socialClass.Name} ({socialClass.CommuneId})");

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
            lines.Add($"Production : {perDay:N1} {production.ProductId} par jour, valeur {perDay * world.PriceOf(production.ProductId):N0}");
        }

        var wages = world.Classes.Sum(c => c.MembersWorkingIn(building.Id) * building.DailyWage * Demos.Domain.Economy.Staffing.WageMultiplier(c.Rank));
        if (wages > 0)
            lines.Add($"Salaires dus : {wages:N0} par jour");
        if (building.HousingPlaces > 0)
            lines.Add($"Loyers possibles : {building.HousingPlaces * building.DailyRent:N0} par jour");
        if (building.SchoolPlaces > 0)
            lines.Add(
                $"Enseignement {(building.SchoolLevel == Demos.Domain.Economy.EducationLevel.Basic ? "de base" : "avancé")} : " +
                $"jusqu'à {building.SchoolPlaces * building.PointsPerPupil:N0} points par jour, frais possibles {building.SchoolPlaces * building.DailyTuition:N0}");

        if (building.StorageCapacity > 0)
            lines.Add(DescribeStorage(building));

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
        _pollution = AddSpin(form, "Pollution par travailleur", 0, 100, 0.01);
        _storage = AddSpin(form, "Capacité de stockage", 0, 1_000_000, 100);
        _storageKind = AddOption(form, "Le stockage accepte", ["Tous les produits", "L'alimentaire", "Le non alimentaire", "Un seul produit"]);
        _storageProduct = AddOption(form, "Produit accepté (un seul)", ProductIds);
        _limitProduct = AddOption(form, "Limite pour le produit", ProductIds);
        _limited = new CheckBox { Text = "Limiter ce produit à" };
        _limit = new SpinBox { MinValue = 0, MaxValue = 1_000_000, Step = 100, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var limitRow = new HBoxContainer();
        limitRow.AddChild(_limited);
        limitRow.AddChild(_limit);
        form.AddChild(limitRow);
        _places = AddSpin(form, "Places de logement", 0, 100_000, 10);
        _rent = AddSpin(form, "Loyer par place et par jour", 0, 1_000, 0.05);
        _schoolPlaces = AddSpin(form, "Places d'élèves", 0, 100_000, 10);
        _tuition = AddSpin(form, "Frais de scolarité par jour", 0, 1_000, 0.05);
        _schoolLevel = AddOption(form, "Niveau d'éducation", ["De base (ouvriers)", "Avancée (prof. intermédiaires)"]);
        _pointsPerPupil = AddSpin(form, "Points par élève et par jour", 0, 100, 0.1);
        _price = AddSpin(form, "Prix du produit au marché", 0, 10_000, 0.1);
        _posts = AddSpin(form, "Postes de personnages", 0, 20, 1);
        _stateTax = AddSpin(form, "Impôt d'État (% par jour)", 0, 100, 0.01);
        _communeTax = AddSpin(form, "Impôt de la commune (% par jour)", 0, 100, 0.01);

        _ownership.ItemSelected += _ => _owner.Disabled = _ownership.Selected == 0;
        _storageKind.ItemSelected += _ => _storageProduct.Disabled = StorageKinds[_storageKind.Selected] != StorageKind.Specific;
        _limitProduct.ItemSelected += _ => ShowLimit(SelectedBuilding());
        _limited.Toggled += on => _limit.Editable = on;

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
        _pollution.Value = building.PollutionPerWorker;
        _storage.Value = building.StorageCapacity;
        _storageKind.Select(System.Array.IndexOf(StorageKinds, building.StorageKind));
        _storageProduct.Select(System.Math.Max(0, System.Array.IndexOf(ProductIds, building.StorageProductId)));
        _storageProduct.Disabled = building.StorageKind != StorageKind.Specific;
        ShowLimit(building);
        _schoolPlaces.Value = building.SchoolPlaces;
        _tuition.Value = building.DailyTuition;
        _schoolLevel.Select((int)building.SchoolLevel);
        _pointsPerPupil.Value = building.PointsPerPupil;
        _posts.Value = building.Posts;
        _stateTax.Value = _simulation.World.StateTaxRate * 100;
        _communeTax.Value = _simulation.World.CommuneOf(building) is { } taxedCommune ? _simulation.World.CommuneTaxRateOf(taxedCommune) * 100 : 0;

        var production = building.Production;
        _perWorker.Editable = _price.Editable = production is not null;
        _perWorker.Value = production?.QuantityPerWorker ?? 0;
        _price.Value = production is { } p ? _simulation.World.PriceOf(p.ProductId) : 0;

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
            PollutionPerWorker = _pollution.Value,
            StorageCapacity = (int)_storage.Value,
            StorageKind = StorageKinds[_storageKind.Selected],
            StorageProductId = StorageKinds[_storageKind.Selected] == StorageKind.Specific ? ProductIds[_storageProduct.Selected] : null,
            StorageLimits = UpdatedLimits(building),
            SchoolPlaces = (int)_schoolPlaces.Value,
            DailyTuition = _tuition.Value,
            SchoolLevel = (Demos.Domain.Economy.EducationLevel)_schoolLevel.Selected,
            PointsPerPupil = _pointsPerPupil.Value,
            Posts = (int)_posts.Value,
        };

        _simulation.UpdateBuilding(updated);
        _simulation.World.StateTaxRate = _stateTax.Value / 100;
        if (_simulation.World.CommuneOf(updated) is { } communeId)
            _simulation.World.SetCommuneTaxRate(communeId, _communeTax.Value / 100);
        if (updated.Production is { } changed)
            _simulation.World.SetPrice(changed.ProductId, _price.Value);

        UpdateInfo();
    }

    private string[] ProductIds => _simulation.World.Products.Select(p => p.Id).ToArray();

    private void ShowLimit(Structure? building)
    {
        var productId = ProductIds[System.Math.Max(0, _limitProduct.Selected)];
        var limit = building?.StorageLimits?.GetValueOrDefault(productId, -1) ?? -1;
        _limited.ButtonPressed = limit >= 0;
        _limit.Editable = limit >= 0;
        _limit.Value = System.Math.Max(0, limit);
    }

    /// <summary>Limites du bâtiment, avec celle du produit choisi mise à jour (ou retirée : sans limite).</summary>
    private IReadOnlyDictionary<string, double>? UpdatedLimits(Structure building)
    {
        var limits = new Dictionary<string, double>(building.StorageLimits ?? new Dictionary<string, double>());
        var productId = ProductIds[System.Math.Max(0, _limitProduct.Selected)];
        if (_limited.ButtonPressed)
            limits[productId] = _limit.Value;
        else
            limits.Remove(productId);
        return limits.Count > 0 ? limits : null;
    }

    private string DescribeStorage(Structure building)
    {
        var world = _simulation.World;
        var accepts = building.StorageKind switch
        {
            StorageKind.Food => "l'alimentaire",
            StorageKind.NonFood => "le non alimentaire",
            StorageKind.Specific => building.StorageProductId ?? "aucun produit choisi",
            _ => "tous les produits",
        };
        var contents = world.CommuneOf(building) is { } communeId ? world.StorageOf(communeId).In(building.Id) : new Dictionary<string, double>();
        var held = contents.Values.Sum();
        var detail = contents.Where(c => c.Value >= 0.5).Select(c => $"{c.Key} {c.Value:N0}");
        var limits = building.StorageLimits?.Select(l => $"{l.Key} {l.Value:N0}") ?? [];
        return $"Stockage ({accepts}) : {held:N0} / {building.StorageCapacity:N0}  [{string.Join(", ", detail)}]" +
            (limits.Any() ? $"\nLimites : {string.Join(", ", limits)} (les autres produits sans limite)" : "");
    }

    private string OwnerName(string? classId) =>
        classId is not null && _simulation.World.ClassesById.TryGetValue(classId, out var owner)
            ? $"{owner.Name} ({owner.Wealth:N0})"
            : "personne";
}
