using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Godot;

namespace Demos.Game;

/// <summary>
/// Choisir une structure en descendant la hiérarchie du jeu, en trois menus déroulants : entité politique (l'État ou
/// une commune) → organisme → bâtiment. Les bâtiments sont rangés sous des intertitres par catégorie (Extraction,
/// Production…), avec le carré de couleur de leur propriétaire. Avec « tous » permis (filtre du code civil), chaque
/// menu commence par « Toutes » ou « Tous » ; sinon (Production), on choisit toujours une entité et un bâtiment.
/// Les organismes n'existent pas encore dans le jeu : leur menu reste grisé tant qu'une entité n'en a pas.
/// </summary>
public sealed class StructurePicker
{
    /// <summary>Le choix « Sans organisme » : les bâtiments rattachés directement à l'entité politique.</summary>
    public const string WithoutOrganization = "";

    // Un intertitre du menu des bâtiments occupe une ligne sans être un choix.
    private const string Separator = "\0intertitre";

    private readonly bool _allowAll;
    private readonly OptionButton _entity;
    private readonly OptionButton _organization;
    private readonly OptionButton _building;
    private readonly List<string?> _entityIds = [];
    private readonly List<string?> _organizationIds = [];
    private readonly List<string?> _buildingIds = [];
    private IReadOnlyList<StructureNode> _roots = [];
    private string _shape = "";

    /// <summary>Un choix a changé (entité, organisme ou bâtiment).</summary>
    public event System.Action? Changed;

    public StructurePicker(VBoxContainer column, bool allowAll)
    {
        _allowAll = allowAll;
        _entity = Menu(column, "Entité");
        _entity.TooltipText = "Entité politique : l'État ou une commune";
        _organization = Menu(column, "Organisme");
        _building = Menu(column, "Bâtiment");

        _entity.ItemSelected += _ =>
        {
            FillOrganizations(null);
            FillBuildings(null);
            Changed?.Invoke();
        };
        _organization.ItemSelected += _ =>
        {
            FillBuildings(null);
            Changed?.Invoke();
        };
        _building.ItemSelected += _ =>
        {
            // Un intertitre ne se choisit pas : on garde le bâtiment juste en dessous.
            if (_building.Selected >= 0 && _buildingIds[_building.Selected] == Separator)
                _building.Select(System.Math.Min(_building.Selected + 1, _building.ItemCount - 1));
            Changed?.Invoke();
        };
    }

    /// <summary>L'entité politique choisie, ou null pour toutes.</summary>
    public string? EntityId => Pick(_entity, _entityIds);

    /// <summary>L'organisme choisi, <see cref="WithoutOrganization"/> pour les bâtiments sans organisme, ou null pour tous.</summary>
    public string? OrganizationId => Pick(_organization, _organizationIds);

    /// <summary>Le bâtiment choisi, ou null pour tous.</summary>
    public string? BuildingId => Pick(_building, _buildingIds) is { } id && id != Separator ? id : null;

    /// <summary>La structure la plus précise choisie (bâtiment, puis organisme, puis entité), ou null pour toutes.</summary>
    public string? MostPrecise => BuildingId ?? (OrganizationId is { Length: > 0 } organization ? organization : null) ?? EntityId;

    /// <summary>Met à jour les menus quand une commune, un organisme ou un bâtiment apparaît ; les choix sont gardés.</summary>
    public void Refresh(IReadOnlyList<StructureNode> roots)
    {
        var shape = Shape(roots);
        if (shape == _shape)
            return;

        _shape = shape;
        _roots = roots;
        var (entity, organization, building) = (EntityId, OrganizationId, BuildingId);
        FillEntities(entity);
        FillOrganizations(organization);
        FillBuildings(building);
    }

    /// <summary>Choisit un bâtiment par son identifiant, avec son entité et son organisme.</summary>
    public void SelectBuilding(string buildingId)
    {
        foreach (var entity in Entities())
        {
            var organization = entity.Children.FirstOrDefault(o => o.Kind == StructureNodeKind.Organization
                && o.Children.Any(b => b.Id == buildingId));
            if (organization is null && entity.Children.All(b => b.Id != buildingId))
                continue;

            FillEntities(entity.Id);
            FillOrganizations(organization?.Id);
            FillBuildings(buildingId);
            return;
        }
    }

    private void FillEntities(string? selected)
    {
        var entities = Entities();
        // Sans « toutes », on commence par la première entité qui a des bâtiments (l'État n'en a pas d'ordinaire).
        if (!_allowAll && selected is null)
            selected = entities.FirstOrDefault(e => e.Children.Any(c => c.Kind is StructureNodeKind.Building or StructureNodeKind.Organization))?.Id;
        Fill(_entity, _entityIds, selected,
            _allowAll ? new (string?, string)[] { (null, "Toutes les entités") } : [],
            entities.Select(e => ((string?)e.Id, e.Name)));
    }

