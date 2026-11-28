using Demos.Application;
using Demos.Domain.Population;
using Demos.Application.Ports;
using Demos.Domain.Economy;

/// <summary>
/// Simulation du départ d'exil (décidé le 27 novembre 2026), sans rien faire : le peuple survit-il, et combien de temps ?
/// Un relevé par mois dans la console : habitants, nourriture couverte, vivres et blé au marché, trésor, chômage, jauge.
/// La Promesse est « La Commune Libre » avec trois engagements concrets, et aucune loi de l'An I.
/// </summary>
public static class ExileReport
{
    public static void Write(IContentSource content, int years)
    {
        var promise = content.LoadPromises().Single(p => p.Id == "eg-ec-co");
        var engagements = content.LoadEngagements().Where(e => e.Id is "eg-logement" or "ec-defrichement" or "co-vente").ToList();
        var simulation = PrototypeScenario.Create(content, StartingLaws.None, new ExileStart("Bobo", promise, engagements));
        var world = simulation.World;
        var commune = PrototypeScenario.FirstCommuneId;

        Console.WriteLine("Mois      | Habitants | Nourri | Logé | Pain   | Farine | Blé    | Bois   | Matériaux | Trésor | Chômage | Jauge");
        for (var month = 0; month <= years * 12; month++)
        {
            if (month > 0)
            {
                var start = simulation.Clock.Today.Month;
                while (simulation.Clock.Today.Month == start)
                    simulation.AdvanceDay();
            }

            var market = world.MarketOf(commune);
            if (month == 1)
                foreach (var p in new[] { "pain", "farine", "ble" })
                    Console.WriteLine($"  {p} : {market.TallyOf(p)}, produit {world.ProducedTotalsOf(commune).GetValueOrDefault(p):N0}");
            var people = world.Classes.Sum(c => (double)c.Population);
            double Covered(string need) => people <= 0 ? 0
                : world.Classes.Sum(c => c.Population * (c.NeedDetails.TryGetValue(need, out var r) ? r.Coverage : 0)) / people;
            var unemployed = world.Classes.Sum(c => c.UnemployedMembers) / Math.Max(1, people);
            Console.WriteLine(
                $"{simulation.Clock.Today:yyyy-MM-dd} | {people,9:N0} | {Covered("Food"),6:P0} | {Covered("Housing"),4:P0} | "
                + $"{market.QuantityOf("pain"),6:N0} | {market.QuantityOf("farine"),6:N0} | {market.QuantityOf("ble"),6:N0} | "
                + $"{market.QuantityOf("bois"),6:N0} | {market.QuantityOf("materiaux-de-construction") + world.ConstructionReserveOf(commune).QuantityOf("materiaux-de-construction"),9:N0} | "
                + $"{world.Treasury.Balance,6:N0} | {unemployed,7:P0} | {simulation.PromiseGauge ?? 0:0.00}");
            double Shortfall(Func<NeedRecord, double> part) => people <= 0 ? 0
                : world.Classes.Sum(c => c.Population * (c.NeedDetails.TryGetValue("Food", out var r) ? part(r) : 0)) / people;
            Console.WriteLine($"           faim faute d'argent {Shortfall(r => r.MoneyShortfall):P0}, faute de pain {Shortfall(r => r.SupplyShortage):P0} ; "
                + $"outils boulangerie {world.ToolEffectOf(world.Structures["boulangerie"]):P0}, ferme {world.ToolEffectOf(world.Structures["ferme"]):P0}, emplois boulangerie {world.Classes.Sum(c => c.MembersWorkingIn("boulangerie")):N0} ; "
                + $"richesse des habitants {world.Classes.Sum(c => c.Wealth):N0}, caisse de la commune {world.CommuneFundOf(commune).Balance:N0}");
        }
    }
}
