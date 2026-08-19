using Microsoft.AspNetCore.Mvc;
using RecrutamentoIA.Api.Auth;
using RecrutamentoIA.Api.Filters;
using RecrutamentoIA.Api.Models;
using RecrutamentoIA.Api.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Carrega appsettings.local.json (ignorado pelo git) p/ segredos locais,
// ex.: a chave real da IA que NÃO deve ser versionada.
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true);

// ─────────────────────────────────────────────────────────────────────────
// Autenticação por API Key
// As credenciais vivem em api-credentials.json (arquivo que NÃO vai para o
// git — está no .gitignore). Em Development, se o arquivo não existir, ele é
// criado automaticamente com uma chave aleatória (exibida no console).
// ─────────────────────────────────────────────────────────────────────────
using var startupLoggerFactory = LoggerFactory.Create(l => l.AddSimpleConsole(o => o.SingleLine = true));
var startupLogger = startupLoggerFactory.CreateLogger("RecrutamentoIA.Api.Auth");
var apiKeyCredentials = ApiKeyCredentialsLoader.LoadOrCreate(
    Path.Combine(builder.Environment.ContentRootPath, ApiKeyCredentialsLoader.FileName),
    builder.Environment.IsDevelopment(),
    startupLogger);
builder.Services.AddSingleton(apiKeyCredentials);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "RecrutamentoIA API",
        Version = "v1",
        Description =
            "API para análise e ranqueamento de currículos com IA.\n\n" +
            "### POST /api/analisar\n" +
            "Envie os dados como **multipart/form-data** (form-data), com os campos:\n" +
            "- `descricaoVaga` (texto) — descrição da vaga.\n" +
            "- `curriculos` (arquivos) — um ou mais currículos (PDF, DOCX, DOC ou TXT).\n\n" +
            "Retorna o ranking dos candidatos ordenado por pontuação (Score).\n\n" +
            "🔒 **Autenticação**: envie a chave no cabeçalho `X-Api-Key` (veja `api-credentials.json`)."
    });
    c.OperationFilter<MultipartDocumentationFilter>();

    // Documenta o cabeçalho de API Key exigido por todas as rotas.
    c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = apiKeyCredentials.HeaderName,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Description = "Chave de acesso. Ex.: " + apiKeyCredentials.HeaderName + ": SUA_CHAVE"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddScoped<IExtracaoTextoService, ExtracaoTextoService>();
builder.Services.AddHttpClient<IAgenteIAService, AgenteService>(c =>
{
    c.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173", "http://localhost:4173")
     .AllowAnyHeader()
     .AllowAnyMethod()));

// Aumenta limite de upload (múltiplos currículos)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB
    o.ValueLengthLimit = int.MaxValue;
});

// Limites configuráveis via appsettings.json > "Limites"
var maxCurriculosPorAnalise = builder.Configuration.GetValue<int>("Limites:MaxCurriculosPorAnalise", 100);
var maxAnalisesSimultaneas = builder.Configuration.GetValue<int>("Limites:MaxAnalisesSimultaneas", 8);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapSwagger("/openapi/{documentName}.json");
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("RecrutamentoIA API");
    });
}

app.UseCors();

// Exige a chave de API em todas as rotas (exceto a documentação em Development).
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

app.MapPost("/api/analisar", async (
    [FromForm] string descricaoVaga,
    IFormFileCollection curriculos,
    IExtracaoTextoService extracao,
    IAgenteIAService agente,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(descricaoVaga))
        return Results.BadRequest(new { erro = "Campo 'descricaoVaga' é obrigatório." });
    if (curriculos.Count == 0)
        return Results.BadRequest(new { erro = "Envie ao menos 1 arquivo em 'curriculos'." });
    if (curriculos.Count > maxCurriculosPorAnalise)
        return Results.BadRequest(new { erro = $"Máximo de {maxCurriculosPorAnalise} currículos por análise." });

    logger.LogInformation("Analisando {N} currículos.", curriculos.Count);

    var curriculosTexto = new List<CurriculoTexto>();
    foreach (var arq in curriculos)
    {
        try
        {
            var texto = await extracao.ExtrairAsync(arq, ct);
            curriculosTexto.Add(new CurriculoTexto(arq.FileName, texto));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao extrair texto de {Nome}", arq.FileName);
            curriculosTexto.Add(new CurriculoTexto(arq.FileName, $"[ERRO_EXTRACAO] {ex.Message}"));
        }
    }

    // Analisa em paralelo (limite configurável de chamadas simultâneas à IA)
    var sem = new SemaphoreSlim(maxAnalisesSimultaneas);
    var tasks = curriculosTexto.Select(async c =>
    {
        await sem.WaitAsync(ct);
        try { return await agente.AnalisarCurriculoAsync(descricaoVaga, c, ct); }
        finally { sem.Release(); }
    });
    var resultados = await Task.WhenAll(tasks);

    var ranking = resultados.OrderByDescending(r => r.Score).ToList();

    var response = new AnaliseResponse(
        DescricaoVaga: descricaoVaga,
        TotalCurriculos: ranking.Count,
        Ranking: ranking,
        ProcessadoEm: DateTime.UtcNow
    );

    return Results.Ok(response);
})
.DisableAntiforgery()
.WithName("AnalisarCurriculos");

app.Run();

