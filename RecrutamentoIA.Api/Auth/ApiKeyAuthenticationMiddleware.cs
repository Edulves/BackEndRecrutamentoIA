namespace RecrutamentoIA.Api.Auth;

/// <summary>
/// Exige uma chave válida (cabeçalho <see cref="ApiKeyCredentials.HeaderName"/>) em toda requisição.
/// Em Development, a documentação (Swagger/Scalar, rotas /openapi, /scalar e /swagger) fica liberada.
/// </summary>
public sealed class ApiKeyAuthenticationMiddleware
{
    private static readonly PathString[] DevOpenPaths =
    {
        "/openapi", "/scalar", "/swagger"
    };

    private readonly RequestDelegate _next;
    private readonly ApiKeyCredentials _credentials;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ApiKeyCredentials credentials,
        IWebHostEnvironment environment,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _credentials = credentials;
        _environment = environment;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_credentials.Enabled)
        {
            await _next(context);
            return;
        }

        // Pré-flight do CORS (o UseCors já roda antes e responde os OPTIONS permitidos).
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Em desenvolvimento, a documentação fica acessível sem chave (somente Swagger/Scalar).
        if (_environment.IsDevelopment() && IsOpenDocumentationPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var headerPresent = context.Request.Headers.TryGetValue(_credentials.HeaderName, out var values);
        var presentedKey = values.FirstOrDefault();

        if (!headerPresent
            || string.IsNullOrEmpty(presentedKey)
            || !_credentials.ApiKeys.Contains(presentedKey, StringComparer.Ordinal))
        {
            _logger.LogWarning(
                "Requisição sem chave válida ({Header}) de {Remote} em {Method} {Path}.",
                _credentials.HeaderName, context.Connection.RemoteIpAddress,
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = _credentials.HeaderName;
            await context.Response.WriteAsJsonAsync(new
            {
                erro = $"Acesso não autorizado. Informe uma chave válida no cabeçalho '{_credentials.HeaderName}'."
            });
            return;
        }

        await _next(context);
    }

    private static bool IsOpenDocumentationPath(PathString path)
        => DevOpenPaths.Any(prefix => path.StartsWithSegments(prefix));
}