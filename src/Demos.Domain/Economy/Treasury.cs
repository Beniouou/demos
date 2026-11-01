namespace Demos.Domain.Economy;

/// <summary>Ressources financières : l'argent de l'État, alimenté par les impôts.</summary>
public sealed class Treasury
{
    public double Balance { get; private set; }

    public void Deposit(double amount) => Balance += amount;

    /// <summary>Dépense une somme. Renvoie false, sans rien dépenser, si le solde est insuffisant.</summary>
    public bool TrySpend(double amount)
    {
        if (Balance < amount)
            return false;

        Balance -= amount;
        return true;
    }
}
