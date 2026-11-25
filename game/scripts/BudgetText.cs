using Demos.Domain.Economy;

namespace Demos.Game;

/// <summary>Les textes de l'onglet Économie → Budget et leurs bulles d'aide.</summary>
public static class BudgetText
{
    public static string Name(BudgetItem item) => item switch
    {
        BudgetItem.Grants => "Dotations aux communes",
        BudgetItem.Construction => "Chantiers",
        BudgetItem.Roads => "Routes",
        _ => "Investissements",
    };

    /// <summary>Le poste, avec le rond de son pôle.</summary>
    public static string Label(BudgetItem item) =>
        $"{LegitimacyText.PoleCode(BudgetItems.PoleOf(item))} {Name(item)}";

    public static string Hint(BudgetItem item) => item switch
    {
        BudgetItem.Grants =>
            "Un montant par habitant et par an : chaque commune reçoit le 1er de chaque mois un douzième de ce montant × ses habitants du moment. Une commune qui grandit reçoit donc plus. "
            + "Une commune paie les salaires et le fonctionnement de ses bâtiments publics avec sa caisse seule : le trésor ne prend plus le relais.",
        BudgetItem.Construction => "Les matériaux achetés pour construire : chantiers, camp de base d'une commune, siège d'une préfecture. Enveloppe vide : aucun chantier ne démarre.",
        BudgetItem.Roads => "Construction et entretien des liaisons payées par l'État. Enveloppe vide : pas de route neuve, et une liaison non entretenue descend d'un niveau.",
        _ => "La dotation de fondation d'une commune et le rachat d'une nationalisation. Enveloppe vide : l'action est refusée.",
    };

    public const string Voted = "L'enveloppe de l'année, entière dès le 1er janvier. Ce qui n'est pas dépensé au 31 décembre est perdu pour le poste (le trésor le garde).";

    public const string Spent = "Ce que le poste a dépensé depuis le 1er janvier (ou depuis le début de la partie).";

    public const string Available = "Ce qui reste au poste cette année. On ne dépense jamais plus que le trésor.";

    public const string Projected =
        "La dépense de l'année si elle continue au même rythme (pour les dotations : avec les habitants d'aujourd'hui). ⚠ quand l'enveloppe ne suffira pas.";

    public const string Draft =
        "Le prochain budget (pour les dotations, un montant par habitant et par an). Voté, il entre en vigueur le 1er du mois suivant et remplace les enveloppes de l'année ; ce qui a déjà été dépensé reste compté. Au 1er janvier, le budget est reconduit tel quel.";

    public const string Cost =
        "Chaque poste qui augmente coûte, dans son pôle, un coût de base plus une part de l'augmentation (pour les dotations : la hausse par habitant × les habitants du pays). Baisser ou reconduire ne coûte rien. On peut voter à crédit, comme une loi.";

    public const string Funds = "La caisse de chaque commune, ses habitants et la dotation qu'elle reçoit chaque mois avec le budget en vigueur.";
}
