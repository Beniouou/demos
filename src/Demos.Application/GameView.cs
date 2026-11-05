using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Port d'entrée en lecture seule : ce que l'interface du joueur peut voir de la partie. Chaque valeur est une copie
/// prise au moment de l'appel (nombres, textes, records) : l'interface ne reçoit aucun objet du moteur, elle ne peut
/// donc rien y changer. Lire ne crée rien non plus (ni caisse, ni marché). Les actions du joueur passent par
/// <see cref="Simulation"/>.
/// </summary>
public sealed class GameView(Simulation simulation)
{
    private World World => simulation.World;

    public DateOnly Today => simulation.Clock.Today;
    public Season Season => simulation.Clock.Season;

    public double TreasuryBalance => World.Treasury.Balance;
    public double StateTaxRate => World.StateTaxRate;

    public double LegitimacyOf(Orientation pole) => World.Legitimacy[pole];

    public IReadOnlyList<CommuneView> Communes() => World.Communes.Select(DescribeCommune).ToList();

    private CommuneView DescribeCommune(Structure commune)
    {
        var jobs = World.Structures.Values
            .Where(s => s.Jobs > 0 && World.CommuneOf(s) == commune.Id)
            .ToList();
        var storage = World.StorageOf(commune.Id);

        return new CommuneView(
            commune.Id,
            commune.Name,
            World.Residents(commune.Id),
            World.HousingPlaces(commune.Id),
            jobs.Sum(s => World.Workers.GetValueOrDefault(s.Id)),
            jobs.Sum(s => s.Jobs),
            World.PollutionByCommune.GetValueOrDefault(commune.Id),
            World.IsGhostTown(commune.Id),
            World.CommuneFundBalanceOf(commune.Id),
            World.CommuneTaxRateOf(commune.Id),
            storage.Stored.Values.Sum(),
            World.StorageCapacity(commune.Id),
            new Dictionary<string, double>(World.StockOf(commune.Id)),
            new Dictionary<string, double>(World.StateHoldingsOf(commune.Id)),
            World.Classes.Where(c => c.CommuneId == commune.Id && c.Population > 0).Select(c => new SocialClassView(
                c.Id, c.Name, c.Population, c.Wealth, c.NeedsSatisfaction, c.HousingCoverage, c.Adhesion,
                c.EducationPoints, c.AdvancedEducationPoints)).ToList());
    }

    /// <summary>
    /// Les structures emboîtées, de l'État (la racine) aux communes, puis à leurs organismes et bâtiments, chacune
    /// avec les lois créées chez elle.
    /// </summary>
    public IReadOnlyList<StructureNode> StructureTree() =>
        World.Structures.Values.Where(s => s.IsNational).Select(DescribeNode).ToList();

    private StructureNode DescribeNode(Structure structure) => new(
        structure.Id,
        structure.Name,
        structure.Kind switch
        {
            StructureKind.PoliticalEntity => structure.IsNational ? StructureNodeKind.State : StructureNodeKind.Commune,
            StructureKind.Organization => StructureNodeKind.Organization,
            _ => StructureNodeKind.Building,
        },
        World.Laws.Where(l => l.StructureId == structure.Id).Select(DescribeLaw).ToList(),
        World.Structures.Values.Where(s => s.ParentId == structure.Id).Select(DescribeNode).ToList(),
        structure.Ownership);

    /// <summary>
    /// Les lois qui s'appliquent à une structure : une loi créée dans une structure s'applique à tout ce qu'elle
    /// contient. De la plus large (l'État) à la structure elle-même.
    /// </summary>
    public IReadOnlyList<LawView> LawsApplyingTo(string structureId)
    {
        var chain = new List<string>();
        for (var current = World.Structures.GetValueOrDefault(structureId); current is not null;
             current = current.ParentId is { } parentId ? World.Structures.GetValueOrDefault(parentId) : null)
            chain.Insert(0, current.Id);

        return chain.SelectMany(id => World.Laws.Where(l => l.StructureId == id)).Select(DescribeLaw).ToList();
    }

    private LawView DescribeLaw(Law law)
    {
        var book = World.LawBook;
        var bricks = law.Bricks.Select(setting => DescribeBrick(book, law, setting)).ToList();
        return new LawView(
            law.Id,
            law.Name,
            law.StructureId,
            World.Structures.TryGetValue(law.StructureId, out var structure) ? structure.Name : law.StructureId,
            bricks.SelectMany(b => b.Poles).Distinct().ToList(),
            law.Domain,
            World.LawCatalogue.DomainOf(law.Domain)?.Regalian == true && structure is { IsNational: false },
            bricks);
    }

