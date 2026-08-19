using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace RecrutamentoIA.Api.Auth;

/// <summary>Token JWT emitido após um login com sucesso.</summary>
public record IssuedToken(string Token, string Username, DateTime ExpiresAtUtc);

/// <summary>
/// Emite tokens JWT assinados com HMAC-SHA256 (HS256) usando o segredo de <c>Jwt:Secret</c>.
/// </summary>
public sealed class TokenService
{
    private readonly JwtSettings _settings;

    public TokenService(JwtSettings settings) => _settings = settings;

    public IssuedToken CreateToken(UserRecord user, DateTime? nowUtc = null)
    {
        var issuedAt = nowUtc ?? DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Username)
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: issuedAt.AddMinutes(_settings.ExpirationMinutes),
            signingCredentials: new SigningCredentials(_settings.SigningKey, SecurityAlgorithms.HmacSha256));

        var serialized = new JwtSecurityTokenHandler().WriteToken(token);
        return new IssuedToken(serialized, user.Username, token.ValidTo);
    }
}