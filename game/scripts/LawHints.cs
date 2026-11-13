using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Game;

/// <summary>
/// Les bulles d'aide des fiches de lois (code civil, éditeur de lois) : ce que veut dire chaque terme grisé, d'après les
/// règles codées (<see cref="LawBook"/>, <see cref="LawCost"/>). Les chiffres viennent des réglages (equilibrage.json).
/// </summary>
public static class LawHints
{
    public const string Level =
        "Niveau : la structure où la loi est votée (l'État, une préfecture, une commune). Elle s'applique à ce niveau et à tout ce qu'il contient.";

    public const string Subject =
        "Sujet : à qui ou à quoi s'applique la brique dans le niveau (tout le monde, un rang, un statut d'entreprise, des types ou catégories de bâtiments, un besoin).";

    public const string AppliesTo =
        "Une loi votée à un niveau s'applique à lui et à tout ce qu'il contient : ses communes, ses organismes, ses bâtiments. "
        + "Un niveau en dessous peut voter sa propre valeur, sauf pour une brique verrouillée au-dessus.";

    public const string Regalian =
        "Domaine régalien : normalement du rôle de l'État. Une loi de ce domaine votée sous l'État coûte plus cher (voir le coût).";

    /// <summary>La valeur héritée, selon la famille de la brique.</summary>
    public static string Inherited(BrickFamily family) => family switch
    {
        BrickFamily.Modifier =>
            "Hérité : un modificateur part toujours de 0. Les modificateurs de chaque niveau s'additionnent (+20 % à l'État et +10 % dans la commune font +30 %).",
        BrickFamily.Permission =>
            "Hérité : ce que décident les lois des niveaux au-dessus pour ce sujet ; sans loi au-dessus, la valeur par défaut de la brique. "
            + "Entre deux niveaux, une seule loi s'applique : la plus proche.",
        _ =>
            "Hérité : la valeur que fixent les lois des niveaux au-dessus pour ce sujet ; sans loi au-dessus, la valeur par défaut de la brique. "
            + "Entre deux niveaux, une seule loi s'applique : la plus proche.",
    };

    public const string Bounds = "Bornes : le curseur de cette brique ne peut pas sortir de ces valeurs.";

    public const string Default = "Par défaut : la valeur de la brique quand aucune loi ne la règle.";

    public const string Intensity =
        "Écart : la part du chemin parcourue entre la valeur héritée et la borne, de 0 % à 100 % (de 2 à 2,5 avec une borne à 4 : 25 %). "
        + "Le coût de la brique est proportionnel à l'écart ; un écart de 0 % ne coûte rien. Les pôles suivent le sens du mouvement.";

    public static string Locked(bool locked) => locked
        ? $"Verrouillée : les niveaux en dessous ne peuvent plus changer cette brique pour ce sujet. Verrouiller coûte ×{Balance.Current.LockedBrickCostFactor:0.##}."
        : "Non verrouillée : un niveau en dessous (une préfecture, une commune) peut voter sa propre valeur pour ce sujet.";

    public const string Poles =
        "Pôles de la brique : ceux que favorise son mouvement (une hausse ou une baisse). Le coût de la loi se paie dans ces pôles.";

    /// <summary>Comment se calcule le coût d'un changement de loi.</summary>
    public static string Cost()
    {
        var settings = Balance.Current;
        return "Coût en légitimité, brique par brique : membres concernés × "
            + $"{LawEffects.LegitimacyPerMemberPerDay:0.###} × {settings.LawPaybackDays:0} jours × écart × poids de la brique, au moins {settings.MinimumBrickCost:0.#}. "
            + $"Les membres du niveau hors du sujet comptent pour {settings.UntargetedMemberWeight:P0}. "
            + $"×{settings.LockedBrickCostFactor:0.##} si la brique est verrouillée, ×{settings.RegalianCostFactor:0.##} pour un domaine régalien voté sous l'État. "
            + "Payé dans les pôles du mouvement, à parts égales. Il suit les membres du moment : il change d'un jour à l'autre.";
    }

    public const string Paid =
        "Payé au vote et figé. Annuler le vote avant le 1er du mois rend toute la légitimité.";
}
