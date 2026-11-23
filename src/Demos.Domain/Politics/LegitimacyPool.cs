namespace Demos.Domain.Politics;

/// <summary>
/// Légitimité : la ressource du joueur. Elle est polaire, avec un stock par pôle idéologique
/// (EG, SE, PO, EC, CO, LI).
/// </summary>
public sealed class LegitimacyPool
{
    private readonly Dictionary<Orientation, double> _stocks = [];

    /// <summary>Le relevé des gains et des dépenses (pour l'interface).</summary>
    public LegitimacyLedger Ledger { get; } = new();

    /// <summary>La date des dépenses notées au relevé : la simulation y branche son horloge.</summary>
    public Func<DateOnly> Clock { get; set; } = () => DateOnly.MinValue;

    public double this[Orientation pole] => _stocks.GetValueOrDefault(pole);

    /// <summary>
    /// Ajoute de la légitimité. Avec un motif, c'est de la légitimité rendue (un vote annulé) : elle est notée au relevé
    /// comme une dépense négative.
    /// </summary>
    public void Add(Orientation pole, double amount, LegitimacySpendingKind? refund = null, string subject = "")
    {
        _stocks[pole] = this[pole] + amount;
        if (refund is { } kind)
            Ledger.RecordSpending(new LegitimacySpending(Clock(), pole, -amount, kind, subject));
    }

    /// <summary>La légitimité qu'un groupe produit en acceptant une loi : ajoutée au stock et notée au relevé du jour.</summary>
    public void Earn(Orientation pole, double amount, string lawId, string classId)
    {
        _stocks[pole] = this[pole] + amount;
        Ledger.RecordGain(new LegitimacyGainKey(pole, lawId, classId), amount);
    }

    /// <summary>
    /// Dépense la légitimité d'un pôle. Renvoie false, sans rien dépenser, si le stock est insuffisant (un pôle en dette ne
    /// paie rien : fonder, nationaliser, vendre…). Avec un motif, la dépense est notée au relevé.
    /// </summary>
    public bool TrySpend(Orientation pole, double amount, LegitimacySpendingKind? kind = null, string subject = "")
    {
        if (this[pole] < amount)
            return false;

        _stocks[pole] = this[pole] - amount;
        Note(pole, amount, kind, subject);
        return true;
    }

    private void Note(Orientation pole, double amount, LegitimacySpendingKind? kind, string subject)
    {
        if (kind is { } spending)
            Ledger.RecordSpending(new LegitimacySpending(Clock(), pole, amount, spending, subject));
    }

    /// <summary>
    /// L'usure de la légitimité (décidé le 23 novembre 2026) : chaque pôle perd cette part de son stock positif ; une dette
    /// ne s'use pas. La perte est notée au relevé.
    /// </summary>
    public void Erode(double share)
    {
        foreach (var (pole, stock) in _stocks.Where(s => s.Value > 0).ToList())
        {
            var lost = stock * share;
            _stocks[pole] = stock - lost;
            Note(pole, lost, LegitimacySpendingKind.Erosion, "");
        }
    }

    /// <summary>Vrai si le pôle peut payer ce montant en s'endettant au plus de <paramref name="maxDebt"/> (voter à crédit).</summary>
    public bool CanSpendOnCredit(Orientation pole, double amount, double maxDebt) => this[pole] - amount >= -maxDebt - 1e-9;

    /// <summary>
    /// Dépense à crédit (voter une loi, décidé le 23 novembre 2026) : le stock peut passer en négatif, jusqu'à
    /// <paramref name="maxDebt"/> ; la légitimité gagnée ensuite rembourse la dette. Renvoie false, sans rien dépenser,
    /// au-delà du plafond.
    /// </summary>
    public bool TrySpendOnCredit(Orientation pole, double amount, double maxDebt, LegitimacySpendingKind? kind = null, string subject = "")
    {
        if (!CanSpendOnCredit(pole, amount, maxDebt))
            return false;

        _stocks[pole] = this[pole] - amount;
        Note(pole, amount, kind, subject);
        return true;
    }
}
