namespace RecrutamentoIA.Api.Auth;

/// <summary>Body do endpoint de permissões (aprovar/suspender um usuário).</summary>
public record AlterarPermissaoRequest(bool Allowed);

/// <summary>
/// Endpoints de administração de acesso: listar usuários e aprovar/suspender contas.
///
/// Regras:
/// - Exigem autenticação (JWT válido).
/// - Somente um usuário JÁ aprovado (<c>allowed = true</c>) pode gerenciar outros.
/// - Ninguém pode alterar a própria permissão pela API (evita auto-aprovação);
///   o primeiro usuário é liberado manualmente editando <c>Data/users.csv</c>.
/// </summary>
public static class UsuariosEndpoints
{
    public static void MapUsuariosEndpoints(this WebApplication app)
    {
        // ── Lista todos os usuários e o estado de aprovação de cada um ──
        app.MapGet("/api/usuarios", (HttpContext context, UserRepository users) =>
        {
            if (ResponsavelAprovado(context, users) is not { } responsavel)
                return Results.Forbid();

            var usuarios = users.List()
                .OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
                .Select(u => new
                {
                    username = u.Username,
                    aprovado = u.Allowed,
                    criadoEmUtc = u.CreatedAtUtc,
                })
                .ToList();

            return Results.Ok(new
            {
                total = usuarios.Count,
                solicitante = responsavel.Username,
                usuarios,
            });
        })
        .RequireAuthorization()
        .WithName("ListarUsuarios");

        // ── Aprovar / suspender o acesso de um usuário ──
        // app.MapPost("/api/usuarios/{username}/permissoes",
        //     (HttpContext ctx, string username, AlterarPermissaoRequest request, UserRepository users) =>
        // {
        //     if (ResponsavelAprovado(ctx, users) is not { } responsavel)
        //         return Results.Forbid();

        //     var alvo = (username ?? string.Empty).Trim();
        //     if (string.IsNullOrEmpty(alvo))
        //         return Results.BadRequest(new { erro = "Informe o nome do usuário na rota." });
        //     if (string.Equals(responsavel.Username, alvo, StringComparison.OrdinalIgnoreCase))
        //         return Results.BadRequest(new
        //         {
        //             erro = "Você não pode alterar a própria permissão pela API. Para isso, edite manualmente Data/users.csv (campo 'allowed').",
        //         });

        //     if (!users.SetAllowed(alvo, request.Allowed))
        //         return Results.NotFound(new { erro = $"Usuário '{alvo}' não encontrado." });

        //     return Results.Ok(new
        //     {
        //         mensagem = request.Allowed
        //             ? $"Acesso de '{alvo}' aprovado. Ele já pode usar o sistema."
        //             : $"Acesso de '{alvo}' suspenso. Ele não pode mais usar o sistema.",
        //         username = alvo,
        //         aprovado = request.Allowed,
        //     });
        // })
        // .RequireAuthorization()
        // .WithName("AlterarPermissaoUsuario");
    }

    /// <summary>
    /// Retorna o usuário autenticado que está APROVADO (pode administrar), ou <c>null</c>.
    /// </summary>
    private static UserRecord? ResponsavelAprovado(HttpContext ctx, UserRepository users)
    {
        var user = users.FindByUsername(ctx.User.Identity?.Name ?? string.Empty);
        return user is { Allowed: true } ? user : null;
    }
}