namespace Demos.Domain.Economy;

/// <summary>
/// Ressources financières : l'argent de l'État, alimenté par les impôts (et, sur le même modèle, la caisse d'une commune ou
/// d'une entreprise). Une enveloppe du budget (<see cref="BudgetEnvelope"/>) en est une vue plafonnée.
/// </summary>
public class Treasury
{
    private double _balance;

    public virtual double Balance => _balance;

    public virtual void Deposit(double amount) => _balance += amount;

    /// <summary>Dépense une somme. Renvoie false, sans rien dépenser, si le solde est insuffisant.</summary>
    public virtual bool TrySpend(double amount)
    {
        if (_balance < amount)
            return false;

        _balance -= amount;
        return true;
    }
}
