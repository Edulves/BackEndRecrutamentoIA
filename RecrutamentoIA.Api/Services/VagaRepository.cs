using System.Text.Json;

namespace RecrutamentoIA.Api.Services;

/// <summary>Vaga cadastrada pelo recrutador. O Titulo é a chave usada no histórico dos candidatos.</summary>
public class VagaRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Titulo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public DateTime CriadaEmUtc { get; set; }
}

/// <summary>
/// Persistência das vagas em Data/vagas.json — mesmo espírito sem-banco do
/// candidatos.json. O título é único (normalizado), porque ele é a chave que
/// liga a vaga ao histórico de análises dos candidatos.
/// </summary>
public class VagaRepository
{
    /// <summary>Mesmo limite do título derivado em POST /api/analisar.</summary>
    public const int MaxTitulo = 70;

    private readonly string _file;
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public VagaRepository(string contentRootPath)
    {
        _file = Path.Combine(contentRootPath, "Data", "vagas.json");
    }

    public List<VagaRecord> ListAll()
    {
        lock (_lock) return Load();
    }

    /// <summary>
    /// Título normalizado com a MESMA derivação do título em POST /api/analisar
    /// (primeira linha não vazia, trim, corte em 70) — ele é a chave do histórico.
    /// </summary>
    public static string NormalizarTitulo(string titulo)
    {
        var t = titulo.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
        return t.Length > MaxTitulo ? t[..MaxTitulo] : t;
    }

    /// <summary>Já existe vaga com esse título (normalizado, sem diferenciar caixa)?</summary>
    public bool ExisteTitulo(string titulo)
    {
        var normalizado = NormalizarTitulo(titulo);
        lock (_lock)
            return Load().Any(v => string.Equals(v.Titulo, normalizado, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Cadastra a vaga. Retorna null se já existe vaga com o mesmo título.</summary>
    public VagaRecord? Add(string titulo, string descricao)
    {
        lock (_lock)
        {
            var todas = Load();
            var normalizado = NormalizarTitulo(titulo);
            if (todas.Any(v => string.Equals(v.Titulo, normalizado, StringComparison.OrdinalIgnoreCase)))
                return null;

            var vaga = new VagaRecord
            {
                Titulo = normalizado,
                Descricao = descricao.Trim(),
                CriadaEmUtc = DateTime.UtcNow,
            };
            todas.Add(vaga);
            Save(todas);
            return vaga;
        }
    }

    private List<VagaRecord> Load()
    {
        if (!File.Exists(_file)) return new List<VagaRecord>();
        var json = File.ReadAllText(_file);
        if (string.IsNullOrWhiteSpace(json)) return new List<VagaRecord>();
        return JsonSerializer.Deserialize<List<VagaRecord>>(json, JsonOpts) ?? new List<VagaRecord>();
    }

    private void Save(List<VagaRecord> todas)
    {
        // Escrita atômica: processo morto no meio não deixa JSON truncado.
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var tmp = _file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(todas, JsonOpts));
        File.Move(tmp, _file, overwrite: true);
    }
}
