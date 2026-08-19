using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace RecrutamentoIA.Api.Services;

public interface IExtracaoTextoService
{
    Task<string> ExtrairAsync(IFormFile arquivo, CancellationToken ct = default);
}

public class ExtracaoTextoService : IExtracaoTextoService
{
    public async Task<string> ExtrairAsync(IFormFile arquivo, CancellationToken ct = default)
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

    private static string ExtrairPdf(Stream stream)
    {
        using var pdf = PdfDocument.Open(stream);
        var sb = new System.Text.StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    private static string ExtrairDocx(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        return body?.InnerText ?? string.Empty;
    }

    private static async Task<string> ExtrairTxtAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }
}
