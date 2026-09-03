using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Depicta.Api.Security;

/// <summary>
/// ASP.NET Data Protection implementation. In production the key ring should be persisted to
/// a durable, protected store (e.g. a KMS / key vault) so restarts don't lose the ability to
/// decrypt stored credentials — see Program.cs.
/// </summary>
public sealed class DataProtectionCredentialProtector : ICredentialProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionCredentialProtector(IDataProtectionProvider provider)
    {
        // Purpose string binds ciphertext to this use — it can't be reused elsewhere.
        _protector = provider.CreateProtector("Depicta.Credentials.v1");
    }

    public byte[] Protect(string plaintext) =>
        _protector.Protect(Encoding.UTF8.GetBytes(plaintext));

    public string Unprotect(byte[] ciphertext) =>
        Encoding.UTF8.GetString(_protector.Unprotect(ciphertext));
}
