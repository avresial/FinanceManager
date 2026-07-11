
using FinanceManager.Application.Identity;

namespace FinanceManager.Tests.Unit.Application.Providers;

[Trait("Category", "Unit")]
public class PasswordEncryptionProviderTests
{
    [Theory]
    [InlineData("testuser", "AE5DEB822E0D71992900471A7199D0D95B8E7C9D05C40A8245A281FD2C1D6684")]
    [InlineData("admin", "8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918")]
    public void EncryptPassword_ReturnsHexEncodedSha256(string input, string expectedHex)
    {
        var result = PasswordEncryptionProvider.EncryptPassword(input);

        Assert.Equal(expectedHex, result);
    }

    [Fact]
    public void EncryptPassword_IsDeterministic()
    {
        Assert.Equal(
            PasswordEncryptionProvider.EncryptPassword("repeat-me"),
            PasswordEncryptionProvider.EncryptPassword("repeat-me"));
    }

    // Regression for the Supabase/PostgreSQL failure (error 22021): the previous Encoding.ASCII.GetString(hash)
    // could emit NUL (0x00) bytes that PostgreSQL rejects in text columns. The output must be storable text:
    // pure ASCII hex with no NUL bytes, for any input — including ones whose raw hash contains a 0x00 byte.
    [Theory]
    [InlineData("")]
    [InlineData("testuser")]
    [InlineData("admin")]
    [InlineData("p@ssw0rd-with-symbols!#")]
    [InlineData("a-much-longer-passphrase-to-vary-the-hash-bytes-0123456789")]
    public void EncryptPassword_ProducesNulFreeAsciiHex(string input)
    {
        var result = PasswordEncryptionProvider.EncryptPassword(input);

        Assert.DoesNotContain('\0', result);
        Assert.Equal(64, result.Length);
        Assert.All(result, c => Assert.Contains(c, "0123456789ABCDEF"));
    }
}