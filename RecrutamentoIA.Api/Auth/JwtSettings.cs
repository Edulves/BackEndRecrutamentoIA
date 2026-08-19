using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace RecrutamentoIA.Api.Auth;

/// <summary>
/// Configuração do JWT (seção "Jwt" do appsettings). O segredo deve ficar
/// em <c>appsettings.local.json</c> (ignorado pelo git) e ter pelo menos 32 bytes.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "RecrutamentoIA.Api";
    public string Audience { get; set; } = "RecrutamentoIA.Client";

    /// <summary>Segredo usado para assinar e validar os tokens (HS256).</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Tempo de validade de cada token emitido no login.</summary>
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>Chave simétrica derivada do segredo.</summary>
    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Secret));

    /// <summary>
    /// Garante que o segredo está presente e forte o suficiente; caso contrário,
    /// aborta a inicialização (fail-closed).
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Secret) || Encoding.UTF8.GetByteCount(Secret) < 32)
        {
            throw new InvalidOperationException(
                $"'{SectionName}:Secret' não configurado ou muito curto (mínimo 32 bytes). " +
                "Defina uma chave forte (ex.: 64 caracteres hexadecimais aleatórios) em " +
                "appsettings.local.json, na seção \"Jwt\".");
        }
    }
}