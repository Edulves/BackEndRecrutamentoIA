using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace RecrutamentoIA.Api.Services;

/// <summary>Resultado de extração: texto do currículo + foto extraída (se houver).</summary>
public record Extracao(string Texto, (byte[] Bytes, string Extensao, string ContentType)? Foto);

public interface IExtracaoTextoService
{
    /// <summary>Extrai texto e tenta extrair foto numa única passada (parse único do arquivo).</summary>
    Task<Extracao> ExtrairAsync(IFormFile arquivo, CancellationToken ct = default);
    
    /// <summary>Apenas compatibilidade: retorna só o texto (sem foto).</summary>
    Task<string> ExtrairApenasTextoAsync(IFormFile arquivo, CancellationToken ct = default);
}

public class ExtracaoTextoService : IExtracaoTextoService
{
    public async Task<Extracao> ExtrairAsync(IFormFile arquivo, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, ct);
        ms.Position = 0;

        return ext switch
        {
            ".pdf" => ExtrairPdf(ms),
            ".docx" => ExtrairDocx(ms),
            ".txt" => await ExtrairTxtAsync(ms, ct),
            _ => throw new NotSupportedException($"Formato '{ext}' não suportado. Use PDF, DOCX ou TXT.")
        };
    }

    public async Task<string> ExtrairApenasTextoAsync(IFormFile arquivo, CancellationToken ct = default)
    {
        var extracao = await ExtrairAsync(arquivo, ct);
        return extracao.Texto;
    }

    private static Extracao ExtrairPdf(MemoryStream ms)
    {
        using var pdf = PdfDocument.Open(ms);
        var sb = new System.Text.StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        var texto = sb.ToString();
        ms.Position = 0;
        var foto = ExtrairFotoPdf(ms);
        return new Extracao(texto, foto);
    }

    private static Extracao ExtrairDocx(MemoryStream ms)
    {
        using var doc = WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        var texto = body?.InnerText ?? string.Empty;
        ms.Position = 0;
        var foto = ExtrairFotoDocx(ms);
        return new Extracao(texto, foto);
    }

    private static async Task<Extracao> ExtrairTxtAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream);
        var texto = await reader.ReadToEndAsync(ct);
        return new Extracao(texto, null);
    }

    private static (byte[], string, string)? ExtrairFotoPdf(MemoryStream ms)
    {
        try
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
        catch { return null; }
    }

    private static (byte[], string, string)? ExtrairFotoDocx(MemoryStream ms)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(ms, false);
            var main = doc.MainDocumentPart;
            if (main is null) return null;
            ImagePart? melhor = null;
            long melhorTamanho = 0;
            foreach (var parte in main.ImageParts)
            {
                using var s = parte.GetStream();
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
        catch { return null; }
    }

    private static bool BytesSaoImagem(byte[] bytes, string contentType)
    {
        if (bytes.Length < 4) return false;
        return contentType switch
        {
            "image/jpeg" => bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/png" => bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
            _ => false
        };
    }
}
