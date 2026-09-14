using ApiTester.McpServer.Runtime;
using Microsoft.OpenApi.Models;

namespace ApiTester.McpServer.Services;

public sealed class OpenApiStore
{
    private readonly object _gate = new();
    private readonly ProjectContext? _projectContext;
    private OpenApiDocument? _document;
    private string? _source;
    private Guid? _projectId;

    public OpenApiStore(ProjectContext? projectContext = null)
    {
        _projectContext = projectContext;
    }

    public OpenApiDocument? Document
    {
        get
        {
            lock (_gate)
            {
                if (_projectContext?.CurrentProjectId is Guid current && _projectId != current)
                    return null;
                return _document;
            }
        }
    }

    public string? Source
    {
        get
        {
            lock (_gate)
            {
                if (_projectContext?.CurrentProjectId is Guid current && _projectId != current)
                    return null;
                return _source;
            }
        }
    }

    public Guid? ProjectId
    {
        get { lock (_gate) return _projectId; }
    }

    public void SetDocument(OpenApiDocument doc, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var projectId = _projectContext?.CurrentProjectId;
        SetDocument(projectId, doc, source);
    }

    public void SetDocument(Guid projectId, OpenApiDocument doc, string? source = null)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("projectId must not be empty.", nameof(projectId));
        SetDocument((Guid?)projectId, doc, source);
    }

    private void SetDocument(Guid? projectId, OpenApiDocument doc, string? source)
    {
        ArgumentNullException.ThrowIfNull(doc);
        lock (_gate)
        {
            _document = doc;
            _source = source;
            _projectId = projectId;
        }
    }

    public OpenApiDocument RequireDocument()
    {
        lock (_gate)
        {
            if (_document is null)
                throw new InvalidOperationException("No OpenAPI document loaded. Call api_import_open_api first.");

            if (_projectContext?.CurrentProjectId is Guid current && _projectId != current)
                throw new InvalidOperationException("No OpenAPI document is loaded for the current project. Import a specification for this project first.");

            return _document;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _document = null;
            _source = null;
            _projectId = null;
        }
    }

    public bool HasDocument
    {
        get
        {
            lock (_gate)
            {
                if (_document is null) return false;
                return _projectContext?.CurrentProjectId is not Guid current || _projectId == current;
            }
        }
    }
}
