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
builder.Services.AddSingleton(new CandidatoRepository(builder.Environment.ContentRootPath));
builder.Services.AddSingleton(new VagaRepository(builder.Environment.ContentRootPath));
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
            "`Authorization: Bearer <token>`. Os dados ficam no arquivo CSV `Data/users.csv` (sem banco de dados).\n\n" +
            "✅ **Aprovação de acesso**: todo novo usuário nasce com `allowed = false` (bloqueado).\n" +
            "O responsável libera o acesso marcando `allowed = true` em `Data/users.csv` **ou** chamando\n" +
            "`POST /api/usuarios/{username}/permissoes` com `{ \"allowed\": true }` (somente outro usuário\n" +
            "já aprovado pode aprovar alguém; ninguém aprova a si mesmo pela API). Enquanto `allowed` for\n" +
            "`false`, `POST /api/analisar` responde `403` mesmo com token válido.\n" +
            "`GET /api/usuarios` lista todos os usuários e o estado de aprovação de cada um."
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

// Semáforo ÚNICO do processo: limita as chamadas simultâneas à IA somando todas
// as requisições (/api/analisar e /api/vagas), não por requisição.
var analisesSemaforo = new SemaphoreSlim(maxAnalisesSimultaneas);

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
app.MapUsuariosEndpoints();

