namespace Demos.Domain.Economy;

/// <summary>Comment l'État reprend un bâtiment privé ou coopératif.</summary>
public enum NationalizationMode
{
    /// <summary>Rachat : l'État paie le propriétaire.</summary>
    Buyout,

    /// <summary>Expropriation : l'État ne paie rien, mais elle coûte beaucoup plus de légitimité.</summary>
    Expropriation,
}

/// <summary>Résultat d'une tentative de nationalisation.</summary>
public enum NationalizationOutcome
{
    Done,
    NotPrivate,
    TreasuryTooPoor,
    NotEnoughLegitimacy,
}

/// <summary>
/// Nationalisation : l'État reprend un bâtiment privé ou coopératif, sans que le propriétaire puisse refuser. Une
/// entreprise en difficulté (qui ne paie plus ses salaires) se rachète presque pour rien et coûte peu de légitimité
/// collectiviste (CO) ; une entreprise qui marche se rachète cher, en argent et en légitimité. L'expropriation ne
/// coûte rien en argent mais beaucoup en légitimité. Toutes les valeurs sont des propositions.
/// </summary>
public static class Nationalization
{
    /// <summary>Une entreprise en difficulté se rachète pour 30 jours de son bénéfice attendu…</summary>
    public const int StrugglingProfitDays = 30;

    /// <summary>… une entreprise qui marche pour un an et demi.</summary>
    public const int ThrivingProfitDays = 547;

    /// <summary>Prix plancher d'un rachat.</summary>
    public const double MinimumPrice = 1_000;

    // Coûts de base en légitimité CO, pour un bâtiment de référence (réglages ; la simulation les fait suivre les emplois).
    public static double StrugglingLegitimacyCost => Balance.Current.NationalizationStrugglingLegitimacyCost;
    public static double ThrivingLegitimacyCost => Balance.Current.NationalizationThrivingLegitimacyCost;
    public static double ExpropriationLegitimacyCost => Balance.Current.ExpropriationLegitimacyCost;

    public static double PriceOf(NationalizationMode mode, bool struggling, double dailyProfit) => mode switch
    {
        NationalizationMode.Expropriation => 0,
        _ => Math.Max(MinimumPrice, dailyProfit * (struggling ? StrugglingProfitDays : ThrivingProfitDays)),
    };

    public static double LegitimacyCostOf(NationalizationMode mode, bool struggling) => mode switch
    {
        NationalizationMode.Expropriation => ExpropriationLegitimacyCost,
        _ => struggling ? StrugglingLegitimacyCost : ThrivingLegitimacyCost,
    };
}
