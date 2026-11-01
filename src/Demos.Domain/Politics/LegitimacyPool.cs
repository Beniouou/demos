namespace Demos.Domain.Politics;

/// <summary>
/// Légitimité : la ressource du joueur. Elle est polaire, avec un stock par pôle idéologique
/// (EG, SE, PO, EC, PR, LI).
/// </summary>
public sealed class LegitimacyPool
{
    private readonly Dictionary<Orientation, double> _stocks = [];

    public double this[Orientation pole] => _stocks.GetValueOrDefault(pole);

    public void Add(Orientation pole, double amount) => _stocks[pole] = this[pole] + amount;

    /// <summary>Dépense la légitimité d'un pôle. Renvoie false, sans rien dépenser, si le stock est insuffisant.</summary>
    public bool TrySpend(Orientation pole, double amount)
    {
        if (this[pole] < amount)
            return false;

        _stocks[pole] = this[pole] - amount;
        return true;
    }
}
