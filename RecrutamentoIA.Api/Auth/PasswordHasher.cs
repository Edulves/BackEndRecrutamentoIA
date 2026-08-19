using System.Security.Cryptography;

namespace RecrutamentoIA.Api.Auth;

/// <summary>
/// "Criptografia" de senhas com PBKDF2-SHA256 + salt aleatório por usuário.
/// No arquivo CSV nunca é gravado texto puro: somente o hash e o salt (base64).
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;         // 128 bits
    private const int HashSize = 32;         // 256 bits
    private const int Iterations = 100_000;  // custo intencional contra força bruta
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>
    /// Gera o hash seguro da senha. O salt (base64) é exportado para ser persistido
    /// ao lado do hash no arquivo CSV.
    /// </summary>
    public static string HashPassword(string password, out string salt)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password, saltBytes, Iterations, Algorithm, HashSize);

        salt = Convert.ToBase64String(saltBytes);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Confere a senha em tempo constante (evita timing attack) contra o hash do CSV.
    /// </summary>
    public static bool Verify(string password, string saltBase64, string expectedHashBase64)
    {
        var saltBytes = Convert.FromBase64String(saltBase64);
        var expectedBytes = Convert.FromBase64String(expectedHashBase64);
        var actualBytes = Rfc2898DeriveBytes.Pbkdf2(
            password, saltBytes, Iterations, Algorithm, expectedBytes.Length);

        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}