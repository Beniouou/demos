using Demos.Application.Ports;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Application;

/// <summary>
/// Partie de départ du prototype : un État et une seule commune, née du premier camp de base, où vivent
/// 5 000 habitants, avec une école et une université d'État. Les bâtiments viennent du catalogue (buildings.json) ; leurs propriétaires sont
/// arbitraires, pour tester les trois circuits d'argent, et se changent dans le menu d'administration.
/// </summary>
public static class PrototypeScenario
{
    public const double StartingTreasury = 50_000;

    public const double StartingCoal = 6_000;

    public const string FirstCommuneId = "commune-1";

    public static Simulation Create(IContentSource content)
    {
        var types = content.LoadBuildingTypes().ToDictionary(t => t.Id);

        var structures = new List<Structure>
        {
            new("etat", "État", StructureKind.PoliticalEntity, null),
            new(FirstCommuneId, "Première commune", StructureKind.PoliticalEntity, "etat"),
            types["camp-de-base"].Build("camp-de-base-1", FirstCommuneId),
            types["carriere"].Build("carriere", FirstCommuneId, Ownership.Cooperative, "ouvriers", "Carrière coopérative"),

            // Énergie : un puits pour l'eau (1 000 par jour), une mine de charbon (100 par jour) qui chauffe tous les
            // bâtiments de départ (190 par jour en hiver, 95 au printemps et en automne, rien en été).
            types["puits"].Build("puits", FirstCommuneId, name: "Puits d'État"),
            types["mine"].BuildFor("charbon", "mine", FirstCommuneId, Ownership.Cooperative, "ouvriers", "Mine de charbon coopérative")
                with { Jobs = 200 },

            // Chaîne du pain et de l'alcool : blé → farine → pain, alcool. Les emplois couvrent à peu près ce que
            // consomment 5 000 habitants (5 000 pains et 280 alcools par jour).
            types["ferme"].BuildFor("ble", "ferme", FirstCommuneId, Ownership.Cooperative, "ouvriers", "Ferme coopérative") with { Jobs = 700 },
            types["atelier"].BuildFor("farine", "moulin", FirstCommuneId, Ownership.Cooperative, "ouvriers", "Moulin coopératif") with { Jobs = 230 },
            types["atelier"].BuildFor("pain", "boulangerie", FirstCommuneId, Ownership.Cooperative, "ouvriers", "Boulangerie coopérative") with { Jobs = 210 },
            types["atelier"].BuildFor("alcool", "distillerie", FirstCommuneId, Ownership.Private, "cadres", "Distillerie") with { Jobs = 30 },

            // Chaîne des vêtements : lin → tissus → vêtements (environ 57 par jour).
            types["ferme"].BuildFor("lin", "champ-de-lin", FirstCommuneId, Ownership.Cooperative, "ouvriers", "Champ de lin coopératif") with { Jobs = 30 },
            types["atelier"].BuildFor("tissus", "tissage", FirstCommuneId, Ownership.Private, "cadres", "Atelier de tissage") with { Jobs = 60 },
            types["atelier"].BuildFor("vetements", "atelier", FirstCommuneId, Ownership.Private, "cadres", "Atelier de couture") with { Jobs = 60 },

            // Chaîne des meubles : bois → planches → meubles (une quarantaine par jour). Le camp de bûcheron fait
            // 60 bois par jour : 40 pour les planches, le reste pour construire.
            types["camp-forestier"].Build("camp-forestier", FirstCommuneId, name: "Camp de bûcheron d'État") with { Jobs = 300 },
            types["atelier"].BuildFor("planche", "scierie", FirstCommuneId, name: "Scierie d'État") with { Jobs = 40 },
            types["atelier"].BuildFor("meubles", "menuiserie", FirstCommuneId, name: "Menuiserie d'État") with { Jobs = 40 },

            // Logements : 5 600 places pour 4 500 ouvriers (1 place), 450 professions intermédiaires (2) et 50 cadres (4).
            types["logements"].Build("logements-etat", FirstCommuneId, name: "Logements d'État")
                with { HousingPlaces = 3_100 },
            types["logements"].Build("logements-prives", FirstCommuneId, Ownership.Private, "cadres", "Logements privés")
                with { HousingPlaces = 2_200, DailyRent = 0.5 },
            types["ecole"].Build("ecole", FirstCommuneId, name: "École d'État"),
            types["universite"].Build("universite", FirstCommuneId, name: "Université d'État"),
            types["entrepot"].Build("entrepot", FirstCommuneId, name: "Entrepôt d'État"),
            types["grenier"].Build("grenier", FirstCommuneId, name: "Grenier d'État"),
        };

        // Tous les bâtiments de départ se chauffent au charbon ; le bois sert aux planches et à construire.
        structures = structures.Select(s => s.Kind == StructureKind.Building ? s with { HeatingProductId = "charbon" } : s).ToList();

        SocialClass[] classes =
        [
            new("ouvriers", "Ouvriers communistes", Rank.Workers, new Ideology(Pole.A, Pole.A, Pole.A), 4_500) { Wealth = 225_000 },
            new("intermediaires", "Professions intermédiaires libertaires", Rank.Intermediate, new Ideology(Pole.A, Pole.A, Pole.B), 450) { Wealth = 135_000 },
            new("cadres", "Cadres capitalistes", Rank.Executives, new Ideology(Pole.B, Pole.A, Pole.B), 50) { Wealth = 100_000 },
        ];
        foreach (var socialClass in classes)
            socialClass.SetResidence(FirstCommuneId, 1);

        var laws = new List<Law>
        {
            new("liberte-entreprendre", "Liberté d'entreprendre", "etat",
                [new Orientation(IdeologicalAxis.Property, Pole.B)]),
            new("impot-progressif", "Impôt progressif", FirstCommuneId,
                [new Orientation(IdeologicalAxis.Hierarchy, Pole.A), new Orientation(IdeologicalAxis.Property, Pole.A)]),
            new("quotas-extraction", "Quotas d'extraction", "carriere",
                [new Orientation(IdeologicalAxis.Production, Pole.B)]),
        };

        // Prix de départ : 30 % de bénéfice pour le bâtiment de référence de chaque produit, à plein régime.
        var catalogue = types.Values.ToList();
        var world = new World(Pricing.WithReferencePrices(content.LoadProducts(), catalogue), catalogue, structures, classes, laws);

        // Trésor de départ gonflé à la main (50 000), en attendant de régler les recettes de l'État : il permet de
        // payer la dotation d'une première nouvelle commune.
        world.Treasury.Deposit(StartingTreasury);

        // Réserve de charbon de l'État pour passer le premier hiver : la partie commence le 1er janvier, et la mine ne
        // fait que la moitié de ce qu'on brûle en hiver.
        world.MarketOf(FirstCommuneId).Offer("charbon", StartingCoal, Market.StateSellerId(FirstCommuneId));

        // Chaque entreprise privée ou coopérative part avec sa réserve (30 jours de salaires et d'énergie), prise à ses
        // propriétaires.
        foreach (var building in world.Structures.Values.Where(Companies.HasCompanyFund))
        {
            if (building.OwnerClassId is not { } ownerId || !world.ClassesById.TryGetValue(ownerId, out var owner))
                continue;

            var reserve = Math.Min(Companies.ReserveOf(building, world.PriceOf), Math.Max(0, owner.Wealth));
            owner.Wealth -= reserve;
            world.CompanyFundOf(building).Deposit(reserve);
        }

        return new Simulation(world, new GameClock(new DateOnly(1900, 1, 1)));
    }
}
