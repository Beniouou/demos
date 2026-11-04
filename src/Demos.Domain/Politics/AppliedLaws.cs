using Demos.Domain.Population;

namespace Demos.Domain.Politics;

/// <summary>
/// Ce que les lois en vigueur imposent à un bâtiment, recalculé chaque jour à partir des lois qui le couvrent (voir
/// <see cref="LawBook.RulesFor"/>). Les calculs de l'économie le lisent sans connaître les lois.
/// </summary>
/// <param name="MinimumWages">Salaire minimum par jour de chaque rang, quand une loi en fixe un.</param>
/// <param name="WageFactors">Niveau des salaires de chaque rang (1,2 = +20 %), quand une loi le change.</param>
/// <param name="ProductionFactor">Production du bâtiment (0,8 = −20 %).</param>
/// <param name="PollutionFactor">Pollution du bâtiment.</param>
/// <param name="LayoffFactor">Part de ses emplois qu'il peut licencier chaque mois, par rapport à la limite de son statut.</param>
/// <param name="MaximumRent">Loyer maximum d'une place par jour, quand une loi en fixe un.</param>
/// <param name="PaidInKind">Vrai si le travail peut être payé avec les produits du bâtiment.</param>
/// <param name="CompanyTaxFactor">Impôt (TVA) de l'entreprise, par rapport au taux de base (0 = exonérée).</param>
public sealed record AppliedLaws(
    IReadOnlyDictionary<Rank, double>? MinimumWages = null,
    IReadOnlyDictionary<Rank, double>? WageFactors = null,
    double ProductionFactor = 1,
    double PollutionFactor = 1,
    double LayoffFactor = 1,
    double? MaximumRent = null,
    bool PaidInKind = false,
    double CompanyTaxFactor = 1)
{
    public static readonly AppliedLaws None = new();

    /// <summary>Un salaire une fois les lois appliquées : son niveau, puis le salaire minimum s'il est plus haut.</summary>
    public double WageFor(Rank rank, double wage)
    {
        wage *= WageFactors?.GetValueOrDefault(rank, 1) ?? 1;
        return MinimumWages is not null && MinimumWages.TryGetValue(rank, out var minimum) ? Math.Max(wage, minimum) : wage;
    }

    /// <summary>Un loyer une fois le loyer maximum appliqué.</summary>
    public double RentFor(double rent) => MaximumRent is { } maximum ? Math.Min(rent, maximum) : rent;
}
