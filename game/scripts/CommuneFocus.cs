using System.Collections.Generic;
using System.Linq;
using Demos.Application;

namespace Demos.Game;

/// <summary>
/// La commune du menu Communes (décidé le 20 novembre 2026) : la même dans tous ses onglets, gardée quand on change
/// d'onglet ou qu'on ferme le menu. Les flèches passent à la commune d'avant ou d'après ; un clic sur une commune de la
/// carte la choisit. Chaque changement est annoncé (la carte se centre sur elle).
/// </summary>
public sealed class CommuneFocus
{
    /// <summary>La commune choisie, ou null tant qu'il n'y en a pas.</summary>
    public string? Id { get; private set; }

    /// <summary>Une autre commune a été choisie.</summary>
    public event System.Action<string>? Changed;

    /// <summary>Choisit une commune (sans effet si c'est déjà elle).</summary>
    public void Select(string communeId)
    {
        if (communeId == Id)
            return;
        Id = communeId;
        Changed?.Invoke(communeId);
    }

    /// <summary>
    /// La commune choisie parmi celles de la partie : la première si aucune ne l'est encore, ou si la commune choisie
    /// n'existe plus.
    /// </summary>
    public CommuneView? Current(IReadOnlyList<CommuneView> communes)
    {
        if (communes.Count == 0)
            return null;
        if (communes.FirstOrDefault(c => c.Id == Id) is { } current)
            return current;
        Select(communes[0].Id);
        return communes[0];
    }

    /// <summary>Passe à la commune suivante (+1) ou précédente (−1), en faisant le tour.</summary>
    public void Step(IReadOnlyList<CommuneView> communes, int direction)
    {
        if (communes.Count == 0)
            return;
        var index = communes.ToList().FindIndex(c => c.Id == Id);
        Select(communes[((index < 0 ? 0 : index + direction) % communes.Count + communes.Count) % communes.Count].Id);
    }
}
