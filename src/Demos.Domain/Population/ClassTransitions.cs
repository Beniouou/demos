namespace Demos.Domain.Population;

/// <summary>
/// Passages entre classes : chaque passage a son propre mécanisme. Une classe est définie par son rang et son
/// idéologie ; quand des membres changent de rang, ils gardent leur idéologie et rejoignent la classe d'arrivée,
/// qui naît si elle n'existe pas encore. Seul le passage ouvrier → profession intermédiaire (par l'éducation)
/// existe pour l'instant.
/// </summary>
public static class ClassTransitions
{
    /// <summary>Proposition : une année d'école (365 points) fait passer un ouvrier en profession intermédiaire.</summary>
    public const double EducationPointsPerPromotion = 365;

    /// <summary>Proposition : au plus 2 % des membres d'une classe changent de rang par saison.</summary>
    public const double MaxSeasonalShare = 0.02;

    /// <summary>Calcul saisonnier. Renvoie les classes nées de ces passages, à ajouter à la partie.</summary>
    public static List<SocialClass> RunSeasonal(IReadOnlyList<SocialClass> classes)
    {
        var all = classes.ToList();
        var created = new List<SocialClass>();

        foreach (var workers in classes.Where(c => c.Rank == Rank.Workers && c.Population > 0))
        {
            var count = (long)Math.Min(workers.EducationPoints / EducationPointsPerPromotion, workers.Population * MaxSeasonalShare);
            if (count <= 0)
                continue;

            var intermediates = all.FirstOrDefault(c => c.Rank == Rank.Intermediate && c.Ideology == workers.Ideology);
            if (intermediates is null)
            {
                intermediates = Create(Rank.Intermediate, workers.Ideology);
                all.Add(intermediates);
                created.Add(intermediates);
            }

            workers.EducationPoints -= count * EducationPointsPerPromotion;
            workers.TransferMembersTo(intermediates, count);
        }

        return created;
    }

    /// <summary>
    /// Crée une classe vide ; son nom se forme à partir du rang et de l'idéologie
    /// (« Professions intermédiaires communistes »).
    /// </summary>
    public static SocialClass Create(Rank rank, Ideology ideology)
    {
        var ideologyName = ideology.Name.ToLowerInvariant();
        var plural = ideologyName.EndsWith('s') ? ideologyName : ideologyName + "s";
        return new SocialClass($"{RankSlug(rank)}-{ideologyName}", $"{RankName(rank)} {plural}", rank, ideology, 0);
    }

    public static string RankName(Rank rank) => rank switch
    {
        Rank.Workers => "Ouvriers",
        Rank.Intermediate => "Professions intermédiaires",
        _ => "Cadres",
    };

    private static string RankSlug(Rank rank) => rank switch
    {
        Rank.Workers => "ouvriers",
        Rank.Intermediate => "intermediaires",
        _ => "cadres",
    };
}
