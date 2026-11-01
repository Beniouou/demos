using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// L'école est une capacité, comme le logement : elle offre des places d'élèves dans sa commune
/// (voir <see cref="PlaceAllocation"/>). Chaque place occupée rapporte chaque jour des points d'éducation
/// à la classe de l'élève. Une école publique est gratuite ; une école privée fait payer des frais.
/// </summary>
public static class Education
{
    public const double PointsPerPupilPerDay = 1;

    public static void RunDaily(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<SocialClass> classes,
        Treasury treasury)
    {
        var pupils = PlaceAllocation.Allocate(structures, classes, treasury, s => s.SchoolPlaces, s => s.DailyTuition);

        foreach (var (socialClass, members) in pupils)
            socialClass.EducationPoints += members * PointsPerPupilPerDay;
    }
}
