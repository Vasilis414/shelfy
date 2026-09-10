using System.Security.Cryptography;
using System.Text;

namespace Shelfy.Services;

public static class HashingAlgorithm
{
    public static string ComputeSha256(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }
}
