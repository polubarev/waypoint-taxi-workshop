using System.Security.Cryptography;
using Shared.Contracts;
namespace Auth.Domain;
public static class Passwords
{
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password,salt,100_000,HashAlgorithmName.SHA256,32);
        return Convert.ToBase64String(salt)+"."+Convert.ToBase64String(hash);
    }
    public static bool Verify(string password,string encoded)
    {
        try
        {
            var parts = encoded.Split('.');
            var actual = Rfc2898DeriveBytes.Pbkdf2(password,Convert.FromBase64String(parts[0]),100_000,HashAlgorithmName.SHA256,32);
            return CryptographicOperations.FixedTimeEquals(actual,Convert.FromBase64String(parts[1]));
        }
        catch (Exception e) when (e is FormatException or IndexOutOfRangeException) { return false; }
    }
    public static string TokenHash(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
public static class UserRules
{
    public static bool ValidRoles(string[] roles) => roles.Length > 0 && roles.Distinct().Count()==roles.Length && roles.All(r=>Roles.All.Contains(r));
    public static bool CanSeeUser(Guid actor, string[] roles, Guid target, Guid? targetManager) => actor==target || targetManager==actor || roles.Contains(Roles.SystemAdmin);
}
