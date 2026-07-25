using OpenLethe.Server.Auth;

public class JwtServiceTests
{
    private static JwtService Svc(TimeSpan? life = null) =>
        new("test-secret-test-secret-test-secret-0123456789", life ?? TimeSpan.FromHours(1));

    [Fact]
    public void MintThenVerify_RoundTripsSubject()
    {
        var token = Svc().Mint("alice");
        Assert.True(Svc().TryVerify(token, out var sub));
        Assert.Equal("alice", sub);
    }

    [Fact]
    public void Verify_RejectsExpiredToken()
    {
        var expired = Svc(TimeSpan.FromSeconds(-1)).Mint("bob");
        Assert.False(Svc().TryVerify(expired, out _));
    }

    [Fact]
    public void Verify_RejectsTamperedSignature()
    {
        var token = Svc().Mint("carol");
        var tampered = token[..^2] + (token[^1] == 'A' ? "BB" : "AA");
        Assert.False(Svc().TryVerify(tampered, out _));
    }

    [Fact]
    public void Verify_RejectsDifferentSecret()
    {
        var token = Svc().Mint("dave");
        var other = new JwtService("a-completely-different-secret-value-9876543210", TimeSpan.FromHours(1));
        Assert.False(other.TryVerify(token, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not.a.jwt")]
    [InlineData("only-one-part")]
    public void Verify_RejectsMalformed(string token)
    {
        Assert.False(Svc().TryVerify(token, out _));
    }
}
