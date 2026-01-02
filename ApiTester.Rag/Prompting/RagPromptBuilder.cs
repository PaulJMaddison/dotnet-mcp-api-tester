using System.Text;
using ApiTester.Rag.Models;

namespace ApiTester.Rag.Prompting;

public sealed class RagPromptBuilder
{
    public string SystemPrompt =>
        """
You are an assistant for an API testing platform.

You MUST answer using only the evidence snippets provided.
If the evidence is insufficient, say you do not know and state exactly what evidence is missing.

Do NOT invent endpoints, parameters, request bodies, response fields, authentication, error codes, or behaviour.
If a user asks about something not present in evidence (e.g. filtering by city), you must say it is not defined in the spec.

When you make a claim, include citations like [chunk:ChunkId] immediately after the sentence or bullet.
Prefer short, practical, developer-friendly answers.

Output format (use these headings exactly):
1) Summary
2) Endpoint
3) Request examples (curl, .NET 8)
4) Response shape
5) Auth and errors
6) Notes
""";

    public string BuildUserPrompt(string question, IReadOnlyList<RagRetrievedChunk> evidence)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Question:");
        sb.AppendLine(question.Trim());
        sb.AppendLine();

        sb.AppendLine("Evidence snippets (do not use anything else):");
        sb.AppendLine();

        foreach (var e in evidence)
        {
            sb.AppendLine($"[chunk:{e.Chunk.ChunkId}] (source:{e.Chunk.SourceType}/{e.Chunk.SourceId})");
            sb.AppendLine(e.Chunk.Text);
            sb.AppendLine();
        }

        sb.AppendLine("Now produce the answer.");
        sb.AppendLine("Remember: only evidence, and cite [chunk:...] for each claim.");
        sb.AppendLine();
        sb.AppendLine("If the question asks for code examples:");
        sb.AppendLine("- Use generic placeholders for base URL (e.g. https://api.example.com) unless the evidence provides a real one.");
        sb.AppendLine("- For auth: only show headers if the evidence specifies a scheme.");
        sb.AppendLine("- Do not add query parameters unless they exist in evidence.");

        return sb.ToString();
    }
}
