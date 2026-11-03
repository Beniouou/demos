namespace Demos.Domain.Economy;

/// <summary>
/// Un besoin des habitants (se nourrir, s'habiller…), par son identifiant stable. La liste des besoins et leurs noms
/// sont des données (game/data/besoins.json) : on en ajoute sans toucher au code. Les besoins connus du code ont un nom
/// ici ; se nourrir garde un rôle à part (le grenier stocke la nourriture), se loger et l'énergie ne s'achètent pas
/// comme des produits.
/// </summary>
public readonly record struct NeedCategory(string Id)
{
    public static readonly NeedCategory Food = new("Food");
    public static readonly NeedCategory Clothing = new("Clothing");
    public static readonly NeedCategory Furniture = new("Furniture");
    public static readonly NeedCategory Housing = new("Housing");

    /// <summary>Être administré : les services administratifs (mairie, préfecture, palais).</summary>
    public static readonly NeedCategory Administration = new("Administration");

    /// <summary>Être protégé : les services de sécurité (caserne de pompiers, poste de police).</summary>
    public static readonly NeedCategory Security = new("Security");

    public static readonly NeedCategory Energy = new("Energy");

    /// <summary>Se distraire : les activités et le plaisir (l'alcool pour commencer).</summary>
    public static readonly NeedCategory Leisure = new("Leisure");

    /// <summary>Le confort matériel (vaisselle, bibelots, véhicules…), pour les niveaux suivants.</summary>
    public static readonly NeedCategory Comfort = new("Comfort");

    public override string ToString() => Id;
}

/// <summary>Un besoin de la liste des besoins : son identifiant et son nom affiché.</summary>
public sealed record NeedDefinition(string Id, string Name, string? Description = null)
{
    public NeedCategory Category => new(Id);
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
