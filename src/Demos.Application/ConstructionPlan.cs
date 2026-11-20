namespace Demos.Application;

/// <summary>
/// D'où viennent les matériaux d'un chantier (décidé le 20 novembre 2026) : la réserve de chantier et le stock de l'État
/// de sa commune, un achat à son marché, puis, s'il en manque, le stock de l'État d'une autre commune (envoyé, avec la
/// perte en route et la durée du trajet) et un achat à son marché. <see cref="Missing"/> &gt; 0 : le chantier ne peut pas
/// démarrer.
/// </summary>
public sealed record ConstructionPlan(
    string ProductId,
    double Quantity,
    double FromReserve,
    double FromState,
    double BoughtHere,
    string? OtherCommuneId,
    double FromOtherState,
    double BoughtThere,
    double LostOnTheWay,
    int TravelDays,
    double Money,
    double Missing,
    bool EnoughMoney)
{
    /// <summary>Ce qui part de l'autre commune (avant la perte en route).</summary>
    public double SentFromOther => FromOtherState + BoughtThere;

    public bool Feasible => Missing <= 1e-6 && EnoughMoney;
}

/// <summary>Le résultat d'un lancement de chantier : lancé ou non, le plan des matériaux, et la raison d'un refus.</summary>
public sealed record ConstructionOutcome(bool Started, ConstructionPlan? Plan, string? Problem);
