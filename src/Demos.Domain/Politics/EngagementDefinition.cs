namespace Demos.Domain.Politics;

/// <summary>Sorte d'engagement de La Promesse (décidé le 23 novembre 2026).</summary>
public enum EngagementKind
{
    /// <summary>Rompu par toute une famille d'actes (toute loi qui favorise le pôle opposé…) : large et exigeant.</summary>
    Principle,

    /// <summary>Rompu par un acte précis : plus étroit, plus facile à tenir.</summary>
    Concrete,
}

/// <summary>
/// Un engagement de La Promesse : il porte sur un acte du joueur, jamais sur l'état de la société, et l'acte n'est jamais
/// bloqué (le rompre est une trahison). Le joueur en choisit un par axe de sa Promesse. Catalogue dans
/// game/data/engagements.json, feuille Engagements du tableur ; sans effet sur la partie pour l'instant.
/// </summary>
/// <param name="Pole">Le code du pôle que l'engagement défend (« EG », « CO »…).</param>
/// <param name="Breach">L'acte qui rompt l'engagement, en clair.</param>
/// <param name="Difficulty">De 1 (facile à tenir) à 3 (exigeant).</param>
/// <param name="Kept">Faux si l'engagement est écarté du catalogue (gardé pour mémoire).</param>
/// <param name="Note">Ce qui reste à créer ou à vérifier dans le jeu pour pouvoir le juger.</param>
public sealed record EngagementDefinition(
    string Id,
    string Pole,
    EngagementKind Kind,
    string Text,
    string Breach,
    int Difficulty,
    bool Kept = true,
    string? Note = null)
{
    public Orientation Orientation => Orientation.FromCode(Pole);
}
