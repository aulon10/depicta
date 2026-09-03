using System.Security.Cryptography;
using System.Text;

namespace Depicta.Api.Security;

/// <summary>
/// Builds the HMAC request signature the WordPress companion plugin verifies. Kept separate from
/// the HTTP client so the exact canonicalization + signing is unit-testable and stays byte-for-byte
/// identical to the PHP side (<c>class-depicta-rest.php</c> :: verify_signature).
///
/// Canonical string (each part on its own line):
///   METHOD \n ROUTE \n TIMESTAMP \n NONCE \n BASE64(SHA256(body))
/// The shared secret is the HMAC key and never travels on the wire; the timestamp + one-time
/// nonce make a captured request useless to replay.
/// </summary>
public static class RequestSigner
{
    public static string BuildCanonical(string method, string route, string timestamp, string nonce, string body)
    {
        var bodyHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        return $"{method}\n{route}\n{timestamp}\n{nonce}\n{bodyHash}";
    }

    public static string ComputeSignature(string secret, string canonical) =>
        Convert.ToBase64String(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(canonical)));

    public static string NewNonce() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    /// <summary>Produce the (timestamp, nonce, signature) triple for one request.</summary>
    public static (string Timestamp, string Nonce, string Signature) Sign(
        string secret, string method, string route, string body, DateTimeOffset now)
    {
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var nonce = NewNonce();
        var signature = ComputeSignature(secret, BuildCanonical(method, route, timestamp, nonce, body));
        return (timestamp, nonce, signature);
    }
}
