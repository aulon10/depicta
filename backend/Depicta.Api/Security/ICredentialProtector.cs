namespace Depicta.Api.Security;

/// <summary>Encrypts/decrypts the WordPress app password and plugin secret at rest.</summary>
public interface ICredentialProtector
{
    byte[] Protect(string plaintext);
    string Unprotect(byte[] ciphertext);
}
