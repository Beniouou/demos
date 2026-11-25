using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Domain.Economy;

/// <summary>Un poste du budget de l'État (décidé le 25 novembre 2026) : ce que le trésor paie, dans la limite de son enveloppe.</summary>
public enum BudgetItem
{
    /// <summary>
    /// Les dotations aux communes : un montant par habitant et par an (décidé le 25 novembre 2026), versé chaque mois à
    /// chaque commune selon ses habitants du moment ; le poste suit donc la croissance des communes.
    /// </summary>
    Grants,

    /// <summary>Les matériaux achetés pour construire (chantiers, camp de base, siège de préfecture).</summary>
    Construction,

    /// <summary>La construction et l'entretien des liaisons payées par l'État.</summary>
    Roads,

    /// <summary>La dotation de fondation d'une commune et le rachat d'une nationalisation.</summary>
    Investment,
}

public static class BudgetItems
{
    public static IReadOnlyList<BudgetItem> All { get; } = Enum.GetValues<BudgetItem>();

    /// <summary>Vrai pour un poste voté par habitant et par an (les dotations), et non en enveloppe fixe.</summary>
    public static bool IsPerResident(BudgetItem item) => item == BudgetItem.Grants;

    /// <summary>Le pôle d'un poste : l'augmenter coûte de la légitimité de ce pôle.</summary>
    public static Orientation PoleOf(BudgetItem item) => item switch
    {
        BudgetItem.Construction or BudgetItem.Roads => new Orientation(IdeologicalAxis.Production, Pole.A),
        _ => new Orientation(IdeologicalAxis.Property, Pole.A),
    };
}

/// <summary>Un budget : l'enveloppe annuelle de chaque poste (pour les dotations, le montant par habitant et par an).</summary>
public sealed class BudgetPlan
{
    private readonly Dictionary<BudgetItem, double> _envelopes;

    public BudgetPlan(IReadOnlyDictionary<BudgetItem, double> envelopes) =>
        _envelopes = BudgetItems.All.ToDictionary(i => i, i => Math.Max(0, envelopes.GetValueOrDefault(i)));

    public double this[BudgetItem item] => _envelopes[item];

    /// <summary>Le même budget avec une autre enveloppe pour ce poste (jamais négative).</summary>
    public BudgetPlan With(BudgetItem item, double amount) =>
        new(new Dictionary<BudgetItem, double>(_envelopes) { [item] = amount });

    /// <summary>Le budget de départ, celui des réglages.</summary>
    public static BudgetPlan Initial(BalanceSettings settings) => new(new Dictionary<BudgetItem, double>
    {
        [BudgetItem.Grants] = settings.BudgetGrantPerResident,
        [BudgetItem.Construction] = settings.BudgetConstruction,
        [BudgetItem.Roads] = settings.BudgetRoads,
        [BudgetItem.Investment] = settings.BudgetInvestment,
    });

    public bool SameAs(BudgetPlan other) => BudgetItems.All.All(i => Math.Abs(this[i] - other[i]) < 0.005);
}

/// <summary>Un budget voté, qui attend le 1er du mois pour entrer en vigueur, et la légitimité payée pour lui.</summary>
public sealed record VotedBudget(BudgetPlan Plan, IReadOnlyDictionary<Orientation, double> Paid);

/// <summary>
/// Le budget de l'État (décidé le 25 novembre 2026) : le budget en vigueur, ce que chaque poste a dépensé depuis le
/// 1er janvier, le brouillon du prochain budget et le budget voté qui attend le 1er du mois.
/// </summary>
public sealed class StateBudget
{
    private readonly Dictionary<BudgetItem, double> _spent = BudgetItems.All.ToDictionary(i => i, _ => 0.0);

    public StateBudget(BudgetPlan initial)
    {
        Current = initial;
        Draft = initial;
    }

    public BudgetPlan Current { get; private set; }

    /// <summary>Le prochain budget en préparation (au départ, une copie du budget en vigueur).</summary>
    public BudgetPlan Draft { get; set; }

    public VotedBudget? Voted { get; private set; }

    public double SpentOn(BudgetItem item) => _spent[item];

