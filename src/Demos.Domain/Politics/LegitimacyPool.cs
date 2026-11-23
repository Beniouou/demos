namespace Demos.Domain.Politics;

/// <summary>
/// Légitimité : la ressource du joueur. Elle est polaire, avec un stock par pôle idéologique
/// (EG, SE, PO, EC, CO, LI).
/// </summary>
public sealed class LegitimacyPool
{
    private readonly Dictionary<Orientation, double> _stocks = [];

    public double this[Orientation pole] => _stocks.GetValueOrDefault(pole);

    public void Add(Orientation pole, double amount) => _stocks[pole] = this[pole] + amount;

    /// <summary>
    /// Dépense la légitimité d'un pôle. Renvoie false, sans rien dépenser, si le stock est insuffisant (un pôle en dette ne
    /// paie rien : fonder, nationaliser, vendre…).
    /// </summary>
    public bool TrySpend(Orientation pole, double amount)
    {
        if (this[pole] < amount)
            return false;

        _stocks[pole] = this[pole] - amount;
        return true;
    }

    /// <summary>Vrai si le pôle peut payer ce montant en s'endettant au plus de <paramref name="maxDebt"/> (voter à crédit).</summary>
    public bool CanSpendOnCredit(Orientation pole, double amount, double maxDebt) => this[pole] - amount >= -maxDebt - 1e-9;

    /// <summary>
    /// Dépense à crédit (voter une loi, décidé le 23 novembre 2026) : le stock peut passer en négatif, jusqu'à
    /// <paramref name="maxDebt"/> ; la légitimité gagnée ensuite rembourse la dette. Renvoie false, sans rien dépenser,
    /// au-delà du plafond.
    /// </summary>
    public bool TrySpendOnCredit(Orientation pole, double amount, double maxDebt)
    {
        if (!CanSpendOnCredit(pole, amount, maxDebt))
            return false;

        _stocks[pole] = this[pole] - amount;
        return true;
    }
}
