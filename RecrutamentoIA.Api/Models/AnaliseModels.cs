namespace RecrutamentoIA.Api.Models;

public record CurriculoTexto(string NomeArquivo, string Texto);

public record AnaliseResultado(
    string NomeArquivo,
    string NomeCandidato,
    int Score,
    string Resumo,
    List<string> PontosFortes,
    List<string> PontosFracos,
    List<string> HabilidadesIdentificadas
);

public record AnaliseResponse(
    string DescricaoVaga,
    int TotalCurriculos,
    List<AnaliseResultado> Ranking,
    DateTime ProcessadoEm
);