    private static BrickView DescribeBrick(LawBook book, Law law, BrickSetting setting)
    {
        var movement = book.MovementOf(law, setting);
        return new BrickView(
            setting.Brick,
            movement?.Brick.Name ?? setting.Brick,
            setting.Subject,
            setting.Value,
            movement?.Reference ?? setting.Value,
            movement?.Intensity ?? 0,
            setting.Locked,
            movement is { Intensity: > 0 } m ? m.Poles.ToList() : []);
    }

    /// <summary>Les modèles de lois du catalogue, à proposer dans le menu des modèles.</summary>
    public IReadOnlyList<LawTemplate> LawTemplates() => World.LawCatalogue.Templates.ToList();

    /// <summary>Toutes les lois en vigueur, de tous les niveaux (le code civil).</summary>
    public IReadOnlyList<LawView> Laws() => World.Laws.Select(DescribeLaw).ToList();

    /// <summary>Les briques du catalogue : famille, bornes, valeur par défaut, poids et description de chaque curseur.</summary>
    public IReadOnlyList<BrickDefinition> LawBricks() => World.LawCatalogue.Bricks.ToList();

    /// <summary>Les domaines du droit, avec ceux qui sont régaliens.</summary>
    public IReadOnlyList<LawDomain> LawDomains() => World.LawCatalogue.Domains.ToList();

    /// <summary>Les brouillons de la commission des lois, avec leur coût s'ils étaient votés maintenant.</summary>
    public IReadOnlyList<LawDraftView> LawDrafts() => World.LawCommission.Drafts
        .Select(d => new LawDraftView(d.Id, d.Kind, DescribeLaw(d.Law), new Dictionary<Orientation, double>(simulation.CostOf(d.Id)), false))
        .ToList();

    /// <summary>Les changements votés, en attente du 1er du mois, avec ce qu'ils ont coûté.</summary>
    public IReadOnlyList<LawDraftView> VotedLawChanges() => World.LawCommission.Voted
        .Select(v => new LawDraftView(v.Draft.Id, v.Draft.Kind, DescribeLaw(v.Draft.Law), new Dictionary<Orientation, double>(v.Paid), true))
        .ToList();
}

public enum StructureNodeKind
{
    State,
    Commune,
    Organization,
    Building,
}

/// <summary>Une structure dans l'arbre de l'État, ses lois, ce qu'elle contient et son propriétaire (État, SCOP ou privé).</summary>
public sealed record StructureNode(
    string Id,
    string Name,
    StructureNodeKind Kind,
    IReadOnlyList<LawView> Laws,
    IReadOnlyList<StructureNode> Children,
    Ownership Ownership);

/// <summary>
/// Une loi vue par le joueur : où elle a été créée, ses pôles idéologiques (ceux de ses briques, selon le sens de leurs
/// curseurs), son domaine, la marque d'une loi régalienne votée sous l'État, et ses briques.
/// </summary>
public sealed record LawView(
    string Id,
    string Name,
    string StructureId,
    string StructureName,
    IReadOnlyList<Orientation> Orientations,
    string Domain = "",
    bool RegalianBelowState = false,
    IReadOnlyList<BrickView>? Bricks = null);

/// <summary>Une brique d'une loi : son sujet, son curseur, la valeur héritée des niveaux au-dessus, son écart et ses pôles.</summary>
public sealed record BrickView(
    string Id,
    string Name,
    LawSubject Subject,
    double Value,
    double Reference,
    double Intensity,
    bool Locked,
    IReadOnlyList<Orientation> Poles);

/// <summary>Un brouillon de la commission, ou un changement voté en attente du 1er du mois, et son coût par pôle.</summary>
public sealed record LawDraftView(
    string Id,
    LawChangeKind Kind,
    LawView Law,
    IReadOnlyDictionary<Orientation, double> Cost,
    bool Voted);

/// <summary>Une commune vue par le joueur, à un instant donné.</summary>
public sealed record CommuneView(
    string Id,
    string Name,
    double Residents,
    int HousingPlaces,
    double JobsFilled,
    int Jobs,
    double PollutionPerResident,
    bool IsGhostTown,
    double Fund,
    double TaxRate,
    double Stored,
    int StorageCapacity,
    IReadOnlyDictionary<string, double> Stock,
    IReadOnlyDictionary<string, double> StateStock,
    IReadOnlyList<SocialClassView> Classes);

/// <summary>Un groupe local (une classe dans sa commune) vu par le joueur, à un instant donné.</summary>
public sealed record SocialClassView(
    string Id,
    string Name,
    long Population,
    double Wealth,
    double NeedsSatisfaction,
    double HousingCoverage,
    double Adhesion,
    double EducationPoints,
    double AdvancedEducationPoints);
