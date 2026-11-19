namespace Demos.Game;

/// <summary>
/// Les lignes d'approvisionnement du joueur (Production → Chaîne de production) : créer, retirer, changer la part
/// envoyée. Chaque action passe par la simulation, qui vérifie la règle ; la réponse dit ce qui s'est passé.
/// </summary>
public sealed partial class PlayerDesk
{
    public DeskAnswer AddSupplyLine(string supplierId, string clientId)
    {
        var (line, problem) = Simulation.TryAddSupplyLine(supplierId, clientId);
        return line is null
            ? new DeskAnswer($"Ligne impossible : {problem}.")
            : new DeskAnswer($"Ligne créée : {Name(supplierId)} → {Name(clientId)}, {line.Share:P0} de sa production.", line.Id);
    }

    public DeskAnswer RemoveSupplyLine(string lineId) =>
        new(Simulation.RemoveSupplyLine(lineId) ? "Ligne retirée ; ce qui est déjà en réserve reste au client." : "Ligne inconnue.");

    /// <summary>Change la part de la production du fournisseur que la ligne envoie (de 5 % à 100 %).</summary>
    public DeskAnswer SetSupplyLineShare(string lineId, double share) =>
        new(Simulation.SetSupplyLineShare(lineId, share) ? $"Part envoyée : {System.Math.Clamp(share, 0.05, 1):P0}." : "Ligne inconnue.");

    /// <summary>Fait passer une ligne avant (ou après) une autre ligne du même client : sa priorité d'achat.</summary>
    public DeskAnswer MoveSupplyLine(string lineId, bool earlier) =>
        new(Simulation.MoveSupplyLine(lineId, earlier) ? "Priorité changée." : "Cette ligne est déjà à cette place.");

    /// <summary>
    /// Change un réglage d'achat ou de vente d'un bâtiment public (n'acheter que par ses lignes, vendeurs refusés,
    /// réserve hors vente, pas d'envoi). La réponse dit ce qui s'est passé, ou pourquoi c'est impossible.
    /// </summary>
    public DeskAnswer ChangeSupplySettings(string buildingId, System.Func<Demos.Domain.Economy.SupplySettings, Demos.Domain.Economy.SupplySettings> change)
    {
        var settings = change(World.SupplySettingsOf(buildingId));
        return Simulation.SetSupplySettings(buildingId, settings) is { } problem
            ? new DeskAnswer($"Impossible : {problem}.")
            : new DeskAnswer($"Réglages enregistrés : {Name(buildingId)}.");
    }

    private string Name(string structureId) =>
        World.Structures.TryGetValue(structureId, out var structure) ? World.DisplayNameOf(structure) : structureId;
}
