using Demos.Application;
using Demos.Domain.Politics;

namespace Demos.Game;

/// <summary>Les textes des onglets Légitimité (Population et Communes) et leurs bulles d'aide.</summary>
public static class LegitimacyText
{
    /// <summary>« ● EG égalitariste », le rond à la couleur du pôle.</summary>
    public static string Pole(Orientation pole) =>
        $"[color=#{UiTheme.PoleColor(pole).ToHtml(false)}]●[/color] {pole.Code} {WorldText.PoleName(pole)}";

    /// <summary>« ● EG », pour les colonnes étroites.</summary>
    public static string PoleCode(Orientation pole) =>
        $"[color=#{UiTheme.PoleColor(pole).ToHtml(false)}]●[/color] {pole.Code}";

    /// <summary>Un gain ou un solde signé : « +1,25 », « −0,40 », « – » pour rien.</summary>
    public static string Signed(double value, string format = "N2") =>
        System.Math.Abs(value) < 0.005 ? "–" : value > 0 ? $"+{value.ToString(format)}" : $"−{(-value).ToString(format)}";

    /// <summary>Le motif d'une dépense, avec ce qu'elle visait.</summary>
    public static string Spending(LegitimacySpendingView spending)
    {
        var subject = SheetText.Escape(spending.Subject);
        return spending.Kind switch
        {
            LegitimacySpendingKind.LawVote => $"Vote de la loi « {subject} »",
            LegitimacySpendingKind.VoteCancelled => $"Vote annulé de la loi « {subject} » (rendu)",
            LegitimacySpendingKind.Founding => $"Fondation de {subject}",
            LegitimacySpendingKind.Prefecture => $"Création de la préfecture {subject}",
            LegitimacySpendingKind.Attach => $"Rattachement de {subject}",
            LegitimacySpendingKind.Sale => $"Vente de {subject}",
            LegitimacySpendingKind.Erosion => "Usure : la légitimité s'use avec le temps",
            _ => $"Nationalisation de {subject}",
        };
    }

    public const string Stock =
        "La légitimité de ce pôle aujourd'hui : ce que le joueur peut dépenser pour agir. Elle peut passer sous zéro en votant une loi à crédit.";

    public const string PerDay =
        "Le gain moyen par jour sur les jours relevés (30 au plus) : ce que produisent les groupes qui acceptent les lois orientées vers ce pôle.";

    public const string Earned = "Ce que les groupes ont produit sur les jours relevés (30 au plus).";

    public const string Spent =
        "Ce que le joueur a dépensé sur les mêmes jours : lois votées, fondations, préfectures, rattachements, ventes, nationalisations. La légitimité rendue (vote annulé) est déduite.";

    public const string Balance = "Gagné moins dépensé sur les jours relevés.";

    public const string ByLaw =
        "Le gain moyen par jour de chaque loi en vigueur, pôle par pôle. Une loi ne rapporte que dans les groupes du même pôle qu'elle ; les autres la rejettent et ne produisent rien.";

    public const string Efficiency =
        "La part des effets de la loi qui arrive jusqu'à cette commune (extension et bureaucratie) : une loi mal appliquée rapporte moins.";

    public const string Produced =
        "Le gain moyen par jour de ce groupe, sur les jours relevés. Un groupe qui rejette les lois (adhésion faible) ne produit presque rien.";

    public const string History =
        "Le stock de chaque pôle : un point par jour sur les 30 derniers jours, puis un point par mois. Le relevé repart de zéro à chaque lancement.";

    public const string Adhesion =
        "La part, pondérée par les membres concernés, des orientations de lois que le groupe accepte.";
}
