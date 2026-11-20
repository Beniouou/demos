using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

/// <summary>
/// L'ordre de la journée (décidé le 20 novembre 2026) : les bâtiments travaillent par étapes de la chaîne, et ceux d'une
/// même étape se partagent au prorata une matière qui manque.
/// </summary>
public class ChainOrderTests
{
    private static readonly ProductDefinition[] Products =
    [
        new("ble", "Blé", null, 0.5),
        new("farine", "Farine", null, 1),
        new("pain", "Pain", NeedCategory.Food, 1),
    ];

    private static readonly Production Moulin = new("farine", 10, [new RecipeInput("ble", 1)]);
    private static readonly Production Boulangerie = new("pain", 10, [new RecipeInput("farine", 1)]);

    private static Structure Workshop(string id, Production recipe) =>
        new(id, id, StructureKind.Building, "commune", Ownership.Cooperative, "ouvriers", recipe, Jobs: 10, DailyWage: 2);

    private static Dictionary<string, double> Run(Structure[] buildings, Market market)
    {
        var structures = new Structure[]
        {
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
        }.Concat(buildings).ToDictionary(s => s.Id);
        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 10 * buildings.Length);
        workers.SetResidence("commune", 1);
        var funds = buildings.ToDictionary(b => b.Id, _ => new Treasury());
        foreach (var fund in funds.Values)
            fund.Deposit(10_000);

        return WorkDay.Run(structures, Employment.Assign(structures, [workers]), [workers], new Treasury(), _ => market,
            companyFundOf: b => funds[b.Id]);
    }

    [Fact]
    public void Les_etapes_suivent_la_chaine()
    {
        var stages = WorkDay.ChainStages([Workshop("boulangerie", Boulangerie), Workshop("moulin", Moulin)]);

        // Le blé n'est fait par personne : le moulin est à l'étape 1, la boulangerie, qui prend sa farine, à l'étape 2.
        Assert.Equal(1, stages["moulin"]);
        Assert.Equal(2, stages["boulangerie"]);
    }

    [Fact]
    public void La_farine_du_jour_sert_le_jour_meme_quel_que_soit_l_ordre_des_batiments()
    {
        var market = new Market(Products);
        market.Offer("ble", 1_000, "ferme");

        // La boulangerie est avant le moulin dans la liste : elle travaille quand même après lui.
        var produced = Run([Workshop("boulangerie", Boulangerie), Workshop("moulin", Moulin)], market);

        Assert.Equal(100, produced["moulin"], precision: 6);
        Assert.Equal(100, produced["boulangerie"], precision: 6);
        Assert.Equal(0, market.QuantityOf("farine"), precision: 6);
    }

    [Fact]
    public void Deux_batiments_de_la_meme_etape_se_partagent_au_prorata_une_matiere_qui_manque()
    {
        var market = new Market(Products);
        market.Offer("farine", 100, "fournisseur");

        // Chacune veut 100 farines, il n'y en a que 100 : 50 chacune, et non tout pour la première de la liste.
        var produced = Run([Workshop("boulangerie-1", Boulangerie), Workshop("boulangerie-2", Boulangerie)], market);

        Assert.Equal(50, produced["boulangerie-1"], precision: 6);
        Assert.Equal(50, produced["boulangerie-2"], precision: 6);
    }

    [Fact]
    public void Ce_qu_un_batiment_ne_peut_pas_payer_revient_aux_autres_de_l_etape()
    {
        var market = new Market(Products);
        market.Offer("farine", 100, "fournisseur");
        var structures = new Structure[]
        {
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new("commune", "Commune", StructureKind.PoliticalEntity, "etat"),
            Workshop("pauvre", Boulangerie),
            Workshop("riche", Boulangerie),
        }.ToDictionary(s => s.Id);
        var workers = new SocialClass("ouvriers", "Ouvriers", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 20);
        workers.SetResidence("commune", 1);
        var funds = new Dictionary<string, Treasury> { ["pauvre"] = new(), ["riche"] = new() };
        funds["pauvre"].Deposit(10);
        funds["riche"].Deposit(10_000);

        var produced = WorkDay.Run(structures, Employment.Assign(structures, [workers]), [workers], new Treasury(), _ => market,
            companyFundOf: b => funds[b.Id]);

        // La pauvre ne paie que 10 farines (au prix 1) ; la riche prend sa part (50), puis les 40 laissées.
        Assert.Equal(10, produced["pauvre"], precision: 6);
        Assert.Equal(90, produced["riche"], precision: 6);
    }
}
