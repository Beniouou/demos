using System.Collections.Generic;
using Demos.Domain.Politics;

namespace Demos.Game;

/// <summary>Les décisions du joueur sur La Promesse (page La Promesse, fenêtre du pacte déchiré).</summary>
public sealed partial class PlayerDesk
{
    /// <summary>Renomme le peuple ; un nom vide est refusé.</summary>
    public string NamePeople(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Le nom du peuple ne peut pas être vide.";
        Simulation.NamePeople(name);
        return $"Le peuple s'appelle désormais {name.Trim()}.";
    }

    /// <summary>Après un pacte déchiré : la nation continue sans promesse (bac à sable).</summary>
    public string ContinueWithoutPromise()
    {
        Simulation.ContinueWithoutPromise();
        return "La nation continue sans sa promesse. Seule la révolution reste une fin.";
    }

    /// <summary>Après un pacte déchiré : une nouvelle Promesse et un nouveau serment, au prix d'une crise et d'une cicatrice.</summary>
    public string Refound(PromiseDefinition promise, IReadOnlyList<EngagementDefinition> engagements,
        IReadOnlyList<IReadOnlyList<Demos.Domain.Map.Point2D>> signature)
    {
        if (Simulation.Refound(promise, engagements) is { } problem)
            return $"Refondation impossible : {problem}.";
        Simulation.SignPromise(signature);
        return $"La Promesse est refondée : {promise.Name}. Toutes les classes traversent une crise.";
    }
}