app.MapPost("/api/analisar", async (
    HttpContext http,
    [FromForm] string descricaoVaga,
    IFormFileCollection curriculos,
    UserRepository users,
    CandidatoRepository candidatos,
    IExtracaoTextoService extracao,
    IAgenteIAService agente,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    // Aprovação de acesso: usuário registrado + autenticado + allowed == true.
    var usuario = users.FindByUsername(http.User.Identity?.Name ?? string.Empty);
    if (usuario is null)
        return Results.Unauthorized();
    if (!usuario.Allowed)
        return Results.Json(
            new { erro = "Sua conta ainda não foi aprovada para uso, entre em contato com o responsável pelo sistema." },
            statusCode: StatusCodes.Status403Forbidden);

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

    // Analisa em paralelo (limite global de chamadas simultâneas à IA)
    var tasks = curriculosTexto.Select(async c =>
    {
        await analisesSemaforo.WaitAsync(ct);
        try { return await agente.AnalisarCurriculoAsync(descricaoVaga, c, ct); }
        finally { analisesSemaforo.Release(); }
    });
    var resultados = await Task.WhenAll(tasks);

    var ranking = resultados.OrderByDescending(r => r.Score).ToList();

    // Cada currículo analisado alimenta o cadastro de candidatos (Data/candidatos.json).
    var vagaTitulo = descricaoVaga
        .Split('\n')
        .Select(l => l.Trim())
        .FirstOrDefault(l => l.Length > 0) ?? "";
    if (vagaTitulo.Length > 70) vagaTitulo = vagaTitulo[..70];
    foreach (var r in ranking)
    {
        // Falhas de análise/extração não viram perfil.
        if (r.NomeCandidato is "Erro" or "Erro no parse") continue;
        try { candidatos.Upsert(r, vagaTitulo); }
        catch (Exception ex) { logger.LogWarning(ex, "Falha ao salvar perfil do candidato de {Arquivo}", r.NomeArquivo); }
    }

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

// Cadastro de candidatos montado a partir dos currículos analisados.
app.MapGet("/api/candidatos", (HttpContext http, UserRepository users, CandidatoRepository candidatos) =>
{
    var usuario = users.FindByUsername(http.User.Identity?.Name ?? string.Empty);
    if (usuario is null)
        return Results.Unauthorized();
    if (!usuario.Allowed)
        return Results.Json(
            new { erro = "Sua conta ainda não foi aprovada para uso, entre em contato com o responsável pelo sistema." },
            statusCode: StatusCodes.Status403Forbidden);

    var todos = candidatos.ListAll()
        .OrderByDescending(c => c.AtualizadoEmUtc)
        .ToList();
    return Results.Ok(todos);
})
.RequireAuthorization()
.WithName("ListarCandidatos");

// ── Vagas cadastradas ────────────────────────────────────────────────────
// Ao cadastrar uma vaga, os candidatos do cadastro são analisados contra ela
// (perfil sintetizado × descrição da vaga) e o melhor posicionado volta como
// "candidato ideal". Cada análise entra no histórico do candidato, então o
// Dashboard passa a filtrar pela vaga nova automaticamente.

app.MapGet("/api/vagas", (HttpContext http, UserRepository users, VagaRepository vagas) =>
{
    var usuario = users.FindByUsername(http.User.Identity?.Name ?? string.Empty);
    if (usuario is null)
        return Results.Unauthorized();
    if (!usuario.Allowed)
        return Results.Json(
            new { erro = "Sua conta ainda não foi aprovada para uso, entre em contato com o responsável pelo sistema." },
            statusCode: StatusCodes.Status403Forbidden);

    var todas = vagas.ListAll()
        .OrderByDescending(v => v.CriadaEmUtc)
        .ToList();
    return Results.Ok(todas);
})
.RequireAuthorization()
.WithName("ListarVagas");

app.MapPost("/api/vagas", async (
    HttpContext http,
    CadastroVagaRequest body,
    UserRepository users,
    VagaRepository vagas,
    CandidatoRepository candidatos,
    IAgenteIAService agente,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var usuario = users.FindByUsername(http.User.Identity?.Name ?? string.Empty);
    if (usuario is null)
        return Results.Unauthorized();
    if (!usuario.Allowed)
        return Results.Json(
            new { erro = "Sua conta ainda não foi aprovada para uso, entre em contato com o responsável pelo sistema." },
            statusCode: StatusCodes.Status403Forbidden);

    if (string.IsNullOrWhiteSpace(body.Titulo))
        return Results.BadRequest(new { erro = "Campo 'titulo' é obrigatório." });
    if (string.IsNullOrWhiteSpace(body.Descricao))
        return Results.BadRequest(new { erro = "Campo 'descricao' é obrigatório." });
    if (body.Descricao.Length > 20_000)
        return Results.BadRequest(new { erro = "Descrição muito longa (máximo de 20.000 caracteres)." });
    if (vagas.ExisteTitulo(body.Titulo))
        return Results.Conflict(new { erro = "Já existe uma vaga cadastrada com esse título." });

    var titulo = VagaRepository.NormalizarTitulo(body.Titulo);
    var perfis = candidatos.ListAll();
    logger.LogInformation("Analisando {N} candidatos do cadastro contra a vaga '{Titulo}'.", perfis.Count, titulo);

    // Analisa ANTES de persistir: cancelamento ou falha total da IA não deixam
    // vaga órfã (sem histórico e bloqueada pelo 409 no re-cadastro).
    // O título entra como primeira linha, igual ao título derivado em /api/analisar.
    var descricaoCompleta = $"{titulo}\n{body.Descricao.Trim()}";
    var tasks = perfis.Select(async p =>
    {
        await analisesSemaforo.WaitAsync(ct);
        try
        {
            var resultado = await agente.AnalisarCurriculoAsync(
                descricaoCompleta, new CurriculoTexto(p.NomeArquivo, p.PerfilTexto()), ct);
            return (perfil: p, resultado);
        }
        finally { analisesSemaforo.Release(); }
    });
    var analises = await Task.WhenAll(tasks);

    // Falhas de análise ficam fora do ranking e do histórico.
    var validas = analises
        .Where(a => a.resultado.NomeCandidato is not ("Erro" or "Erro no parse"))
        .ToList();
    var falhas = analises.Length - validas.Count;
    if (falhas > 0)
        logger.LogWarning("{Falhas} análise(s) falharam contra a vaga '{Vaga}'.", falhas, titulo);
    if (perfis.Count > 0 && validas.Count == 0)
        return Results.Json(
            new { erro = "Não foi possível analisar os candidatos agora (falha na IA). A vaga não foi cadastrada — tente novamente." },
            statusCode: StatusCodes.Status503ServiceUnavailable);

    var vaga = vagas.Add(body.Titulo, body.Descricao);
    if (vaga is null)
        return Results.Conflict(new { erro = "Já existe uma vaga cadastrada com esse título." });

    var gravadas = candidatos.RegistrarAnalises(
        validas.Select(a => (a.perfil.Id, vaga.Titulo, a.resultado.Score)));
    if (gravadas < validas.Count)
        logger.LogWarning("{N} análise(s) não entraram no histórico (candidato não encontrado).", validas.Count - gravadas);

    var ranking = validas
        .Select(a => new VagaRankingItem(a.perfil.Id, a.perfil.Nome, a.resultado.Score, a.resultado.Resumo))
        .OrderByDescending(r => r.Score)
        .ToList();

    return Results.Ok(new
    {
        vaga,
        totalCandidatos = perfis.Count,
        ranking,
    });
})
.RequireAuthorization()
.WithName("CadastrarVaga");

app.Run();

