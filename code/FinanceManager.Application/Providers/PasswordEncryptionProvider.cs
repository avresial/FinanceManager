using System.Security.Cryptography;
using System.Text;

namespace FinanceManager.Application.Providers;

public static class PasswordEncryptionProvider
{
    public static string EncryptPassword(string inputString)
    {
        var data = Encoding.ASCII.GetBytes(inputString);
        var hashedData = SHA256.HashData(data);

        return Encoding.ASCII.GetString(hashedData);
    }
}