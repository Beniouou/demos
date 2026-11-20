using Demos.Application;
using Demos.Domain.Politics;

namespace Demos.Tests.Application;

/// <summary>La vue de l'interface ne fait que lire : elle ne crée rien et ne donne que des copies.</summary>
public class GameViewTests
{
    [Fact]
    public void Lire_une_commune_sans_marche_ne_cree_ni_marche_ni_caisse()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        simulation.World.AddStructure(new Structure("commune-vide", "Commune vide", StructureKind.PoliticalEntity, PrototypeScenario.StateId));
        var view = new GameView(simulation);

        var commune = view.Communes().Single(c => c.Id == "commune-vide");

        Assert.False(simulation.World.Markets.ContainsKey("commune-vide"));
        Assert.Equal(0, commune.Fund);
        Assert.Empty(commune.Stock);
        Assert.Empty(commune.StateStock);
    }

    [Fact]
    public void Deux_communes_n_echangent_que_si_l_une_a_un_batiment_logistique()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        simulation.World.AddStructure(new Structure("vide-1", "Vide 1", StructureKind.PoliticalEntity, PrototypeScenario.StateId));
        simulation.World.AddStructure(new Structure("vide-2", "Vide 2", StructureKind.PoliticalEntity, PrototypeScenario.StateId));
        var view = new GameView(simulation);

        // La première commune a un entrepôt et un grenier ; les deux communes vides n'ont rien.
        Assert.True(simulation.World.HasLogisticsBuilding(PrototypeScenario.FirstCommuneId));
        Assert.True(view.CanTrade(PrototypeScenario.FirstCommuneId, "vide-1"));
        Assert.True(view.CanTrade("vide-1", "vide-1"));
        Assert.False(view.CanTrade("vide-1", "vide-2"));
    }

    [Fact]
    public void La_vue_montre_la_capacite_d_envoi_et_les_chargements_en_route()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var view = new GameView(simulation);
        var first = PrototypeScenario.FirstCommuneId;
        simulation.World.ShipmentsInTransit.Add(new Demos.Domain.Economy.Shipment(first, "ailleurs", "pain", "etat", 90, DaysLeft: 2, Days: 3));

        // Un grenier et un entrepôt : 2 × 500 par jour.
        Assert.Equal(1_000, view.ShippingCapacityOf(first).PerDay);
        var shipment = Assert.Single(view.ShipmentsFrom(first));
        Assert.Equal(90, shipment.Quantity);
        Assert.Equal(view.Today.AddDays(-1), shipment.DepartedOn);
        Assert.Equal(view.Today.AddDays(2), shipment.ArrivesOn);
        Assert.Equal(90, view.ProductFlows().Single(f => f.CommuneId == first && f.ProductId == "pain").InTransit);
    }

    [Fact]
    public void Les_totaux_des_produits_suivent_la_production_et_ce_qui_sort_du_stockage()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var view = new GameView(simulation);

        for (var day = 0; day < 10; day++)
            simulation.AdvanceDay();

        var flows = view.ProductFlows().Where(f => f.CommuneId == PrototypeScenario.FirstCommuneId).ToList();
        // La production comptée est celle des bâtiments de la commune, jour après jour.
        Assert.Contains(flows, f => f.Produced > 0);
        // Les habitants achètent de quoi se nourrir : ça sort du stockage comme utilisé.
        Assert.Contains(flows, f => f.Used > 0);
        Assert.All(flows, f => Assert.True(f.Produced >= 0 && f.Used >= 0 && f.Sent >= 0 && f.Lost >= 0 && f.Stock >= 0));
    }

    [Fact]
    public void La_chaine_de_production_donne_les_batiments_a_recette_de_la_commune_et_leurs_matieres()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var view = new GameView(simulation);
        simulation.AdvanceDay();

        var chain = view.ProductionChain(PrototypeScenario.FirstCommuneId);

        Assert.NotEmpty(chain);
        // Le moulin de la partie de départ fait de la farine avec du blé, que la ferme de la commune produit.
        var mill = chain.Single(b => b.ProductId == "farine");
        Assert.Contains(mill.Inputs, i => i.ProductId == "ble" && i.NeededPerDay > 0);
        Assert.Contains(chain, b => b.ProductId == "ble");
        Assert.All(chain, b => Assert.InRange(b.SupplyShare, 0, 1));
        Assert.Empty(view.ProductionChain("commune-inconnue"));
        Assert.False(simulation.World.Markets.ContainsKey("commune-inconnue"));
    }

    [Fact]
    public void Les_flux_d_un_batiment_disent_d_ou_viennent_ses_matieres_et_ou_part_sa_production()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var view = new GameView(simulation);
        simulation.AdvanceDay();
        var mill = view.ProductionChain(PrototypeScenario.FirstCommuneId).Single(b => b.ProductId == "farine");

        var flows = view.BuildingFlows(mill.Id)!;

        // Le blé vient de la ferme de la commune ; la farine part aux ateliers qui l'utilisent (pain, alcool).
        Assert.Contains(flows.Sources, s => s.Kind == FlowKind.Market && s.ProductName == "Blé" && s.Relation == PlaceRelation.SameCommune);
        Assert.True(flows.Destinations.Count(d => d.Kind == FlowKind.Market) >= 2);
        Assert.True(flows.HasMaterials);
        Assert.Null(view.BuildingFlows(PrototypeScenario.FirstCommuneId));
    }

    [Fact]
    public void Un_fournisseur_d_une_autre_commune_est_marque_comme_tel()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var world = simulation.World;
        world.AddStructure(new Structure("commune-b", "Commune B", StructureKind.PoliticalEntity, PrototypeScenario.StateId));
        var sawmill = world.Structures.Values.Single(b => b.Ownership == Ownership.Public && b.Production?.ProductId == "planche");
        world.AddStructure(sawmill with { Id = "scierie-b", ParentId = "commune-b" });
        var camp = world.Structures.Values.Single(b => b.Ownership == Ownership.Public && b.Production?.ProductId == "bois");
        Assert.Null(simulation.TryAddSupplyLine(camp.Id, "scierie-b").Problem);
        var view = new GameView(simulation);

        var source = view.BuildingFlows("scierie-b")!.Sources.Single(s => s.Kind == FlowKind.Line);
        var destination = view.BuildingFlows(camp.Id)!.Destinations.Single(d => d.Kind == FlowKind.Line);

        // Les deux communes sont directement sous l'État : même « préfecture », autre commune.
        Assert.Equal(PlaceRelation.OtherCommune, source.Relation);
        Assert.Equal(PlaceRelation.OtherCommune, destination.Relation);
        Assert.Equal(PlaceRelation.SameCommune, view.RelationBetween("commune-b", "commune-b"));
    }

    [Fact]
    public void La_vue_est_une_copie_que_la_suite_de_la_partie_ne_change_pas()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var view = new GameView(simulation);
        var before = view.Communes().Single(c => c.Id == PrototypeScenario.FirstCommuneId);
        var wealthBefore = before.Classes[0].Wealth;
        var stockBefore = before.Stock.ToDictionary();

        for (var day = 0; day < 10; day++)
            simulation.AdvanceDay();

        Assert.Equal(wealthBefore, before.Classes[0].Wealth);
        Assert.Equal(stockBefore, before.Stock);
        Assert.NotEqual(before, view.Communes().Single(c => c.Id == PrototypeScenario.FirstCommuneId));
    }

    [Fact]
    public void Les_reglages_de_l_administration_passent_par_la_simulation()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());

        simulation.SetStateTaxRate(0.01);
        simulation.SetCommuneTaxRate(PrototypeScenario.FirstCommuneId, 0.02);
        simulation.SetPrice("pierre", 3);

        Assert.Equal(0.01, simulation.World.StateTaxRate);
        Assert.Equal(0.02, simulation.World.CommuneTaxRateOf(PrototypeScenario.FirstCommuneId));
        Assert.Equal(3, simulation.World.PriceOf("pierre"));
    }

    [Fact]
    public void Chaque_classe_montre_son_impot_selon_les_lois_et_ce_qu_elle_a_paye_cette_saison()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var view = new GameView(simulation);
        for (var day = 0; day < 10; day++)
            simulation.AdvanceDay();

        var commune = view.Communes().Single(c => c.Id == PrototypeScenario.FirstCommuneId);
        foreach (var socialClass in commune.Classes)
        {
            var engine = simulation.World.ClassesById[socialClass.Id];
            Assert.Equal(engine.Rank, socialClass.Rank);
            Assert.Equal(simulation.World.LawBook.TaxFactorOf(engine), socialClass.TaxFactor);
            Assert.Equal(engine.SeasonTaxes, socialClass.SeasonTaxes);
        }
        Assert.Contains(commune.Classes, c => c.SeasonTaxes > 0);
    }

    [Fact]
    public void Les_batiments_d_extraction_de_depart_sont_poses_sur_leur_zone_a_portee_de_la_commune()
    {
        var world = PrototypeScenario.Create(GameData.Content()).World;
        var capital = world.CommunePositions[PrototypeScenario.FirstCommuneId];

        foreach (var id in new[] { "camp-forestier", "ferme", "champ-de-lin", "carriere", "mine" })
        {
            var building = world.Structures[id];
            var position = Assert.Contains(id, world.BuildingPositions);
            Assert.Null(world.ExtractionSiteProblem(building.TypeId, building.Production?.ProductId, PrototypeScenario.FirstCommuneId, position));
            Assert.True(Demos.Domain.Map.MapScale.KmBetween(position, capital) <= Demos.Domain.Economy.Balance.Current.ExtractionMaxDistance);
        }
    }

    [Fact]
    public void Un_batiment_d_extraction_ne_se_construit_que_sur_sa_zone_a_portee()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var world = simulation.World;
        var type = world.BuildingTypes["camp-forestier"];
        var forest = world.BuildingPositions["camp-forestier"];
        var capital = world.CommunePositions[PrototypeScenario.FirstCommuneId];

        Assert.False(simulation.TryBuild(type.Build("bucheron-2", PrototypeScenario.FirstCommuneId), Demos.Domain.Economy.PaymentMode.StockFirst));
        Assert.False(simulation.TryBuild(type.Build("bucheron-2", PrototypeScenario.FirstCommuneId), Demos.Domain.Economy.PaymentMode.StockFirst,
            new Demos.Domain.Map.Point2D(capital.X + 1000, capital.Y)));
        Assert.True(simulation.TryBuild(type.Build("bucheron-2", PrototypeScenario.FirstCommuneId), Demos.Domain.Economy.PaymentMode.StockFirst, forest));
        Assert.Equal(forest, world.BuildingPositions["bucheron-2"]);
    }

    [Fact]
    public void Un_chantier_lance_apparait_dans_la_vue_avec_sa_commune_et_ses_jours()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var view = new GameView(simulation);
        var type = simulation.World.BuildingTypes["logements"];

        Assert.True(simulation.TryBuild(type.Build("logements-neufs", PrototypeScenario.FirstCommuneId), Demos.Domain.Economy.PaymentMode.StockFirst));

        var site = Assert.Single(view.ConstructionSites());
        Assert.Equal(("logements-neufs", "logements", PrototypeScenario.FirstCommuneId, type.Construction!.Days),
            (site.BuildingId, site.TypeId, site.CommuneId, site.DaysLeft));
    }

    [Fact]
    public void La_capitale_est_le_chef_lieu_de_l_Etat_la_premiere_commune_de_la_partie()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());

        Assert.Equal(PrototypeScenario.FirstCommuneId, simulation.World.CapitalId);
        Assert.Equal(PrototypeScenario.FirstCommuneId, new GameView(simulation).CapitalId);
    }

    [Fact]
    public void Le_code_civil_liste_toutes_les_lois_en_vigueur_et_le_catalogue_ses_briques()
    {
        var simulation = PrototypeScenario.Create(GameData.Content());
        var view = new GameView(simulation);

        Assert.Equal(simulation.World.Laws.Select(l => l.Id), view.Laws().Select(l => l.Id));
        Assert.Equal(simulation.World.LawCatalogue.Bricks.Count, view.LawBricks().Count);
    }

    [Fact]
    public void L_arbre_des_structures_part_de_l_Etat_et_porte_les_lois_de_chaque_niveau()
    {
        var view = new GameView(WithLawAtEachLevel());

        var state = Assert.Single(view.StructureTree());
        Assert.Equal(StructureNodeKind.State, state.Kind);
        Assert.Contains(state.Laws, l => l.Id == "loi-etat");

        var commune = Assert.Single(state.Children, c => c.Id == PrototypeScenario.FirstCommuneId);
        Assert.Equal(StructureNodeKind.Commune, commune.Kind);
        var quarry = Assert.Single(commune.Children, c => c.Id == "carriere");
        Assert.Equal(StructureNodeKind.Building, quarry.Kind);
        Assert.Equal("Extraction", quarry.Category);
        Assert.Contains(quarry.Laws, l => l.Id == "loi-carriere");
    }

    [Fact]
    public void Une_structure_subit_les_lois_de_tout_ce_qui_la_contient_de_l_Etat_a_elle_meme()
    {
        var view = new GameView(WithLawAtEachLevel());

        var laws = view.LawsApplyingTo("carriere").Select(l => l.Id).ToList();

        Assert.Equal(["loi-etat", "loi-commune", "loi-carriere"], laws);
        Assert.DoesNotContain("loi-carriere", view.LawsApplyingTo(PrototypeScenario.FirstCommuneId).Select(l => l.Id));
    }

    /// <summary>Une partie sans loi de départ, avec une loi à l'État, à la commune et à la carrière.</summary>
    private static Simulation WithLawAtEachLevel()
    {
        var simulation = PrototypeScenario.Create(GameData.Content(), StartingLaws.None);
        Law Production(string id, string level, double value) =>
            new(id, id, "environnement", level, [new BrickSetting("production", LawSubject.Everyone, value)]);
        simulation.World.AddLaw(Production("loi-etat", PrototypeScenario.StateId, 0.1));
        simulation.World.AddLaw(Production("loi-commune", PrototypeScenario.FirstCommuneId, 0.1));
        simulation.World.AddLaw(Production("loi-carriere", "carriere", -0.2));
        return simulation;
    }
}
