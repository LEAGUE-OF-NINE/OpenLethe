using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenLethe.Server.Auth;

/// Minimal HS256 JWT. The game client never verifies this token (it is checked
/// server-side only), so a hand-rolled HMAC is sufficient and pulls in no
/// dependency. Header is the constant {"alg":"HS256","typ":"JWT"}.
public sealed class JwtService(string secret, TimeSpan lifetime)
{
    private static readonly byte[] HeaderBytes =
        Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}""");

    private readonly byte[] _key = Encoding.UTF8.GetBytes(secret);

    public string Mint(string sub)
    {
        var exp = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var payload = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new PayloadDto { sub = sub, exp = exp }));

        var head = Base64Url.EncodeToString(HeaderBytes);
        var body = Base64Url.EncodeToString(payload);
        var signingInput = $"{head}.{body}";
        var sig = Base64Url.EncodeToString(Sign(signingInput));
        return $"{signingInput}.{sig}";
    }

    public bool TryVerify(string token, out string sub)
    {
        sub = "";
        if (string.IsNullOrEmpty(token)) return false;

        var parts = token.Split('.');
        if (parts.Length != 3) return false;

        var expectedSig = Sign($"{parts[0]}.{parts[1]}");
        byte[] actualSig;
        try { actualSig = Base64Url.DecodeFromChars(parts[2]); }
        catch { return false; }
        if (!CryptographicOperations.FixedTimeEquals(expectedSig, actualSig)) return false;

        PayloadDto? payload;
        try { payload = JsonSerializer.Deserialize<PayloadDto>(Base64Url.DecodeFromChars(parts[1])); }
        catch { return false; }
        if (payload is null || payload.sub is null) return false;
        if (payload.exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;

        sub = payload.sub;
        return true;
    }

    private byte[] Sign(string signingInput) =>
        HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(signingInput));

    private sealed class PayloadDto
    {
        public string? sub { get; set; }
        public long exp { get; set; }
    }
}
