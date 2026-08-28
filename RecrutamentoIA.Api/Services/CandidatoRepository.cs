using System.Text;
using System.Text.Json;
using RecrutamentoIA.Api.Models;

namespace RecrutamentoIA.Api.Services;

/// <summary>Uma passagem do candidato por uma análise (histórico de vagas × score).</summary>
public record AnaliseHistorico(DateTime DataUtc, string Vaga, int Score);

/// <summary>
/// Perfil persistido do candidato, montado a partir de cada currículo analisado.
/// AreasAptidao responde "para o que esse candidato é bom" (ex.: Administrativo),
/// independentemente da vaga que originou a análise.
/// </summary>
public class CandidatoRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nome { get; set; } = "";
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public string? Cidade { get; set; }
    public List<string> AreasAptidao { get; set; } = new();
    public List<string> Habilidades { get; set; } = new();
    public List<string> Experiencias { get; set; } = new();
    public List<string> Formacao { get; set; } = new();
    public string Resumo { get; set; } = "";
    public List<string> PontosFortes { get; set; } = new();
    public List<string> PontosFracos { get; set; } = new();
    public string NomeArquivo { get; set; } = "";
    // Arquivo original do currículo (Data/curriculos) e foto de perfil (Data/fotos);
    // null nos candidatos importados antes do armazenamento de arquivos.
    public string? CurriculoArquivo { get; set; }
    public string? CurriculoContentType { get; set; }
    public string? FotoArquivo { get; set; }
    public string? FotoContentType { get; set; }
    public DateTime CriadoEmUtc { get; set; }
    public DateTime AtualizadoEmUtc { get; set; }
    public List<AnaliseHistorico> Historico { get; set; } = new();

    /// <summary>Currículo sintetizado a partir do perfil salvo, para reanálise contra vagas novas.</summary>
    public string PerfilTexto()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Nome: {Nome}");
        if (!string.IsNullOrWhiteSpace(Cidade)) sb.AppendLine($"Cidade: {Cidade}");
        if (!string.IsNullOrWhiteSpace(Email)) sb.AppendLine($"E-mail: {Email}");
        if (!string.IsNullOrWhiteSpace(Telefone)) sb.AppendLine($"Telefone: {Telefone}");
        if (!string.IsNullOrWhiteSpace(Resumo)) sb.AppendLine($"Resumo: {Resumo}");
        Secao(sb, "Áreas de aptidão", AreasAptidao);
        Secao(sb, "Habilidades", Habilidades);
        Secao(sb, "Experiências", Experiencias);
        Secao(sb, "Formação", Formacao);
        Secao(sb, "Pontos fortes", PontosFortes);
        Secao(sb, "Pontos de atenção", PontosFracos);
        return sb.ToString();
    }

    private static void Secao(StringBuilder sb, string titulo, List<string> itens)
    {
        if (itens.Count == 0) return;
        sb.AppendLine($"{titulo}:");
        foreach (var item in itens) sb.AppendLine($"- {item}");
    }
}

