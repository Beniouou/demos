namespace Demos.Domain.Economy;

public enum NeedCategory
{
    Food,
    Clothing,
    Furniture,
    Housing,

    /// <summary>Être administré : les services administratifs (mairie, préfecture, palais).</summary>
    Administration,

    /// <summary>Être protégé : les services de sécurité (caserne de pompiers, poste de police).</summary>
    Security,

    Energy,

    /// <summary>Se distraire : les activités et le plaisir (l'alcool pour commencer).</summary>
    Leisure,

    /// <summary>Le confort matériel (vaisselle, bibelots, véhicules…), pour les niveaux suivants.</summary>
    Comfort,
}

/// <summary>Bien, service ou matière. Une matière (pierre, bois…) ne couvre aucun besoin.</summary>
/// <param name="BasePrice">Prix de départ sur le marché ; la formation des prix reste à définir.</param>
/// <param name="ShelfLifeDays">
/// Durée de vie de base d'un produit périssable (pain, farine, alcool…), en jours, dans un stockage général ; plus longue
/// dans un grenier ou un stockage spécifique. Null pour un produit qui ne se périme pas.
/// </param>
/// <param name="Stockable">
/// Faux pour un produit qui se consomme le jour même ou se perd (l'eau, plus tard l'électricité), sauf dans un
/// stockage fait pour lui (le château d'eau).
/// </param>
public sealed record ProductDefinition(
    string Id, string Name, NeedCategory? Need, double BasePrice, double? ShelfLifeDays = null, bool Stockable = true)
{
    public bool IsPerishable => ShelfLifeDays is > 0;
}
