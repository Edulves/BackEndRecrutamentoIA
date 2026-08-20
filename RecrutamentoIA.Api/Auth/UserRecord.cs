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

    /// <summary>
    /// Se <c>true</c>, o usuário está aprovado e pode usar o sistema (campo <c>allowed</c> do CSV).
    /// Novos usuários começam com <c>false</c> e só passam a usar depois da aprovação do responsável.
    /// </summary>
    public bool Allowed { get; init; } = false;
}