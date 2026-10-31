using Demos.Domain.Economy;
using Demos.Domain.Population;

namespace Demos.Application;

/// <summary>État complet d'une partie.</summary>
public sealed class World(IReadOnlyList<ProductDefinition> products, IReadOnlyList<SocialClass> classes)
{
    public IReadOnlyList<ProductDefinition> Products { get; } = products;
    public IReadOnlyList<SocialClass> Classes { get; } = classes;

    /// <summary>Idéologie du régime. Plus tard : moyenne des points d'idéologie des lois en vigueur.</summary>
    public Ideology RegimeIdeology { get; set; }
}
