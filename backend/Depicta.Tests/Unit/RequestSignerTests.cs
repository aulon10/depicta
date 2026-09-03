using System.Text.RegularExpressions;
using Depicta.Api.Security;
using Xunit;

namespace Depicta.Tests.Unit;

/// <summary>
/// The signature the WordPress plugin verifies. These tests pin the exact wire format so the
/// C# side can never silently drift from the PHP side (a drift would 401 every write).
/// </summary>
public class RequestSignerTests
{
    [Fact]
    public void Canonical_string_has_five_lines_in_order()
    {
        var canonical = RequestSigner.BuildCanonical("POST", "/media/482/alt", "1756800000", "abc123", "{\"alt\":\"hi\"}");
        var lines = canonical.Split('\n');
        Assert.Equal(5, lines.Length);
        Assert.Equal("POST", lines[0]);
        Assert.Equal("/media/482/alt", lines[1]);
        Assert.Equal("1756800000", lines[2]);
        Assert.Equal("abc123", lines[3]);
        // line[4] is base64(SHA256(body))
        Assert.False(string.IsNullOrEmpty(lines[4]));
    }

    [Fact]
    public void Signature_is_deterministic_for_the_same_inputs()
    {
        var canonical = RequestSigner.BuildCanonical("GET", "/media/missing-alt", "1", "n", "");
        Assert.Equal(
            RequestSigner.ComputeSignature("secret", canonical),
            RequestSigner.ComputeSignature("secret", canonical));
    }

    [Fact]
    public void Different_secret_produces_a_different_signature()
    {
        var canonical = RequestSigner.BuildCanonical("GET", "/media/missing-alt", "1", "n", "");
        Assert.NotEqual(
            RequestSigner.ComputeSignature("secret-a", canonical),
            RequestSigner.ComputeSignature("secret-b", canonical));
    }

    [Fact]
    public void Tampering_with_the_body_changes_the_signature()
    {
        var original = RequestSigner.BuildCanonical("POST", "/media/482/alt", "1756800000", "abc123", "{\"alt\":\"hi\"}");
        var tampered = RequestSigner.BuildCanonical("POST", "/media/482/alt", "1756800000", "abc123", "{\"alt\":\"TAMPERED\"}");
        Assert.NotEqual(
            RequestSigner.ComputeSignature("secret", original),
            RequestSigner.ComputeSignature("secret", tampered));
    }

    [Fact]
    public void Known_vector_matches_an_independent_hmac_implementation()
    {
        // Cross-checked against Python/PHP HMAC-SHA256 of the same canonical string.
        var canonical = RequestSigner.BuildCanonical("POST", "/media/482/alt", "1756800000", "abc123", "{\"alt\":\"hi\"}");
        Assert.Equal("kwI7vNB063HpRqVUgV0V2QwAYvxrqFWAwGBoe3AaJx0=",
            RequestSigner.ComputeSignature("secret", canonical));
    }

    [Fact]
    public void Sign_emits_a_hex_nonce_and_unix_timestamp()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1756800000);
        var (timestamp, nonce, signature) = RequestSigner.Sign("secret", "POST", "/media/1/alt", "{}", now);
        Assert.Equal("1756800000", timestamp);
        Assert.Matches(new Regex("^[a-f0-9]{32}$"), nonce);
        Assert.False(string.IsNullOrEmpty(signature));
    }
}
