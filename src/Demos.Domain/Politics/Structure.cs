using Demos.Domain.Economy;

namespace Demos.Domain.Politics;

public enum StructureKind
{
    PoliticalEntity,
    Organization,
    Building,
}

/// <summary>
/// Public : étatique ou public, la production va dans le stock de l'État et les salaires sont payés par le trésor.
/// Privé et coopératif (droit privé, autogestionnaire, scop) : la production est vendue sur le marché
/// et les salaires sont payés par la classe propriétaire, qui touche les ventes.
/// </summary>
public enum Ownership
{
    Public,
    Private,
    Cooperative,
}

/// <summary>Matière consommée par une recette : quantité par unité produite.</summary>
public readonly record struct RecipeInput(string ProductId, double Quantity);

/// <summary>
/// Recette d'une structure : ce qu'elle produit chaque jour par travailleur, et les matières qu'elle consomme pour
/// chaque unité produite (aucune pour une extraction : ferme, mine, carrière…).
/// </summary>
public readonly record struct Production(string ProductId, double QuantityPerWorker, IReadOnlyList<RecipeInput>? Inputs = null)
{
    public IReadOnlyList<RecipeInput> Materials => Inputs ?? [];

    /// <summary>Nom lisible de la recette, par exemple « vetements ← tissus ».</summary>
    public string Label => Materials.Count == 0 ? ProductId : $"{ProductId} ← {string.Join(" + ", Materials.Select(m => m.ProductId))}";
}

/// <summary>
/// Entité politique, organisme ou bâtiment. Les structures s'emboîtent :
/// l'État contient des communes, une commune des organismes et des bâtiments…
/// Une loi créée dans une structure s'applique à tout ce qu'elle contient.
/// </summary>
/// <param name="ParentId">Structure qui la contient ; null pour l'État, qui couvre toute la nation.</param>
/// <param name="OwnerClassId">Classe propriétaire d'une structure privée ou coopérative.</param>
/// <param name="Jobs">Nombre d'emplois de production ; les emplois d'encadrement et de direction en découlent.</param>
/// <param name="DailyWage">Salaire de base versé par emploi et par jour.</param>
/// <param name="HousingPlaces">Places de logement offertes dans la commune.</param>
/// <param name="DailyRent">Loyer d'une place par jour, versé au propriétaire.</param>
/// <param name="TypeId">Type de bâtiment du catalogue dont il est issu, s'il y en a un.</param>
/// <param name="Posts">
/// Nombre de postes de personnages. À ne pas confondre avec les emplois : un poste est occupé par un personnage
/// et apporte un bonus, mais la structure fonctionne sans personne au poste. Le recrutement reste à écrire.
/// </param>
/// <param name="SchoolPlaces">Places d'élèves offertes dans la commune ; chaque place occupée rapporte des points d'éducation.</param>
/// <param name="DailyTuition">Frais de scolarité d'une place par jour, versés au propriétaire (0 pour une école gratuite).</param>
/// <param name="SchoolLevel">Éducation produite : de base (école, lycée) ou avancée (université).</param>
/// <param name="PointsPerPupil">Points d'éducation rapportés par élève et par jour (un lycée en rapporte plus qu'une école).</param>
/// <param name="PollutionPerWorker">Pollution produite par travailleur de production et par jour.</param>
/// <param name="StorageCapacity">Quantité de produits que le bâtiment peut stocker pour sa commune.</param>
/// <param name="StorageKind">Ce que le stockage accepte : tout, l'alimentaire, le non alimentaire, ou un seul produit.</param>
/// <param name="StorageProductId">Le produit accepté par un stockage spécifique.</param>
/// <param name="StorageLimits">Quantité maximale de certains produits dans ce stockage ; un produit absent n'est pas limité.</param>
/// <param name="HeatingProductId">Combustible du chauffage (bois ou charbon) ; le bois par défaut.</param>
/// <param name="MainRank">
/// Rang productif : celui des emplois dont dépend la production (les ouvriers d'ordinaire, les professions intermédiaires
/// dans un bâtiment de services). <c>Jobs</c> compte ces emplois productifs.
/// </param>
/// <param name="StaffRatios">Emplois des autres rangs par emploi productif ; null pour les proportions par défaut.</param>
/// <param name="Wages">Salaire par jour de chaque rang ; null pour le salaire de base × multiplicateur.</param>
public sealed record Structure(
    string Id,
    string Name,
    StructureKind Kind,
    string? ParentId,
    Ownership Ownership = Ownership.Public,
    string? OwnerClassId = null,
    Production? Production = null,
    int Jobs = 0,
    double DailyWage = 0,
    int HousingPlaces = 0,
    double DailyRent = 0,
    string? TypeId = null,
    int Posts = 0,
    int SchoolPlaces = 0,
    double DailyTuition = 0,
    EducationLevel SchoolLevel = EducationLevel.Basic,
    double PointsPerPupil = 1,
    double PollutionPerWorker = 0,
    int StorageCapacity = 0,
    StorageKind StorageKind = StorageKind.General,
    string? StorageProductId = null,
    IReadOnlyDictionary<string, double>? StorageLimits = null,
    string? HeatingProductId = null,
    Population.Rank MainRank = Population.Rank.Workers,
    IReadOnlyDictionary<Population.Rank, double>? StaffRatios = null,
    IReadOnlyDictionary<Population.Rank, double>? Wages = null)
{
    public bool IsNational => ParentId is null;
}