/// <summary>
/// Persistência dos perfis de candidatos em Data/candidatos.json — mesmo espírito
/// sem-banco do users.csv. Reimportar o mesmo candidato atualiza o perfil
/// (dedupe por e-mail; sem e-mail, por nome normalizado).
/// </summary>
public class CandidatoRepository
{
    private readonly string _file;
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true, // aceita arquivos antigos/editados com "Id" em PascalCase
        WriteIndented = true,
    };

    public CandidatoRepository(string contentRootPath)
    {
        _file = Path.Combine(contentRootPath, "Data", "candidatos.json");
    }

    public string FilePath => _file;

    public List<CandidatoRecord> ListAll()
    {
        lock (_lock) return Load();
    }

    /// <summary>Cria ou atualiza o perfil a partir do resultado de uma análise; retorna o registro salvo.</summary>
    public CandidatoRecord Upsert(AnaliseResultado r, string vagaTitulo)
    {
        lock (_lock)
        {
            var todos = Load();
            var atual = todos.FirstOrDefault(c => MesmaPessoa(r, c));
            var agora = DateTime.UtcNow;

            if (atual is null)
            {
                atual = new CandidatoRecord { CriadoEmUtc = agora };
                todos.Add(atual);
            }

            // Campos novos sobrescrevem quando vierem preenchidos; senão mantém o que havia.
            atual.Nome = PreferirNovo(r.NomeCandidato, atual.Nome) ?? "";
            atual.Email = PreferirNovo(r.Email, atual.Email);
            atual.Telefone = PreferirNovo(r.Telefone, atual.Telefone);
            atual.Cidade = PreferirNovo(r.Cidade, atual.Cidade);
            if (r.AreasAptidao is { Count: > 0 }) atual.AreasAptidao = r.AreasAptidao;
            if (r.HabilidadesIdentificadas.Count > 0) atual.Habilidades = r.HabilidadesIdentificadas;
            if (r.Experiencias is { Count: > 0 }) atual.Experiencias = r.Experiencias;
            if (r.Formacao is { Count: > 0 }) atual.Formacao = r.Formacao;
            atual.Resumo = PreferirNovo(r.Resumo, atual.Resumo) ?? "";
            if (r.PontosFortes.Count > 0) atual.PontosFortes = r.PontosFortes;
            if (r.PontosFracos.Count > 0) atual.PontosFracos = r.PontosFracos;
            atual.NomeArquivo = PreferirNovo(r.NomeArquivo, atual.NomeArquivo) ?? "";
            atual.AtualizadoEmUtc = agora;
            atual.Historico.Add(new AnaliseHistorico(agora, vagaTitulo, r.Score));

            Save(todos);
            return atual;
        }
    }

    /// <summary>
    /// Atualiza as referências de arquivo do candidato (currículo/foto). Campos
    /// null são mantidos como estão — trocar a foto não apaga o currículo.
    /// </summary>
    public void AtualizarArquivos(string candidatoId, string? curriculoArquivo, string? curriculoContentType, string? fotoArquivo, string? fotoContentType)
    {
        lock (_lock)
        {
            var todos = Load();
            var atual = todos.FirstOrDefault(c => c.Id == candidatoId);
            if (atual is null) return;
            if (curriculoArquivo is not null)
            {
                atual.CurriculoArquivo = curriculoArquivo;
                atual.CurriculoContentType = curriculoContentType;
            }
            if (fotoArquivo is not null)
            {
                atual.FotoArquivo = fotoArquivo;
                atual.FotoContentType = fotoContentType;
            }
            Save(todos);
        }
    }

    /// <summary>
    /// Anexa análises ao histórico numa gravação só, sem mexer no perfil (usado no
    /// cadastro de vagas). Retorna quantas foram gravadas — menos que o enviado
    /// significa candidato não encontrado (o chamador loga).
    /// </summary>
    public int RegistrarAnalises(IEnumerable<(string CandidatoId, string VagaTitulo, int Score)> analises)
    {
        lock (_lock)
        {
            var todos = Load();
            var agora = DateTime.UtcNow;
            var gravadas = 0;
            foreach (var (id, vaga, score) in analises)
            {
                var atual = todos.FirstOrDefault(c => c.Id == id);
                if (atual is null) continue;
                atual.Historico.Add(new AnaliseHistorico(agora, vaga, score));
                atual.AtualizadoEmUtc = agora;
                gravadas++;
            }
            if (gravadas > 0) Save(todos);
            return gravadas;
        }
    }

    private static string? PreferirNovo(string? novo, string? antigo) =>
        string.IsNullOrWhiteSpace(novo) ? antigo : novo.Trim();

    // ── Dedupe: o mesmo currículo/pessoa nunca vira dois cadastros ────────
    // Casa por QUALQUER identificador — e-mail, nome completo normalizado ou
    // telefone normalizado — para que um currículo reimportado (com ou sem
    // e-mail, com o telefone escrito diferente) atualize o perfil existente.

    private static bool MesmaPessoa(AnaliseResultado r, CandidatoRecord c)
    {
        var email = ChaveEmail(r.Email);
        if (email is not null && email == ChaveEmail(c.Email)) return true;

        var nome = ChaveNome(r.NomeCandidato);
        if (nome is not null && nome == ChaveNome(c.Nome)) return true;

        var tel = ChaveTelefone(r.Telefone);
        var telExistente = ChaveTelefone(c.Telefone);
        // ponytail: casa também por sufixo (com/sem DDD); se um dia colidir
        // entre DDDs, exigir comparação com DDD completo
        if (tel is not null && telExistente is not null &&
            (tel == telExistente || tel.EndsWith(telExistente) || telExistente.EndsWith(tel)))
            return true;

        // Sem nenhum identificador extraído, o nome do arquivo é o que resta.
        if (email is null && nome is null && tel is null)
            return string.Equals(r.NomeArquivo?.Trim(), c.NomeArquivo?.Trim(), StringComparison.OrdinalIgnoreCase);

        return false;
    }

    private static string? ChaveEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Contains('@')
            ? email.Trim().ToLowerInvariant()
            : null;

    private static string? ChaveNome(string? nome)
    {
        var n = (nome ?? "").Trim().ToLowerInvariant();
        if (n.Length == 0 || n == "não identificado") return null;
        return string.Join(' ', n.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Só dígitos, sem o código do país; null se curto demais para identificar.</summary>
    private static string? ChaveTelefone(string? telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return null;
        var digitos = new string(telefone.Where(char.IsDigit).ToArray());
        if (digitos.StartsWith("55") && digitos.Length > 11) digitos = digitos[2..];
        return digitos.Length >= 8 ? digitos : null;
    }

    private List<CandidatoRecord> Load()
    {
        if (!File.Exists(_file)) return new List<CandidatoRecord>();
        var json = File.ReadAllText(_file);
        if (string.IsNullOrWhiteSpace(json)) return new List<CandidatoRecord>();
        return JsonSerializer.Deserialize<List<CandidatoRecord>>(json, JsonOpts) ?? new List<CandidatoRecord>();
    }

    private void Save(List<CandidatoRecord> todos)
    {
        // Escrita atômica: processo morto no meio não deixa JSON truncado.
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var tmp = _file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(todos, JsonOpts));
        File.Move(tmp, _file, overwrite: true);
    }
}
