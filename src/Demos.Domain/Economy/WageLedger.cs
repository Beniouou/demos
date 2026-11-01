namespace Demos.Domain.Economy;

/// <summary>
/// Salaires dus et versés par chaque bâtiment, jour par jour, sur les derniers jours. Sert à repérer une entreprise
/// en difficulté, qui ne paie plus ses travailleurs.
/// </summary>
public sealed class WageLedger
{
    /// <summary>Proposition : on juge un bâtiment sur ses 30 derniers jours.</summary>
    public const int Days = 30;

    /// <summary>Proposition : un bâtiment est en difficulté quand il a versé moins de 80 % des salaires dus.</summary>
    public const double StrugglingThreshold = 0.8;

    private readonly Dictionary<string, Queue<(double Due, double Paid)>> _days = [];

    public void Record(string buildingId, double due, double paid)
    {
        if (!_days.TryGetValue(buildingId, out var days))
            _days[buildingId] = days = new Queue<(double, double)>();

        days.Enqueue((due, paid));
        while (days.Count > Days)
            days.Dequeue();
    }

    /// <summary>Part des salaires dus effectivement versée sur les derniers jours ; null si rien n'était dû.</summary>
    public double? PaidShare(string buildingId)
    {
        if (!_days.TryGetValue(buildingId, out var days))
            return null;

        var due = days.Sum(d => d.Due);
        return due > 0 ? days.Sum(d => d.Paid) / due : null;
    }

    public bool IsStruggling(string buildingId) => PaidShare(buildingId) < StrugglingThreshold;

    /// <summary>Oublie l'historique d'un bâtiment (quand il change de propriétaire).</summary>
    public void Forget(string buildingId) => _days.Remove(buildingId);
}
