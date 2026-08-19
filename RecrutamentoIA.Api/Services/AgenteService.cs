using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RecrutamentoIA.Api.Models;

namespace RecrutamentoIA.Api.Services;

public interface IAgenteIAService
{
    Task<AnaliseResultado> AnalisarCurriculoAsync(string descricaoVaga, CurriculoTexto curriculo, CancellationToken ct = default);
}

/// <summary>
/// Agente de IA baseado no Google Gemini (REST API).
/// Configuração: appsettings.json -> "Gemini": { "ApiKey": "...", "Model": "gemini-1.5-flash" }
/// Chave gratuita: https://aistudio.google.com/app/apikey
/// </summary>
public class AgenteService : IAgenteIAService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<AgenteService> _logger;

    public AgenteService(HttpClient http, IConfiguration config, ILogger<AgenteService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

        public async Task<AnaliseResultado> AnalisarCurriculoAsync(string descricaoVaga, CurriculoTexto curriculo, CancellationToken ct = default)
    {
        var apiKey = _config["Claude:ApiKey"];
        var model = _config["Claude:Model"] ?? "claude-sonnet-5";

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "COLE_SUA_CHAVE_AQUI")
        {
            _logger.LogWarning("Api ApiKey não configurada. Usando análise MOCK.");
            return AnaliseMock(curriculo);
        }

        var prompt = MontarPrompt(descricaoVaga, curriculo.Texto);

        var payload = new
        {
            model,
            max_tokens = 2048,
            temperature = 0.2,
            system = "Você deve responder APENAS com um JSON válido, sem nenhum texto adicional antes ou depois, sem markdown e sem blocos de código (```).",
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var url = "https://api.anthropic.com/v1/messages";
        var json = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");

        try
        {
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Falha Claude {Status}: {Body}", resp.StatusCode, body);
                return ErroFallback(curriculo, $"Erro API IA: {resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);

            var texto = "";
            foreach (var bloco in doc.RootElement.GetProperty("content").EnumerateArray())
            {
                if (bloco.GetProperty("type").GetString() == "text")
                {
                    texto += bloco.GetProperty("text").GetString();
                }
            }

            return ParseResposta(texto, curriculo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção ao analisar currículo {Arquivo}", curriculo.NomeArquivo);
            return ErroFallback(curriculo, ex.Message);
        }
    }

    // public async Task<AnaliseResultado> AnalisarCurriculoAsync(string descricaoVaga, CurriculoTexto curriculo, CancellationToken ct = default)
    // {
    //     var apiKey = _config["Claude:ApiKey"];
    //     var model = _config["Claude:Model"] ?? "claude-sonnet-5";

    //     if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "COLE_SUA_CHAVE_AQUI")
    //     {
    //         _logger.LogWarning("Api ApiKey não configurada. Usando análise MOCK.");
    //         return AnaliseMock(curriculo);
    //     }

    //     var prompt = MontarPrompt(descricaoVaga, curriculo.Texto);

    //     var payload = new
    //     {
    //         contents = new[] { new { parts = new[] { new { text = prompt } } } },
    //         generationConfig = new { temperature = 0.2, responseMimeType = "application/json" }
    //     };

    //     var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
    //     var json = JsonSerializer.Serialize(payload);
    //     using var req = new HttpRequestMessage(HttpMethod.Post, url)
    //     {
    //         Content = new StringContent(json, Encoding.UTF8, "application/json")
    //     };

    //     try
    //     {
    //         using var resp = await _http.SendAsync(req, ct);
    //         var body = await resp.Content.ReadAsStringAsync(ct);

    //         if (!resp.IsSuccessStatusCode)
    //         {
    //             _logger.LogError("Falha Gemini {Status}: {Body}", resp.StatusCode, body);
    //             return ErroFallback(curriculo, $"Erro API IA: {resp.StatusCode}");
    //         }

    //         using var doc = JsonDocument.Parse(body);
    //         var texto = doc.RootElement
    //             .GetProperty("candidates")[0]
    //             .GetProperty("content")
    //             .GetProperty("parts")[0]
    //             .GetProperty("text")
    //             .GetString() ?? "";

    //         return ParseResposta(texto, curriculo);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Exceção ao analisar currículo {Arquivo}", curriculo.NomeArquivo);
    //         return ErroFallback(curriculo, ex.Message);
    //     }
    // }


    private static string MontarPrompt(string vaga, string curriculo)
    {
        if (curriculo.Length > 8000) curriculo = curriculo[..8000] + "...";

        return $$"""
        Você é um especialista sênior em recrutamento e seleção (R&S).
        Analise o currículo abaixo em relação à vaga descrita e retorne um JSON com a estrutura EXATA:

        {
          "nomeCandidato": "nome extraído do currículo, ou 'Não identificado'",
          "score": 0-100 (compatibilidade com a vaga),
          "resumo": "1-2 frases resumindo a aderência",
          "pontosFortes": ["item 1", "item 2"],
          "pontosFracos": ["item 1", "item 2"],
          "habilidadesIdentificadas": ["skill 1", "skill 2"]
        }

        Critérios do score:
        - 90-100: match excepcional
        - 70-89: forte candidato
        - 50-69: match parcial
        - 30-49: pouca aderência
        - 0-29: não recomendado

        === DESCRIÇÃO DA VAGA ===
        {{vaga}}

        === CURRÍCULO ===
        {{curriculo}}

        Retorne SOMENTE o JSON, sem markdown, sem texto adicional.
        """;
    }

    private static AnaliseResultado ParseResposta(string textoIA, CurriculoTexto curriculo)
    {
        var limpo = Regex.Replace(textoIA.Trim(), @"^```(json)?|```$", "", RegexOptions.Multiline).Trim();

        try
        {
            using var doc = JsonDocument.Parse(limpo);
            var root = doc.RootElement;

            return new AnaliseResultado(
                NomeArquivo: curriculo.NomeArquivo,
                NomeCandidato: root.TryGetProperty("nomeCandidato", out var n) ? n.GetString() ?? "Não identificado" : "Não identificado",
                Score: root.TryGetProperty("score", out var s) ? s.GetInt32() : 0,
                Resumo: root.TryGetProperty("resumo", out var r) ? r.GetString() ?? "" : "",
                PontosFortes: LerLista(root, "pontosFortes"),
                PontosFracos: LerLista(root, "pontosFracos"),
                HabilidadesIdentificadas: LerLista(root, "habilidadesIdentificadas")
            );
        }
        catch
        {
            return new AnaliseResultado(curriculo.NomeArquivo, "Erro no parse", 0,
                "Falha ao interpretar resposta da IA.", new(), new(), new());
        }
    }

    private static List<string> LerLista(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return new List<string>();
        return arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? "")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static AnaliseResultado ErroFallback(CurriculoTexto c, string erro) =>
        new(c.NomeArquivo, "Erro", 0, $"Falha na análise: {erro}", new(), new(), new());

    private static AnaliseResultado AnaliseMock(CurriculoTexto c)
    {
        var texto = c.Texto.ToLowerInvariant();
        var palavras = new[] { "c#", "python", "java", "sql", "react", "node", "docker", "aws", "azure", "scrum", "agile", "git" };
        var encontradas = palavras.Where(p => texto.Contains(p)).ToList();
        var score = Math.Min(100, encontradas.Count * 12 + 20);
        var nome = Path.GetFileNameWithoutExtension(c.NomeArquivo);

        return new AnaliseResultado(
            c.NomeArquivo, nome, score,
            "[MOCK] Configure Gemini:ApiKey em appsettings.json para análise real.",
            encontradas.Take(3).Select(p => $"Domina {p}").ToList(),
            new List<string> { "Análise real indisponível sem API Key" },
            encontradas
        );
    }
}
