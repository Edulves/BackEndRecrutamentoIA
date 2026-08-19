namespace RecrutamentoIA.Api.Auth;

/// <summary>
/// Um usuário do sistema. A senha fica apenas como hash PBKDF2 (+ salt),
/// nunca em texto puro, no arquivo CSV.
/// </summary>
public sealed class UserRecord
{
    public required string Username { get; init; }
    public required string PasswordHash { get; init; }
    public required string Salt { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}