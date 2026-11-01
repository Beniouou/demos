using Demos.Domain.Population;

namespace Demos.Domain.Politics;

public static class LawEffects
{
    /// <summary>Légitimité produite par jour et par membre concerné : 1 point pour 10 000 membres.</summary>
    public const double LegitimacyPerMemberPerDay = 0.0001;

    /// <summary>
    /// Calcul quotidien. Pour chaque classe et chaque loi qui s'applique à elle, en proportion
    /// des membres concernés : une orientation acceptée produit de la légitimité de ce pôle,
    /// et l'adhésion est la part des orientations acceptées.
    /// </summary>
    public static void ApplyDaily(
        IEnumerable<SocialClass> classes,
        IReadOnlyList<Law> laws,
        IReadOnlyDictionary<string, Structure> structures,
        LegitimacyPool legitimacy)
    {
        foreach (var socialClass in classes)
        {
            double applicable = 0;
            double accepted = 0;

            foreach (var law in laws)
            {
                var share = socialClass.ShareSubjectTo(structures[law.StructureId]);
                if (share <= 0)
                    continue;

                foreach (var orientation in law.Orientations)
                {
                    applicable += share;
                    if (socialClass.Ideology[orientation.Axis] != orientation.Pole)
                        continue;

                    accepted += share;
                    legitimacy.Add(orientation, LegitimacyPerMemberPerDay * socialClass.Population * share);
                }
            }

            socialClass.Adhesion = applicable > 0 ? accepted / applicable : 1;
        }
    }
}
