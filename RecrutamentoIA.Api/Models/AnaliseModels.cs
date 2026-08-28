namespace RecrutamentoIA.Api.Models;

public record CurriculoTexto(string NomeArquivo, string Texto);

public record AnaliseResultado(
    string NomeArquivo,
    string NomeCandidato,
    int Score,
    string Resumo,
    List<string> PontosFortes,
    List<string> PontosFracos,
    List<string> HabilidadesIdentificadas,
    // Dados de perfil extraídos do currículo (independem da vaga analisada).
    List<string>? AreasAptidao = null,
    string? Email = null,
    string? Telefone = null,
    string? Cidade = null,
    List<string>? Experiencias = null,
    List<string>? Formacao = null
);

public record AnaliseResponse(
    string DescricaoVaga,
    int TotalCurriculos,
    List<AnaliseResultado> Ranking,
    DateTime ProcessadoEm
);

// ── Cadastro de vagas ────────────────────────────────────────────────────

public record CadastroVagaRequest(string Titulo, string Descricao);

/// <summary>Posição de um candidato do cadastro na análise contra uma vaga nova.</summary>
public record VagaRankingItem(string CandidatoId, string Nome, int Score, string Resumo);
