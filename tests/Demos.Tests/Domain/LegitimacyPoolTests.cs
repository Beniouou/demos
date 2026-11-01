using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Tests.Domain;

public class LegitimacyPoolTests
{
    private static readonly Orientation Ecologist = new(IdeologicalAxis.Production, Pole.B);

    [Fact]
    public void On_depense_la_legitimite_d_un_pole_si_le_stock_suffit()
    {
        var pool = new LegitimacyPool();
        pool.Add(Ecologist, 10);

        Assert.True(pool.TrySpend(Ecologist, 4));
        Assert.Equal(6, pool[Ecologist]);
    }

    [Fact]
    public void Sans_stock_suffisant_rien_n_est_depense()
    {
        var pool = new LegitimacyPool();
        pool.Add(Ecologist, 3);

        Assert.False(pool.TrySpend(Ecologist, 4));
        Assert.Equal(3, pool[Ecologist]);
    }

    [Fact]
    public void Les_codes_des_poles_suivent_le_document()
    {
        Assert.Equal(["EG", "SE", "PO", "EC", "PR", "LI"], Orientation.All.Select(o => o.Code));
    }
}
