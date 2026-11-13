using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Map;
using Demos.Domain.Politics;

namespace Demos.Game;

/// <summary>
/// Ce que coûte un camp de base (fonder une commune), vu depuis la commune qui le paie : le matériau, ce qui peut le
/// payer, la légitimité, la dotation et le trésor.
/// </summary>
public sealed record FoundingOffer(
    string MaterialName,
    double MaterialQuantity,
    MaterialAvailability Availability,
    double LegitimacyCost,
    double Grant);

/// <summary>
/// Le bureau du joueur, fondation d'une commune : un camp de base, payé par la commune choisie, posé sur la carte là où
/// la règle le permet (ni l'eau, ni une falaise, ni un glacier, ni trop près d'une autre commune).
/// </summary>
public sealed partial class PlayerDesk
{
    /// <summary>Le coût d'un camp de base, et ce que la commune qui paie a pour le régler.</summary>
    public FoundingOffer? FoundingOfferFrom(string payingCommuneId)
    {
        var camp = World.BuildingTypes.GetValueOrDefault(Simulation.BaseCampTypeId);
        if (camp?.Construction is not { } cost)
            return null;
        return new FoundingOffer(ProductName(cost.ProductId), cost.Quantity, AvailabilityOf(payingCommuneId, cost.ProductId),
            Balance.Current.BaseCampLegitimacyCost, Simulation.FoundingGrantOf(camp));
    }

    /// <summary>Ce qui empêche de fonder une commune à cette position, ou null si c'est permis.</summary>
    public string? CommuneSiteProblem(Point2D position) => World.CommuneSiteProblem(position);

    /// <summary>Le nom proposé pour la prochaine commune.</summary>
    public string NextCommuneName() => $"Commune {World.Communes.Count() + 1}";

    /// <summary>
    /// Fonde une commune à cette position : son camp de base est payé par la commune choisie (réserve de chantier, stock
    /// de l'État, puis achat avec le trésor), la légitimité dans le pôle choisi (sa charte), la dotation par le trésor.
    /// </summary>
    public DeskAnswer FoundCommune(string name, string payingCommuneId, Orientation pole, Point2D position)
    {
        var legitimacy = Balance.Current.BaseCampLegitimacyCost;
        if (string.IsNullOrWhiteSpace(name))
            return new("Donnez un nom à la commune.");
        if (World.CommuneSiteProblem(position) is { } problem)
            return new($"Impossible de fonder ici : {problem}.");
        if (World.Legitimacy[pole] < legitimacy)
            return new($"Pas assez de légitimité {pole.Code} (il en faut {legitimacy:0.#}).");
        if (FoundingOfferFrom(payingCommuneId) is not { } offer)
            return new("Le camp de base ne se construit pas.");
        if (World.Treasury.Balance < offer.Grant)
            return new($"Le trésor n'a pas de quoi verser la dotation ({offer.Grant:N0}).");

        if (Simulation.TryFoundCommune(name.Trim(), payingCommuneId, PaymentMode.StockFirst, pole, legitimacy, position) is { } commune)
            return new($"{commune.Name} fondée, charte {WorldText.PoleName(pole)} ({pole.Code}), caisse de {World.CommuneFundOf(commune.Id).Balance:N0} ; "
                + "son camp de base est en chantier.", commune.Id);

        var available = offer.Availability;
        return new($"Fondation impossible : il faut {offer.MaterialQuantity:N0} {offer.MaterialName}. Il y a {available.Reserve:N0} en réserve "
            + $"de chantier, {available.StateStock:N0} dans le stock de l'État et {available.OnSale:N0} en vente, à acheter avec le trésor.");
    }
}
