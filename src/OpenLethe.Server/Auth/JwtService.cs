using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenLethe.Server.Auth;

/// Minimal HS256 JWT. The game client never verifies this token (it is checked
/// server-side only), so a hand-rolled HMAC is sufficient and pulls in no
/// dependency. Header is the constant {"alg":"HS256","typ":"JWT"}.
public sealed class JwtService(string secret, TimeSpan lifetime)
{
    private static readonly byte[] HeaderBytes =
        Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}""");

    private readonly byte[] _key = Encoding.UTF8.GetBytes(secret);

    public string Mint(string sub) => Mint(new PayloadDto { sub = sub }, lifetime);

    /// Short-lived token for the dashboard (Rust create_ephemeral_jwt). Carries
    /// eph=true so it cannot be used to mint another one.
    public string MintEphemeral(string sub) =>
        Mint(new PayloadDto { sub = sub, eph = true }, TimeSpan.FromHours(1));

    /// Rust create_jwt_with_profile: the Discord login token, carrying display
    /// name and avatar hash so the frontend needs no second Discord round-trip.
    public string MintProfile(string sub, string name, string avatar) =>
        Mint(new PayloadDto { sub = sub, name = name, avatar = avatar }, lifetime);

    /// Rust create_captcha_jwt: 30-minute abuse_exemption cookie proving the
    /// holder passed Turnstile. Only /misc/locale* reads it.
    public string MintCaptcha(string sub) =>
        Mint(new PayloadDto { sub = sub, captcha = true }, TimeSpan.FromMinutes(30));

    private string Mint(PayloadDto dto, TimeSpan ttl)
    {
        dto.exp = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto));

        var head = Base64Url.EncodeToString(HeaderBytes);
        var body = Base64Url.EncodeToString(payload);
        var signingInput = $"{head}.{body}";
        var sig = Base64Url.EncodeToString(Sign(signingInput));
        return $"{signingInput}.{sig}";
    }

    public bool TryVerify(string token, out string sub) => TryVerify(token, out sub, out _);

    public bool TryVerify(string token, out string sub, out bool ephemeral)
    {
        var ok = TryVerifyClaims(token, out var claims);
        sub = claims.Sub;
        ephemeral = claims.Ephemeral;
        return ok;
    }

    public bool TryVerifyClaims(string token, out JwtClaims claims)
    {
        claims = new JwtClaims("", "", "", false, false);
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

        claims = new JwtClaims(payload.sub, payload.name ?? "", payload.avatar ?? "",
            payload.eph, payload.captcha);
        return true;
    }

    /// Reads the `sub` claim WITHOUT verifying the signature or expiry. Dev-only:
    /// lets SignInAsSteam accept any JWT (e.g. a token from another server) as an
    /// identity. Never use for real authorization - the signature is the trust.
    public static bool TryReadSubjectUnverified(string token, out string sub)
    {
        sub = "";
        if (string.IsNullOrEmpty(token)) return false;
        var parts = token.Split('.');
        if (parts.Length != 3) return false;
        try
        {
            var payload = JsonSerializer.Deserialize<PayloadDto>(Base64Url.DecodeFromChars(parts[1]));
            if (string.IsNullOrEmpty(payload?.sub)) return false;
            sub = payload.sub;
            return true;
        }
        catch { return false; }
    }

    private byte[] Sign(string signingInput) =>
        HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(signingInput));

    private sealed class PayloadDto
    {
        public string? sub { get; set; }
        public long exp { get; set; }
        // Omitted when default so ordinary game tokens stay byte-identical to pre-dashboard ones.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool eph { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? name { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? avatar { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool captcha { get; set; }
    }
}

/// Verified payload. Mirrors Rust middleware::jwt::Claims minus `exp`, which
/// TryVerify has already enforced by the time a caller sees this.
public readonly record struct JwtClaims(
    string Sub, string Name, string Avatar, bool Ephemeral, bool Captcha);
