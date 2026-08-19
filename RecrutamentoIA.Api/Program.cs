using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
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
// Autenticação por usuário/senha (CSV) + JWT
// - Os usuários ficam em Data/users.csv (sem banco de dados); a senha é
//   armazenada apenas como hash PBKDF2 + salt, nunca em texto puro.
// - O login (POST /api/auth/login) devolve um JWT assinado (HS256) e os
//   endpoints protegidos (ex.: POST /api/analisar) exigem o cabeçalho
//   "Authorization: Bearer <token>".
// ─────────────────────────────────────────────────────────────────────────
var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>()
    ?? new JwtSettings();
jwtSettings.Validate();

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(new UserRepository(builder.Environment.ContentRootPath));
builder.Services.AddSingleton(new TokenService(jwtSettings));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtSettings.SigningKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

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
            "🔒 **Autenticação**: crie um usuário em `POST /api/auth/registrar`, faça login\n" +
            "em `POST /api/auth/login` e envie o token JWT no cabeçalho\n" +
            "`Authorization: Bearer <token>`. Os dados ficam no arquivo CSV `Data/users.csv` (sem banco de dados)."
    });
    c.OperationFilter<MultipartDocumentationFilter>();

    // Documenta o token JWT (Bearer) exigido pelo endpoint protegido.
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Informe o token obtido em POST /api/auth/login no formato: Bearer SEU_TOKEN"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
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
app.UseAuthentication();
app.UseAuthorization();

// Endpoints públicos de autenticação (registro e login). Rotas protegidas usam JWT.
app.MapAuthEndpoints();

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
.RequireAuthorization()
.WithName("AnalisarCurriculos");

app.Run();