    /// <summary>
    /// Ce qui reste à dépenser cette année sur ce poste. Les dotations n'ont pas d'enveloppe fixe : elles suivent les
    /// habitants, et seul le trésor les limite.
    /// </summary>
    public double Available(BudgetItem item) =>
        BudgetItems.IsPerResident(item) ? double.PositiveInfinity : Math.Max(0, Current[item] - _spent[item]);

    /// <summary>Note une dépense du poste (négative : de l'argent rendu, par exemple une fondation qui échoue).</summary>
    public void Record(BudgetItem item, double amount) => _spent[item] = Math.Max(0, _spent[item] + amount);

    /// <summary>Le jour depuis lequel les dépenses de l'année sont comptées (le 1er janvier, ou le début de la partie).</summary>
    public DateOnly? CountedSince { get; private set; }

    /// <summary>Le dernier mois (année × 12 + mois) dont les dotations ont été versées.</summary>
    public int? GrantsPaidFor { get; set; }

    /// <summary>Le 1er janvier : les enveloppes repartent entières (le budget en vigueur est reconduit).</summary>
    public void StartYear(DateOnly date)
    {
        foreach (var item in BudgetItems.All)
            _spent[item] = 0;
        CountedSince = date;
    }

    /// <summary>Le début de la partie : les dépenses se comptent à partir de ce jour.</summary>
    public void StartCountingOn(DateOnly date) => CountedSince ??= date;

    public void MarkVoted(BudgetPlan plan, IReadOnlyDictionary<Orientation, double> paid) => Voted = new VotedBudget(plan, paid);

    /// <summary>Annule le vote en attente et le renvoie (pour rendre la légitimité), ou null s'il n'y en a pas.</summary>
    public VotedBudget? Unvote()
    {
        var voted = Voted;
        Voted = null;
        return voted;
    }

    /// <summary>Le 1er du mois : le budget voté entre en vigueur ; ce qui a déjà été dépensé cette année reste compté.</summary>
    public BudgetPlan? EnactVoted()
    {
        if (Voted is not { } voted)
            return null;

        Current = voted.Plan;
        Voted = null;
        return Current;
    }

    /// <summary>
    /// Le coût d'un budget par rapport à celui en vigueur (décidé le 25 novembre 2026) : chaque poste qui augmente coûte,
    /// dans son pôle, un coût de base plus une part de l'augmentation ; baisser ou reconduire ne coûte rien. Pour les
    /// dotations, l'augmentation est celle du montant par habitant × les habitants du pays.
    /// </summary>
    public static Dictionary<Orientation, double> CostOf(BudgetPlan current, BudgetPlan proposed, BalanceSettings settings, double residents)
    {
        var cost = new Dictionary<Orientation, double>();
        foreach (var item in BudgetItems.All)
        {
            var increase = (proposed[item] - current[item]) * (BudgetItems.IsPerResident(item) ? residents : 1);
            if (increase < 0.5)
                continue;
            var pole = BudgetItems.PoleOf(item);
            cost[pole] = cost.GetValueOrDefault(pole) + settings.BudgetVoteBaseCost + increase / 1000 * settings.BudgetVoteCostPerThousand;
        }
        return cost;
    }
}

/// <summary>
/// L'enveloppe d'un poste, vue comme une caisse : on n'y dépense que ce qui reste à la fois au poste et au trésor. Une
/// dépense est prise au trésor et notée au poste ; un dépôt est de l'argent rendu (il revient au trésor et au poste).
/// </summary>
public sealed class BudgetEnvelope(Treasury treasury, StateBudget budget, BudgetItem item) : Treasury
{
    public BudgetItem Item => item;

    public override double Balance => Math.Min(treasury.Balance, budget.Available(item));

    public override void Deposit(double amount)
    {
        treasury.Deposit(amount);
        budget.Record(item, -amount);
    }

    public override bool TrySpend(double amount)
    {
        if (amount > Balance + 1e-9)
            return false;

        treasury.TrySpend(Math.Min(amount, treasury.Balance));
        budget.Record(item, amount);
        return true;
    }
}
