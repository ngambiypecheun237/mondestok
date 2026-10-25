using Isopoh.Cryptography.Argon2;
using System.Security.Cryptography;
using System.Text;

namespace Destock.App.Services;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        var config = new Argon2Config
        {
            Type = Argon2Type.DataIndependentAddressing,
            Version = Argon2Version.Nineteen,
            TimeCost = 3,
            MemoryCost = 65536,
            Lanes = 4,
            Threads = 4,
            Password = Encoding.UTF8.GetBytes(password),
            Salt = RandomNumberGenerator.GetBytes(16)
        };

        var argon2 = new Argon2(config);
        using var hash = argon2.Hash();
        return config.EncodeString(hash.Buffer);
    }

    public static bool Verify(string password, string hashString)
    {
        if (string.IsNullOrWhiteSpace(hashString))
        {
            return false;
        }

        try
        {
            return Argon2.Verify(hashString, Encoding.UTF8.GetBytes(password));
        }
        catch
        {
            return false;
        }
    }
}
