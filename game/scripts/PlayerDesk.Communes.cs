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
/// <param name="LegitimacyCost">Légitimité PO à payer (fonder dégrade la terre), après le défrichement voté.</param>
/// <param name="LegitimacyAvailable">Légitimité PO disponible.</param>
/// <param name="ExtensionAllowed">Vrai si une loi d'extension couvre la commune qui paie.</param>
public sealed record FoundingOffer(
    string MaterialName,
    double MaterialQuantity,
    MaterialAvailability Availability,
    double LegitimacyCost,
    double Grant,
    double LegitimacyAvailable,
    bool ExtensionAllowed);

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
            Simulation.FoundingLegitimacyCostOf(payingCommuneId), Simulation.FoundingGrantOf(camp),
            World.Legitimacy[Simulation.FoundingPole], Simulation.AllowsExtension(payingCommuneId));
    }

    /// <summary>Ce qui empêche de fonder une commune à cette position, ou null si c'est permis.</summary>
    public string? CommuneSiteProblem(Point2D position) => World.CommuneSiteProblem(position);

    /// <summary>Le nom proposé pour la prochaine commune.</summary>
    public string NextCommuneName() => $"Commune {World.Communes.Count() + 1}";

    /// <summary>
    /// Fonde une commune à cette position : son camp de base est payé par la commune choisie (réserve de chantier, stock
    /// de l'État, puis achat avec le trésor), la légitimité PO (fonder dégrade la terre), la dotation par le trésor ; la
    /// charte est le pôle choisi. Il faut une loi d'extension qui couvre la commune qui paie.
    /// </summary>
    public DeskAnswer FoundCommune(string name, string payingCommuneId, Orientation pole, Point2D position)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new("Donnez un nom à la commune.");
        if (World.CommuneSiteProblem(position) is { } problem)
            return new($"Impossible de fonder ici : {problem}.");
        if (FoundingOfferFrom(payingCommuneId) is not { } offer)
            return new("Le camp de base ne se construit pas.");
        if (!offer.ExtensionAllowed)
            return new("Fonder une commune demande une loi d'extension (domaine Environnement).");
        if (offer.LegitimacyAvailable < offer.LegitimacyCost)
            return new($"Pas assez de légitimité {Simulation.FoundingPole.Code} : il en faut {offer.LegitimacyCost:0.#}, "
                + $"il y en a {offer.LegitimacyAvailable:0.#}. Fonder une commune dégrade la terre : elle se paie en productivisme.");
        if (World.EnvelopeOf(BudgetItem.Investment).Balance < offer.Grant)
            return new($"Le trésor ou le poste Investissements du budget n'a pas de quoi verser la dotation ({offer.Grant:N0}).");

        if (Simulation.TryFoundCommune(name.Trim(), payingCommuneId, PaymentMode.StockFirst, pole, offer.LegitimacyCost, position) is { } commune)
            return new($"{commune.Name} fondée, charte {WorldText.PoleName(pole)} ({pole.Code}), caisse de {World.CommuneFundOf(commune.Id).Balance:N0} ; "
                + "son camp de base est en chantier.", commune.Id);

        var available = offer.Availability;
        return new($"Fondation impossible : il faut {offer.MaterialQuantity:N0} {offer.MaterialName}. Il y a {available.Reserve:N0} en réserve "
            + $"de chantier, {available.StateStock:N0} dans le stock de l'État et {available.OnSale:N0} en vente, à acheter avec le trésor.");
    }
}
