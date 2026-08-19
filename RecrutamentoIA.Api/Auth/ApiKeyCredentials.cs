namespace RecrutamentoIA.Api.Auth;

/// <summary>
/// Credenciais de acesso da API, lidas de <c>api-credentials.json</c>.
/// Esse arquivo NÃO vai para o git (<see cref="ApiKeyCredentialsLoader"/>) — só existe no servidor.
/// </summary>
public sealed class ApiKeyCredentials
{
    public const string DefaultHeaderName = "X-Api-Key";

    /// <summary>
    /// Se <c>true</c> (padrão), toda chamada exige uma chave válida no cabeçalho <see cref="HeaderName"/>.
    /// Use <c>false</c> apenas para desativar a proteção temporariamente (não recomendado).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Nome do cabeçalho HTTP que transporta a chave.</summary>
    public string HeaderName { get; set; } = DefaultHeaderName;

    /// <summary>Chaves aceitas para acesso. Pode haver mais de uma (ex.: uma por consumidor).</summary>
    public List<string> ApiKeys { get; set; } = new();
}