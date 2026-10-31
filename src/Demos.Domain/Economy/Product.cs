namespace Demos.Domain.Economy;

public enum NeedCategory
{
    Food,
    Clothing,
    Furniture,
    Housing,
    Services,
    Energy,
}

/// <summary>Bien ou service consommé par les classes pour couvrir un besoin.</summary>
public sealed record ProductDefinition(string Id, string Name, NeedCategory Need);
