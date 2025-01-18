using System.Security.Cryptography;

namespace FinanceManager.Application.Providers
{
    public static class PasswordEncryptionProvider
    {
        public static string EncryptPassword(string inputString)
        {
            byte[] data = System.Text.Encoding.ASCII.GetBytes(inputString);
            var hashAlgorithm = SHA256.Create();

            data = hashAlgorithm.ComputeHash(data);
            string hash = System.Text.Encoding.ASCII.GetString(data);

            return hash;
        }
    }
}
