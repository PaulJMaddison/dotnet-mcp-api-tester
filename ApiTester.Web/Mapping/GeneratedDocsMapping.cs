using System.Text;
using ApiTester.McpServer.Models;
using ApiTester.Web.AI;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Mapping;

public static class GeneratedDocsMapping
{
    public static GeneratedDocsResponse ToResponse(GeneratedDocsRecord record, AiDocsPayload payload)
    {
        var sections = payload.Sections.Select(section => new GeneratedDocsSectionDto(
            section.OperationId,
            section.Method,
            section.Path,
            section.Title,
            section.Summary,
            section.Markdown,
            section.Examples
                .Select(example => new GeneratedDocsExampleDto(
                    example.Title,
                    Guid.TryParse(example.RunId, out var runId) ? runId : Guid.Empty,
                    example.CaseName,
                    example.StatusCode,
                    example.ResponseSnippet))
                .ToList()))
            .ToList();

        return new GeneratedDocsResponse(
            record.ProjectId,
            record.SpecId,
            payload.Title,
            payload.Summary,
            sections,
            BuildMarkdown(payload),
            record.CreatedUtc,
            record.UpdatedUtc);
    }

    private static string BuildMarkdown(AiDocsPayload payload)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(payload.Title))
        {
            builder.AppendLine($"# {payload.Title}");
        }

        if (!string.IsNullOrWhiteSpace(payload.Summary))
        {
            if (builder.Length > 0)
                builder.AppendLine();

            builder.AppendLine(payload.Summary.Trim());
        }

        foreach (var section in payload.Sections)
        {
            if (string.IsNullOrWhiteSpace(section.Markdown))
                continue;

            if (builder.Length > 0)
                builder.AppendLine().AppendLine();

            builder.AppendLine(section.Markdown.Trim());
        }

        return builder.ToString();
    }
}
