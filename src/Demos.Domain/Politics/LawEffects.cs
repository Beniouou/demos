using Demos.Domain.Economy;
using Demos.Domain.Population;

namespace Demos.Domain.Politics;

public static class LawEffects
{
    /// <summary>Légitimité produite par jour et par membre concerné : 1 point pour 1 000 membres.</summary>
    public const double LegitimacyPerMemberPerDay = 0.001;

    /// <summary>
    /// Calcul quotidien. Chaque classe juge chaque brique des lois en vigueur sur ses pôles, selon le sens du curseur.
    /// Le poids de son jugement est la part de ses membres touchée (les membres visés par le sujet comptent pour 1, les
    /// autres membres du niveau pour moitié), multipliée par l'intensité et le poids de la brique. Une brique acceptée
    /// produit de la légitimité de son pôle ; l'adhésion est la part acceptée de ce que la classe juge.
    /// </summary>
    public static void ApplyDaily(IEnumerable<SocialClass> classes, LawBook book, LegitimacyPool legitimacy)
    {
        var untargetedWeight = Balance.Current.UntargetedMemberWeight;

        foreach (var socialClass in classes)
        {
            double applicable = 0;
            double accepted = 0;

            foreach (var law in book.Laws)
            {
                if (!book.Structures.TryGetValue(law.StructureId, out var level))
                    continue;

                foreach (var setting in law.Bricks)
                {
                    if (book.MovementOf(law, setting) is not { } movement || movement.Intensity <= 0)
                        continue;

                    var share = book.WeightedShare(socialClass, setting.Subject, level, untargetedWeight);
                    var weight = share * movement.Intensity * movement.Brick.Weight;
                    if (weight <= 0)
                        continue;

                    foreach (var pole in movement.Poles)
                    {
                        applicable += weight;
                        if (socialClass.Ideology[pole.Axis] != pole.Pole)
                            continue;

                        accepted += weight;
                        legitimacy.Add(pole, LegitimacyPerMemberPerDay * socialClass.Population * weight);
                    }
                }
            }

            socialClass.Adhesion = applicable > 0 ? accepted / applicable : 1;
        }
    }
}
