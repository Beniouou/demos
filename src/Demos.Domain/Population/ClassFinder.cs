namespace Demos.Domain.Population;

/// <summary>
/// Trouve le groupe local d'arrivée d'un passage ou d'une migration (même rang, même idéologie, même commune),
/// ou le crée s'il n'existe pas encore.
/// </summary>
public sealed class ClassFinder(IReadOnlyList<SocialClass> classes)
{
    public List<SocialClass> All { get; } = classes.ToList();

    /// <summary>Groupes créés pendant ce calcul, à ajouter à la partie.</summary>
    public List<SocialClass> Created { get; } = [];

    public SocialClass Get(Rank rank, Ideology ideology, string? communeId)
    {
        var target = All.FirstOrDefault(c => c.Rank == rank && c.Ideology == ideology && c.CommuneId == communeId);
        if (target is not null)
            return target;

        target = ClassTransitions.Create(rank, ideology, communeId);
        All.Add(target);
        Created.Add(target);
        return target;
    }
}
