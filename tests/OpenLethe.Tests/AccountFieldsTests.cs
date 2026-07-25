using OpenLethe.Server;
using OpenLethe.Server.Wire;
using Xunit;

public class AccountFieldsTests
{
    [Fact]
    public void Get_NeutralDefaults_ReturnDefault()
    {
        Assert.Null(AccountFields.Get<List<Ego>>("{}"));
        Assert.Null(AccountFields.Get<List<Ego>>("[]"));
        Assert.Null(AccountFields.Get<List<Ego>>(""));
        Assert.Null(AccountFields.Get<List<Ego>>(null));
    }

    [Fact]
    public void SetThenGet_RoundTrips()
    {
        var egos = new List<Ego> { new() { ego_id = 7, gacksung = 4, acquire_time = "t" } };

        var json = AccountFields.Set(egos);
        var back = AccountFields.Get<List<Ego>>(json);

        Assert.NotNull(back);
        Assert.Single(back!);
        Assert.Equal(7, back![0].ego_id);
    }

    [Fact]
    public void MergeById_ReplacesMatching_AppendsNew()
    {
        var existing = new List<Ego>
        {
            new() { ego_id = 1, gacksung = 1 },
            new() { ego_id = 2, gacksung = 1 },
        };
        var incoming = new List<Ego>
        {
            new() { ego_id = 2, gacksung = 9 }, // replace
            new() { ego_id = 3, gacksung = 4 }, // append
        };

        var merged = AccountFields.MergeById(existing, incoming, e => e.ego_id);

        Assert.Equal(3, merged.Count);
        Assert.Equal(9, merged.Single(e => e.ego_id == 2).gacksung);
        Assert.Contains(merged, e => e.ego_id == 3);
    }
}
