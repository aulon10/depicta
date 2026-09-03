using Depicta.Api.Security.Auth;
using Xunit;

namespace Depicta.Tests.Unit;

public class PasswordHasherTests
{
    [Fact]
    public void Verify_accepts_the_correct_password()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");
        Assert.True(PasswordHasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_rejects_the_wrong_password()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");
        Assert.False(PasswordHasher.Verify("Correct Horse Battery Staple", hash));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("v1.notanumber.aaaa.bbbb")]
    [InlineData("")]
    public void Verify_rejects_malformed_hashes(string encoded)
        => Assert.False(PasswordHasher.Verify("anything", encoded));

    [Fact]
    public void Hash_is_salted_so_two_hashes_of_the_same_password_differ()
        => Assert.NotEqual(PasswordHasher.Hash("same"), PasswordHasher.Hash("same"));

    [Fact]
    public void Dev_password_verifies_against_the_shipped_dev_hash()
    {
        // This is the hash in appsettings.Development.json — if it ever drifts from the documented
        // dev password, local login breaks. This test pins them together.
        const string devHash = "v1.210000.DLIdmMq+L0FbaMgYeL0oHw==.nFgJH+YxTmHjXXoKR0Rg05airQBBowCjdcRwUPeuN9Q=";
        Assert.True(PasswordHasher.Verify("depicta-dev-2026", devHash));
    }
}
