using System;
using System.Security.Cryptography;

namespace PolyStock.Core.Common;

public static class PasswordSecurity
{
    private const int SaltByteSize = 16;
    private const int HashByteSize = 32;
    private const int Iterations = 10000;

    public static (string Hash, string Salt) HashPassword(string password)
    {
        if (password == null) throw new ArgumentNullException(nameof(password));

        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltByteSize);
        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            HashByteSize);

        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
        {
            return false;
        }

        try
        {
            byte[] saltBytes = Convert.FromBase64String(storedSalt);
            byte[] expectedHashBytes = Convert.FromBase64String(storedHash);

            byte[] actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                Iterations,
                HashAlgorithmName.SHA256,
                HashByteSize);

            return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
        }
        catch
        {
            return false;
        }
    }
}
