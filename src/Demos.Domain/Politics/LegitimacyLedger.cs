namespace Demos.Domain.Politics;

/// <summary>Ce qu'a coûté (ou rendu) une dépense de légitimité.</summary>
public enum LegitimacySpendingKind
{
    LawVote,
    VoteCancelled,
    Founding,
    Prefecture,
    Attach,
    Sale,
    Nationalization,
}

/// <summary>Une part du gain d'un jour : ce qu'un groupe a produit dans un pôle en acceptant une loi.</summary>
public readonly record struct LegitimacyGainKey(Orientation Pole, string LawId, string ClassId);

/// <summary>
/// Une dépense de légitimité : sa date, son pôle, le montant (négatif quand la légitimité est rendue, à l'annulation d'un
/// vote), son motif et ce qu'elle visait (l'identifiant d'une loi, d'une commune, d'un bâtiment…).
/// </summary>
public sealed record LegitimacySpending(DateOnly Date, Orientation Pole, double Amount, LegitimacySpendingKind Kind, string Subject);

/// <summary>Le gain d'un jour joué, groupe par groupe, loi par loi et pôle par pôle.</summary>
public sealed record LegitimacyDay(DateOnly Date, IReadOnlyDictionary<LegitimacyGainKey, double> Gains);

/// <summary>
/// Le relevé de la légitimité (décidé le 23 novembre 2026) : ce que chaque groupe produit chaque jour, et chaque dépense
/// avec son motif, gardés <see cref="Days"/> jours. C'est un relevé pour l'interface : il ne change aucune règle.
/// </summary>
public sealed class LegitimacyLedger
{
    /// <summary>Le nombre de jours gardés dans le détail.</summary>
    public const int Days = 30;

    private readonly List<LegitimacyDay> _days = [];
    private readonly List<LegitimacySpending> _spendings = [];
    private Dictionary<LegitimacyGainKey, double>? _current;

    /// <summary>Les jours joués gardés, du plus ancien au plus récent.</summary>
    public IReadOnlyList<LegitimacyDay> GainDays => _days;

    /// <summary>Les dépenses gardées, de la plus ancienne à la plus récente.</summary>
    public IReadOnlyList<LegitimacySpending> Spendings => _spendings;

    /// <summary>Ouvre le relevé d'un jour joué (un second appel le même jour repart de zéro) et oublie ce qui a plus de 30 jours.</summary>
    public void BeginDay(DateOnly date)
    {
        _current = [];
        if (_days.Count > 0 && _days[^1].Date == date)
            _days[^1] = new LegitimacyDay(date, _current);
        else
            _days.Add(new LegitimacyDay(date, _current));

        var limit = date.AddDays(-(Days - 1));
        _days.RemoveAll(d => d.Date < limit);
        _spendings.RemoveAll(s => s.Date < limit);
    }

    /// <summary>Note un gain dans le jour ouvert (sans jour ouvert, rien n'est noté).</summary>
    public void RecordGain(LegitimacyGainKey key, double amount)
    {
        if (_current is null || amount == 0)
            return;
        _current[key] = _current.GetValueOrDefault(key) + amount;
    }

    public void RecordSpending(LegitimacySpending spending)
    {
        if (spending.Amount != 0)
            _spendings.Add(spending);
    }
}