    private void FillOrganizations(string? selected)
    {
        var organizations = SelectedEntity()?.Children.Where(c => c.Kind == StructureNodeKind.Organization).ToList() ?? [];
        Fill(_organization, _organizationIds, selected,
            organizations.Count == 0
                ? new (string?, string)[] { (null, "Tous les organismes") }
                : [(null, "Tous les organismes"), (WithoutOrganization, "Sans organisme")],
            organizations.Select(o => ((string?)o.Id, o.Name)));
        // En attendant les organismes, le menu reste grisé.
        _organization.Disabled = organizations.Count == 0;
    }

    private void FillBuildings(string? selected)
    {
        var buildings = Buildings();
        var items = new List<(string? Id, string Name, Texture2D? Icon)>();
        if (_allowAll || buildings.Count == 0)
            items.Add((null, buildings.Count == 0 && !(_allowAll && EntityId is null) ? "Aucun bâtiment" : "Tous les bâtiments", null));
        foreach (var category in buildings.GroupBy(b => b.Category ?? "Autres"))
        {
            items.Add((Separator, category.Key, null));
            items.AddRange(category.Select(b => ((string?)b.Id, b.Name, (Texture2D?)UiTheme.Square(UiTheme.OwnershipColor(b.Ownership), 12, 12))));
        }

        _buildingIds.Clear();
        _building.Clear();
        foreach (var (id, name, icon) in items)
        {
            _buildingIds.Add(id);
            if (id == Separator)
                _building.AddSeparator(name);
            else if (icon is not null)
                _building.AddIconItem(icon, name);
            else
                _building.AddItem(name);
        }

        // Le bâtiment gardé, sinon le premier choix qui n'est pas un intertitre.
        var index = _buildingIds.IndexOf(selected);
        if (index < 0 || selected == Separator)
            index = _buildingIds.FindIndex(id => id != Separator);
        if (index >= 0)
            _building.Select(index);
        _building.Disabled = _allowAll && EntityId is null;
    }

    /// <summary>L'État puis ses communes : les entités politiques.</summary>
    private List<StructureNode> Entities() =>
        _roots.SelectMany(root => root.Children.Where(c => c.Kind == StructureNodeKind.Commune).Prepend(root)).ToList();

    private StructureNode? SelectedEntity() => EntityId is { } id ? Entities().FirstOrDefault(e => e.Id == id) : null;

    /// <summary>Les bâtiments de l'entité choisie, filtrés par l'organisme choisi.</summary>
    private List<StructureNode> Buildings()
    {
        if (SelectedEntity() is not { } entity)
            return [];

        var direct = entity.Children.Where(c => c.Kind == StructureNodeKind.Building);
        var inOrganizations = entity.Children.Where(c => c.Kind == StructureNodeKind.Organization)
            .Where(o => OrganizationId is null || o.Id == OrganizationId)
            .SelectMany(o => o.Children.Where(c => c.Kind == StructureNodeKind.Building));
        return OrganizationId switch
        {
            null => direct.Concat(inOrganizations).ToList(),
            WithoutOrganization => direct.ToList(),
            _ => inOrganizations.ToList(),
        };
    }

    private static void Fill(OptionButton menu, List<string?> ids, string? selected,
        IEnumerable<(string? Id, string Name)> first, IEnumerable<(string? Id, string Name)> items)
    {
        ids.Clear();
        menu.Clear();
        foreach (var (id, name) in first.Concat(items))
        {
            ids.Add(id);
            menu.AddItem(name);
        }
        if (ids.Count > 0)
            menu.Select(System.Math.Max(0, ids.IndexOf(selected)));
    }

    private static string? Pick(OptionButton menu, List<string?> ids) =>
        menu.Selected >= 0 && menu.Selected < ids.Count ? ids[menu.Selected] : null;

    /// <summary>Ce qui oblige à refaire les menus : une structure en plus, renommée, ou qui change de propriétaire.</summary>
    private static string Shape(IEnumerable<StructureNode> nodes) =>
        string.Join(";", nodes.Select(n => $"{n.Id}:{n.Name}:{n.Ownership}({Shape(n.Children)})"));

    private static OptionButton Menu(VBoxContainer column, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(90, 0) });
        // Le menu ne prend pas la largeur de son plus long choix : sinon il élargirait sa boîte.
        var menu = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
            FitToLongestItem = false,
            ClipText = true,
        };
        row.AddChild(menu);
        column.AddChild(row);
        return menu;
    }
}
