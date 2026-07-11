using System.Security.Cryptography;
using System.Text;

namespace FinanceManager.Application.Identity;

public static class PasswordEncryptionProvider
{
    public static string EncryptPassword(string inputString)
    {
        var data = Encoding.ASCII.GetBytes(inputString);
        var hashedData = SHA256.HashData(data);

        // Render the raw hash as hex rather than reinterpreting the bytes as an ASCII string.
        // The old Encoding.ASCII.GetString(hashedData) was both lossy (every byte > 127 collapsed to '?')
        // and produced NUL (0x00) bytes, which SQL Server's nvarchar tolerated but PostgreSQL/Supabase
        // rejects in text columns (error 22021: invalid byte sequence for encoding "UTF8": 0x00).
        return Convert.ToHexString(hashedData);
    }
}