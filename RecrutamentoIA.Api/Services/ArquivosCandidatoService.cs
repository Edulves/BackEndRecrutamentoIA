using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace RecrutamentoIA.Api.Services;

/// <summary>
/// Guarda o arquivo original do currículo e a foto de perfil do candidato em
/// Data/curriculos e Data/fotos — mesmo espírito sem-banco dos JSONs.
/// Os nomes de arquivo são sempre derivados do Id do candidato (nunca do nome
/// enviado), então não há traversal de caminho.
/// </summary>
public class ArquivosCandidatoService
{
    private readonly string _dirCurriculos;
    private readonly string _dirFotos;
    // ponytail: uma trava só para todas as escritas de arquivo (delete+write nunca
    // intercalam entre requisições); trava por candidato se algum dia virar gargalo
    private readonly object _lockArquivos = new();

    public ArquivosCandidatoService(string contentRootPath)
    {
        _dirCurriculos = Path.Combine(contentRootPath, "Data", "curriculos");
        _dirFotos = Path.Combine(contentRootPath, "Data", "fotos");
    }

    public static string? ContentTypeCurriculo(string nomeArquivo) =>
        Path.GetExtension(nomeArquivo).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain; charset=utf-8",
            _ => null,
        };

    /// <summary>
    /// Salva o arquivo original do currículo; retorna o nome salvo (Id + extensão).
    /// Só aceita as extensões da whitelist (as mesmas de ContentTypeCurriculo).
    /// Otimizado: remove varredura de Directory.GetFiles(), deleta arquivos antigos diretamente.
    /// </summary>
    public async Task<string> SalvarCurriculoAsync(string candidatoId, IFormFile arquivo, CancellationToken ct)
    {
        if (ContentTypeCurriculo(arquivo.FileName) is null)
            throw new NotSupportedException($"Extensão não suportada para armazenamento: '{arquivo.FileName}'.");

        var destino = candidatoId + Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        lock (_lockArquivos)
        {
            Directory.CreateDirectory(_dirCurriculos);
            var caminhoDestino = Path.Combine(_dirCurriculos, destino);
            
            // Remove arquivos antigos com outras extensões (pdf -> docx)
            // Sem varredura com padrão: deleta extensões conhecidas que não sejam a atual
            foreach (var ext in new[] { ".pdf", ".docx", ".txt" })
            {
                if (ext.Equals(Path.GetExtension(destino), StringComparison.OrdinalIgnoreCase))
                    continue;
                var antigo = Path.Combine(_dirCurriculos, candidatoId + ext);
                if (File.Exists(antigo))
                    File.Delete(antigo);
            }
            File.WriteAllBytes(caminhoDestino, bytes);
        }
        return destino;
    }

    /// <summary>Salva a foto de perfil, removendo foto anterior de outra extensão. Otimizado: sem Directory.GetFiles().</summary>
    public string SalvarFoto(string candidatoId, byte[] bytes, string extensao)
    {
        var destino = candidatoId + extensao;
        lock (_lockArquivos)
        {
            Directory.CreateDirectory(_dirFotos);
            // Remove fotos antigas com outras extensões
            foreach (var ext in new[] { ".jpg", ".png", ".gif", ".bmp", ".webp" })
            {
                if (ext.Equals(extensao, StringComparison.OrdinalIgnoreCase))
                    continue;
                var antiga = Path.Combine(_dirFotos, candidatoId + ext);
                if (File.Exists(antiga))
                    File.Delete(antiga);
            }
            File.WriteAllBytes(Path.Combine(_dirFotos, destino), bytes);
        }
        return destino;
    }

    /// <summary>Apaga o currículo e a foto armazenados do candidato, se existirem.</summary>
    public void RemoverArquivos(string candidatoId)
    {
        lock (_lockArquivos)
        {
            foreach (var dir in new[] { _dirCurriculos, _dirFotos })
                if (Directory.Exists(dir))
                    foreach (var arquivo in Directory.GetFiles(dir, candidatoId + ".*"))
                        File.Delete(arquivo);
        }
    }

    /// <summary>Confere a assinatura binária contra o tipo declarado (JPEG/PNG/WEBP).</summary>
    public static bool BytesSaoImagem(byte[] b, string contentType) => contentType switch
    {
        "image/jpeg" => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
        "image/png" => b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47,
        "image/webp" => b.Length > 12 &&
            b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F' &&
            b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P',
        _ => false,
    };

    public string CaminhoCurriculo(string arquivoSalvo) => Path.Combine(_dirCurriculos, arquivoSalvo);
    public string CaminhoFoto(string arquivoSalvo) => Path.Combine(_dirFotos, arquivoSalvo);

    /// <summary>
    /// Tenta extrair a foto de perfil do currículo. Retorna null quando não há
    /// imagem plausível — estado válido; a extração nunca derruba a importação.
    /// </summary>
    public (byte[] Bytes, string Extensao, string ContentType)? ExtrairFoto(IFormFile arquivo)
    {
        try
        {
            var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            using var ms = new MemoryStream();
            using (var stream = arquivo.OpenReadStream())
                stream.CopyTo(ms);
            ms.Position = 0;
            return ext switch
            {
                ".pdf" => ExtrairFotoPdf(ms),
                ".docx" => ExtrairFotoDocx(ms),
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    // ponytail: heurística — maior imagem de proporção ~retrato nas 2 primeiras
    // páginas; logos/ícones costumam ser pequenos ou muito largos. Se errar,
    // o upload manual de foto corrige.
    private static (byte[], string, string)? ExtrairFotoPdf(MemoryStream ms)
    {
        using var pdf = PdfDocument.Open(ms);
        byte[]? melhor = null;
        var extensao = ".jpg";
        var contentType = "image/jpeg";
        double melhorArea = 0;
        foreach (var page in pdf.GetPages().Take(2))
        {
            foreach (var img in page.GetImages())
            {
                var w = img.Bounds.Width;
                var h = img.Bounds.Height;
                if (w < 40 || h < 40) continue;
                var proporcao = w / h;
                if (proporcao < 0.5 || proporcao > 1.4) continue;
                var area = w * h;
                if (area <= melhorArea) continue;

                if (img.TryGetPng(out var png))
                {
                    melhor = png;
                    extensao = ".png";
                    contentType = "image/png";
                    melhorArea = area;
                }
                else
                {
                    // RawBytes só é imagem pronta quando o filtro do PDF é DCTDecode
                    // (JPEG); JPEG2000/Flate viram lixo — confere a assinatura.
                    var raw = img.RawBytes.ToArray();
                    if (BytesSaoImagem(raw, "image/jpeg"))
                    {
                        melhor = raw;
                        extensao = ".jpg";
                        contentType = "image/jpeg";
                        melhorArea = area;
                    }
                }
            }
        }
        return melhor is null ? null : (melhor, extensao, contentType);
    }

    private static (byte[], string, string)? ExtrairFotoDocx(MemoryStream ms)
    {
        using var doc = WordprocessingDocument.Open(ms, false);
        var main = doc.MainDocumentPart;
        if (main is null) return null;

        ImagePart? melhor = null;
        long melhorTamanho = 0;
        foreach (var parte in main.ImageParts)
        {
            using var s = parte.GetStream();
            // imagens minúsculas (< 4 KB) são quase sempre ícones/linhas decorativas
            if (s.Length > melhorTamanho && s.Length > 4_000)
            {
                melhor = parte;
                melhorTamanho = s.Length;
            }
        }
        if (melhor is null) return null;

        using var origem = melhor.GetStream();
        using var buf = new MemoryStream();
        origem.CopyTo(buf);
        var contentType = melhor.ContentType;
        var extensao = contentType switch
        {
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            _ => ".jpg",
        };
        return (buf.ToArray(), extensao, contentType);
    }
}
