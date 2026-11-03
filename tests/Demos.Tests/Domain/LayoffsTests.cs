using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Tests.Domain;

public class LayoffsTests
{
    private static readonly ProductDefinition[] Products = [new("pierre", "Pierre", null, 1)];

    private static Structure Quarry(Ownership ownership, int jobs = 100, int? capacity = null, double wageCut = 0, bool setByPlayer = false) =>
        new("carriere", "Carrière", StructureKind.Building, "commune", ownership, ownership == Ownership.Public ? null : "ouvriers",
            new Production("pierre", 1), Jobs: jobs, DailyWage: 2, Capacity: capacity, JobsSetByPlayer: setByPlayer, WageCut: wageCut);

    private static (Dictionary<string, Structure> Structures, Market Market) Town(Structure quarry, double unsold)
    {
        var structures = new Structure[]
        {
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
            quarry,
        }.ToDictionary(s => s.Id);
        var market = new Market(Products);
        var seller = quarry.Ownership == Ownership.Public ? Market.StateSellerId("commune") : Market.CompanySellerId(quarry.Id);
        if (unsold > 0)
            market.Offer("pierre", unsold, seller);
        return (structures, market);
    }

    private static Structure? RunMonth(Structure quarry, double unsold, WageLedger? wages = null, Treasury? fund = null)
    {
        var (structures, market) = Town(quarry, unsold);
        return Layoffs.RunMonthly(structures, _ => market, wages ?? new WageLedger(), fund is null ? null : _ => fund).SingleOrDefault();
    }

    [Theory]
    [InlineData(Ownership.Private, 85)]
    [InlineData(Ownership.Public, 98)]
    public void Une_entreprise_qui_ne_vend_pas_licencie_selon_son_statut(Ownership ownership, int jobsLeft)
    {
        // 100 postes, 1 pierre par travailleur : 3 100 invendus, c'est 31 jours de production.
        var after = RunMonth(Quarry(ownership), unsold: 3_100);

        Assert.Equal(jobsLeft, after!.Jobs);
        Assert.Equal(100, after.JobCapacity);
    }

    [Fact]
    public void Un_batiment_qui_garde_ses_postes_ne_licencie_pas_meme_sans_vendre()
    {
        var (structures, market) = Town(Quarry(Ownership.Public), unsold: 3_100);

        var changed = Layoffs.RunMonthly(structures, _ => market, new WageLedger(), keepsJobs: b => b.Id == "carriere");

        Assert.Empty(changed);
    }

    [Fact]
    public void Un_petit_batiment_public_perd_au_moins_un_poste()
    {
        var after = RunMonth(Quarry(Ownership.Public, jobs: 20), unsold: 1_000);

        Assert.Equal(19, after!.Jobs);
    }

    [Fact]
    public void Une_scop_baisse_d_abord_ses_salaires_puis_licencie()
    {
        var quarry = Quarry(Ownership.Cooperative);
        for (var month = 1; month <= 4; month++)
        {
            quarry = RunMonth(quarry, unsold: 3_100)!;
            Assert.Equal(100, quarry.Jobs);
            Assert.Equal(0.05 * month, quarry.WageCut, precision: 9);
        }

        // Ses salaires ont baissé de 20 % : elle licencie maintenant 5 % de sa capacité.
        quarry = RunMonth(quarry, unsold: 3_100)!;
        Assert.Equal(95, quarry.Jobs);
        Assert.Equal(0.2, quarry.WageCut, precision: 9);
        Assert.Equal(1.6, Staffing.WageOf(quarry, Demos.Domain.Population.Rank.Workers), precision: 9);
    }

    [Fact]
    public void Une_entreprise_qui_ne_paie_plus_ses_salaires_licencie_meme_sans_invendus()
    {
        var wages = new WageLedger();
        wages.Record("carriere", due: 100, paid: 50);

        var after = RunMonth(Quarry(Ownership.Private), unsold: 0, wages);

        Assert.Equal(85, after!.Jobs);
    }

    [Fact]
    public void Une_entreprise_qui_vend_tout_reembauche_sans_depasser_sa_capacite()
    {
        var after = RunMonth(Quarry(Ownership.Private, jobs: 90, capacity: 100), unsold: 0);

        Assert.Equal(100, after!.Jobs);
        Assert.Null(RunMonth(after, unsold: 0));
    }

    [Fact]
    public void Une_scop_qui_va_mieux_remonte_ses_salaires_avant_de_reembaucher()
    {
        var after = RunMonth(Quarry(Ownership.Cooperative, jobs: 90, capacity: 100, wageCut: 0.2), unsold: 0);

        Assert.Equal(90, after!.Jobs);
        Assert.Equal(0.15, after.WageCut, precision: 9);
    }

    [Fact]
    public void Entre_les_deux_seuils_rien_ne_bouge()
    {
        // 20 jours d'invendus : ni assez pour licencier (30), ni assez peu pour réembaucher (10).
        Assert.Null(RunMonth(Quarry(Ownership.Private, jobs: 90, capacity: 100), unsold: 1_800));
    }

    [Fact]
    public void Des_postes_fixes_par_le_joueur_ne_bougent_pas()
    {
        Assert.Null(RunMonth(Quarry(Ownership.Public, setByPlayer: true), unsold: 10_000));
    }

    [Fact]
    public void Une_entreprise_fermee_rouvre_des_postes_quand_ses_invendus_sont_ecoules()
    {
        var wages = new WageLedger();
        wages.Record("carriere", due: 100, paid: 0);

        var after = RunMonth(Quarry(Ownership.Private, jobs: 0, capacity: 100), unsold: 0, wages);

        Assert.Equal(15, after!.Jobs);
    }

    [Fact]
    public void Une_entreprise_sans_argent_est_en_faillite_et_ne_rouvre_pas()
    {
        Assert.Null(RunMonth(Quarry(Ownership.Private, jobs: 0, capacity: 100), unsold: 0, fund: new Treasury()));
    }

    [Fact]
    public void Une_entreprise_ne_rouvre_que_les_postes_que_sa_caisse_peut_payer_un_mois()
    {
        // Un poste coûte 2,48 par jour (salaire 2, encadrement et direction) : 30 jours, c'est 74,4. 500 en caisse en paient 6.
        var fund = new Treasury();
        fund.Deposit(500);

        var after = RunMonth(Quarry(Ownership.Private, jobs: 50, capacity: 100), unsold: 0, fund: fund);

        Assert.Equal(56, after!.Jobs);
    }

    [Fact]
    public void Un_batiment_public_rouvre_sans_regarder_de_caisse()
    {
        var after = RunMonth(Quarry(Ownership.Public, jobs: 50, capacity: 100), unsold: 0, fund: new Treasury());

        Assert.Equal(52, after!.Jobs);
    }
}
