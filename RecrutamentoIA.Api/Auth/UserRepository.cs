using System.Globalization;
using System.Text;

namespace RecrutamentoIA.Api.Auth;

/// <summary>
/// Repositório de usuários sobre um arquivo CSV simples (<c>Data/users.csv</c>) — sem banco de dados.
/// Colunas: <c>username,passwordHash,salt,createdAtUtc</c>.
/// </summary>
public sealed class UserRepository
{
    public const string DirectoryName = "Data";
    public const string FileName = "users.csv";
    public const string CsvHeader = "username,passwordHash,salt,createdAtUtc";

    private readonly string _filePath;
    private readonly object _gate = new();

    public UserRepository(string contentRootPath)
    {
        _filePath = Path.Combine(contentRootPath, DirectoryName, FileName);
        EnsureFile();
    }

    /// <summary>Caminho absoluto do arquivo CSV usado pelo repositório.</summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Cria um novo usuário. Retorna <c>false</c> se o nome de usuário já existir
    /// (comparação sem diferenciar maiúsculas/minúsculas).
    /// </summary>
    public bool Create(UserRecord user)
    {
        lock (_gate)
        {
            if (FindByUsername(user.Username) is not null)
                return false;

            var line = string.Join(",",
                Escape(user.Username),
                Escape(user.PasswordHash),
                Escape(user.Salt),
                user.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture));

            File.AppendAllText(_filePath, line + Environment.NewLine);
            return true;
        }
    }

    public UserRecord? FindByUsername(string username)
    {
        var wanted = username?.Trim() ?? string.Empty;
        lock (_gate)
        {
            foreach (var line in ReadAllLines())
            {
                var user = ParseRow(line);
                if (user is not null &&
                    string.Equals(user.Username, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return user;
                }
            }
            return null;
        }
    }

    // ── Internos ──────────────────────────────────────────────────────────────────

    private IEnumerable<string> ReadAllLines()
        => File.Exists(_filePath) ? File.ReadAllLines(_filePath) : Array.Empty<string>();

    private void EnsureFile()
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(_filePath));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(_filePath))
            {
                File.WriteAllText(_filePath, CsvHeader + Environment.NewLine);
                return;
            }

            // Garante que o arquivo termina com quebra de linha para que o append
            // não "grude" a primeira linha de usuário no cabeçalho.
            var existing = File.ReadAllText(_filePath);
            if (existing.Length == 0)
            {
                File.WriteAllText(_filePath, CsvHeader + Environment.NewLine);
            }
            else if (!existing.EndsWith(Environment.NewLine, StringComparison.Ordinal))
            {
                File.AppendAllText(_filePath, Environment.NewLine);
            }
        }
    }

    private static UserRecord? ParseRow(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        if (line.TrimStart().StartsWith('#'))   // comentário permitido no CSV
            return null;

        var fields = SplitCsvLine(line);
        if (fields.Length < 3)
            return null;

        var username = fields[0].Trim();
        if (string.Equals(username, "username", StringComparison.OrdinalIgnoreCase)) // cabeçalho
            return null;

        var passwordHash = fields[1];
        var salt = fields[2];
        if (username.Length == 0 || passwordHash.Length == 0 || salt.Length == 0)
            return null;

        var createdAt = fields.Length > 3
            && DateTime.TryParse(fields[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : DateTime.UtcNow;

        return new UserRecord
        {
            Username = username,
            PasswordHash = passwordHash,
            Salt = salt,
            CreatedAtUtc = createdAt
        };
    }

    /// <summary>Faz escape de um valor para o formato CSV (aspas duplicadas e vírgulas entre aspas).</summary>
    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    /// <summary>Divide uma linha CSV respeitando campos entre aspas duplas.</summary>
    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }

        fields.Add(field.ToString());
        return fields.ToArray();
    }
}