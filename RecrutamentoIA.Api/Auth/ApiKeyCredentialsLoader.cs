using System.Security.Cryptography;
using System.Text.Json;

namespace RecrutamentoIA.Api.Auth;

/// <summary>
/// Responsável por carregar as credenciais de <c>api-credentials.json</c>.
/// Esse arquivo contém o segredo de acesso e é ignorado pelo git (.gitignore),
/// portanto existe apenas no servidor / na máquina de desenvolvimento.
/// </summary>
public static class ApiKeyCredentialsLoader
{
    /// <summary>Arquivo real (segredo, não vai para o git).</summary>
    public const string FileName = "api-credentials.json";

    /// <summary>Modelo versionado com o formato esperado (valores de exemplo).</summary>
    public const string ExampleFileName = "api-credentials.example.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // O arquivo segue o padrão camelCase (enabled, headerName, apiKeys);
        // a de-serialização é case-insensitive para casar com as propriedades C#.
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Carrega as credenciais do disco.
    /// <para>
    /// Se o arquivo não existir:
    /// em <b>Development</b> ele é criado automaticamente com uma chave aleatória (e a chave é logada);
    /// em <b>qualquer outro ambiente</b> a inicialização falha (fail-closed), exigindo que o arquivo
    /// seja copiado para o servidor.
    /// </para>
    /// </summary>
    public static ApiKeyCredentials LoadOrCreate(string filePath, bool isDevelopment, ILogger logger)
    {
        if (!File.Exists(filePath))
        {
            if (!isDevelopment)
            {
                var message =
                    $"Arquivo de credenciais '{filePath}' não encontrado. Para liberar a API em produção, " +
                    $"copie '{ExampleFileName}' para '{filePath}' e preencha 'apiKeys' com uma chave forte.";
                logger.LogCritical("{Message}", message);
                throw new FileNotFoundException(message, filePath);
            }

            var generatedKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)); // 48 chars hex
            var created = new ApiKeyCredentials { Enabled = true };
            created.ApiKeys.Add(generatedKey);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
            File.WriteAllText(filePath, JsonSerializer.Serialize(created, JsonOptions));

            logger.LogInformation("Arquivo de credenciais '{File}' criado automaticamente.", filePath);
            logger.LogInformation(
                "Guarde esta chave e use-a no cabeçalho {Header}: {Key}",
                created.HeaderName, generatedKey);
            return created;
        }

        ApiKeyCredentials credentials;
        try
        {
            credentials = JsonSerializer.Deserialize<ApiKeyCredentials>(File.ReadAllText(filePath), JsonOptions)
                ?? new ApiKeyCredentials();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Falha ao ler o arquivo de credenciais '{filePath}'. Valide o JSON e tente novamente.", ex);
        }

        if (string.IsNullOrWhiteSpace(credentials.HeaderName))
            credentials.HeaderName = ApiKeyCredentials.DefaultHeaderName;

        if (credentials.Enabled && (credentials.ApiKeys is null || credentials.ApiKeys.Count == 0))
        {
            throw new InvalidOperationException(
                $"Autenticação habilitada em '{filePath}', mas 'apiKeys' está vazio. " +
                "Adicione ao menos uma chave ou defina 'enabled' como false.");
        }

        logger.LogInformation("Autenticação por API Key ativa: {N} chave(s) carregada(s).", credentials.ApiKeys.Count);
        return credentials;
    }
}