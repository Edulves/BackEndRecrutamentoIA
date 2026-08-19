using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RecrutamentoIA.Api.Filters;

/// <summary>
/// Adiciona descrições aos campos do corpo multipart/form-data dos endpoints de análise.
/// </summary>
public class MultipartDocumentationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var requestBody = operation.RequestBody;
        if (requestBody?.Content is null)
            return;

        foreach (var mediaType in requestBody.Content.Values)
        {
            var schema = mediaType.Schema;
            if (schema?.Properties is null)
                continue;

            foreach (var property in schema.Properties)
            {
                switch (property.Key)
                {
                    case "descricaoVaga":
                        property.Value.Description =
                            "Descrição da vaga (texto) usada pela IA para comparar com os currículos. Campo obrigatório.";
                        break;
                    case "curriculos":
                        property.Value.Description =
                            "Arquivos de currículo (PDF, DOCX, DOC ou TXT). Envie de 1 a 30 arquivos no campo 'curriculos'.";
                        break;
                }
            }
        }
    }
}
