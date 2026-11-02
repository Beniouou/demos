namespace Demos.Domain.Economy;

public enum NeedCategory
{
    Food,
    Clothing,
    Furniture,
    Housing,
    Services,
    Energy,

    /// <summary>Se distraire : les activités et le plaisir (l'alcool pour commencer).</summary>
    Leisure,

    /// <summary>Le confort matériel (vaisselle, bibelots, véhicules…), pour les niveaux suivants.</summary>
    Comfort,
}

/// <summary>Bien, service ou matière. Une matière (pierre, bois…) ne couvre aucun besoin.</summary>
/// <param name="BasePrice">Prix de départ sur le marché ; la formation des prix reste à définir.</param>
public sealed record ProductDefinition(string Id, string Name, NeedCategory? Need, double BasePrice);
