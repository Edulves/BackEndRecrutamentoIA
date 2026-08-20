namespace RecrutamentoIA.Api.Auth;

public record RegisterRequest(string Username, string Password);
public record LoginRequest(string Username, string Password);

/// <summary>
/// Endpoints públicos de autenticação (registro e login).
/// O login devolve o token JWT que protege o endpoint <c>POST /api/analisar</c>.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // ── Registro: cria login/senha no CSV (a senha é gravada apenas como hash) ──
        app.MapPost("/api/auth/registrar", (RegisterRequest request, UserRepository users, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("RecrutamentoIA.Api.Auth");
            var username = request?.Username?.Trim() ?? string.Empty;
            var password = request?.Password ?? string.Empty;

            if (username.Length < 3)
                return Results.BadRequest(new { erro = "O nome de usuário deve ter ao menos 3 caracteres." });
            if (username.Length > 50)
                return Results.BadRequest(new { erro = "O nome de usuário deve ter no máximo 50 caracteres." });
            if (username.Contains(',') || username.Contains('"') ||
                username.Contains('\n') || username.Contains('\r'))
                return Results.BadRequest(new { erro = "O nome de usuário não pode conter vírgula, aspas ou quebras de linha." });
            if (password.Length < 6)
                return Results.BadRequest(new { erro = "A senha deve ter ao menos 6 caracteres." });
            if (password.Length > 128)
                return Results.BadRequest(new { erro = "A senha deve ter no máximo 128 caracteres." });

            var user = new UserRecord
            {
                Username = username,
                PasswordHash = PasswordHasher.HashPassword(password, out var salt),
                Salt = salt,
                CreatedAtUtc = DateTime.UtcNow,
                // Novos usuários começam bloqueados até o responsável aprovar o acesso
                // (campo 'allowed' = true em Data/users.csv ou pelo endpoint de permissões).
                Allowed = false,
            };

            if (!users.Create(user))
                return Results.Conflict(new { erro = "Já existe um usuário com este nome de usuário." });

            logger.LogInformation(
                "Usuário '{Username}' criado com sucesso (arquivo: {File}).",
                user.Username, users.FilePath);

            return Results.Created(
                "/api/auth/login",
                new
                {
                    mensagem = "Usuário criado com sucesso. A conta ainda NÃO está liberada para uso: aguarde a aprovação do responsável (campo 'allowed' será ativado).",
                    username = user.Username,
                    criadoEmUtc = user.CreatedAtUtc,
                    aprovado = user.Allowed,
                });
        })
        .WithName("RegistrarUsuario");

        // ── Login: valida login/senha e devolve o token JWT (Bearer) ──
        app.MapPost("/api/auth/login", (LoginRequest request, UserRepository users, TokenService tokens) =>
        {
            var username = request?.Username?.Trim() ?? string.Empty;
            var password = request?.Password ?? string.Empty;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return Results.BadRequest(new { erro = "Informe 'username' e 'password'." });

            var user = users.FindByUsername(username);
            // Mensagem genérica: não revela se o usuário existe ou se a senha está errada.
            if (user is null || !PasswordHasher.Verify(password, user.Salt, user.PasswordHash))
                return Results.Json(
                    new { erro = "Nome de usuário ou senha inválidos." },
                    statusCode: StatusCodes.Status401Unauthorized);

            var issued = tokens.CreateToken(user);
            return Results.Ok(new
            {
                mensagem = "Login realizado com sucesso.",
                usuario = issued.Username,
                token = issued.Token,
                tipo = "Bearer",
                expiraEmUtc = issued.ExpiresAtUtc,
                aprovado = user.Allowed,
                aviso = user.Allowed
                    ? (string?)null
                    : "Sua conta ainda não foi aprovada. O acesso aos endpoints protegidos está bloqueado até a liberação pelo responsável.",
            });
        })
        .WithName("Login");
    }
}