using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>
/// L'éducation se raffine : l'éducation de base (école, puis lycée qui en rapporte plus) prépare les ouvriers
/// à devenir professions intermédiaires ; l'éducation avancée (université) prépare les professions
/// intermédiaires à devenir cadres.
/// </summary>
public enum EducationLevel
{
    Basic,
    Advanced,
}

/// <summary>
/// Les établissements d'enseignement sont des capacités, comme le logement (voir <see cref="PlaceAllocation"/>) :
/// ils offrent des places d'élèves dans leur commune, et chaque élève rapporte chaque jour des points à sa classe.
/// L'éducation de base n'accueille que les ouvriers ; l'éducation avancée, que les professions intermédiaires,
/// qui sont déjà passées par l'école. Un établissement public est gratuit ; un établissement privé fait payer des frais.
/// </summary>
public static class Education
{
    public static Rank StudentsOf(EducationLevel level) => level == EducationLevel.Basic ? Rank.Workers : Rank.Intermediate;

    public static void RunDaily(
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyList<SocialClass> classes,
        Treasury treasury,
        Func<Structure, Treasury>? companyFundOf = null)
    {
        foreach (var level in Enum.GetValues<EducationLevel>())
        {
            var pupils = PlaceAllocation.Allocate(
                structures,
                classes,
                treasury,
                s => s.SchoolLevel == level ? s.SchoolPlaces : 0,
                s => s.DailyTuition,
                c => c.Rank == StudentsOf(level),
                s => s.PointsPerPupil,
                companyFundOf);

            foreach (var (socialClass, served) in pupils)
            {
                if (level == EducationLevel.Basic)
                    socialClass.EducationPoints += served.Value;
                else
                    socialClass.AdvancedEducationPoints += served.Value;
            }
        }
    }
}
