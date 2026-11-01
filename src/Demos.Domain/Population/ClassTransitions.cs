namespace Demos.Domain.Population;

/// <summary>
/// Passages entre classes : chaque passage a son propre mécanisme. Une classe est définie par son rang et son
/// idéologie ; quand des membres changent de rang, ils gardent leur idéologie et rejoignent la classe d'arrivée,
/// qui naît si elle n'existe pas encore. Pour l'instant, deux passages vers le haut, par l'éducation :
/// ouvrier → profession intermédiaire (éducation de base) et profession intermédiaire → cadre (éducation avancée).
/// </summary>
public static class ClassTransitions
{
    /// <summary>Proposition : une année d'études (365 points) fait monter un membre d'un rang.</summary>
    public const double EducationPointsPerPromotion = 365;

    /// <summary>Proposition : au plus 2 % des membres d'une classe changent de rang par saison.</summary>
    public const double MaxSeasonalShare = 0.02;

    /// <summary>Un passage d'un rang à un autre, et la ressource de la classe qu'il dépense.</summary>
    private sealed record Promotion(Rank From, Rank To, Func<SocialClass, double> Points, Action<SocialClass, double> Spend);

    private static readonly Promotion[] Promotions =
    [
        new(Rank.Workers, Rank.Intermediate, c => c.EducationPoints, (c, spent) => c.EducationPoints -= spent),
        new(Rank.Intermediate, Rank.Executives, c => c.AdvancedEducationPoints, (c, spent) => c.AdvancedEducationPoints -= spent),
    ];

    /// <summary>Calcul saisonnier. Renvoie les classes nées de ces passages, à ajouter à la partie.</summary>
    public static List<SocialClass> RunSeasonal(IReadOnlyList<SocialClass> classes)
    {
        var all = classes.ToList();
        var created = new List<SocialClass>();

        foreach (var promotion in Promotions)
        {
            foreach (var source in all.Where(c => c.Rank == promotion.From && c.Population > 0).ToList())
            {
                var count = (long)Math.Min(promotion.Points(source) / EducationPointsPerPromotion, source.Population * MaxSeasonalShare);
                if (count <= 0)
                    continue;

                var target = all.FirstOrDefault(c => c.Rank == promotion.To && c.Ideology == source.Ideology);
                if (target is null)
                {
                    target = Create(promotion.To, source.Ideology);
                    all.Add(target);
                    created.Add(target);
                }

                promotion.Spend(source, count * EducationPointsPerPromotion);
                source.TransferMembersTo(target, count);
            }
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
