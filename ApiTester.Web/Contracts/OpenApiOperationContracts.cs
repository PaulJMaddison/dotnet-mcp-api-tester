namespace ApiTester.Web.Contracts;

public sealed record OpenApiOperationListResponse(IReadOnlyList<OpenApiOperationSummaryDto> Operations);

public sealed record OpenApiOperationSummaryDto(
    string OperationId,
    string Method,
    string Path,
    string Summary,
    string Description,
    bool RequiresAuth);

public sealed record OpenApiOperationDescribeResponse(
    string OperationId,
    string Method,
    string Path,
    string Summary,
    string Description,
    bool RequiresAuth,
    IReadOnlyList<OpenApiOperationParameterDto> Parameters,
    OpenApiOperationRequestBodyDto? RequestBody,
    IReadOnlyDictionary<string, OpenApiOperationResponseDto> Responses);

public sealed record OpenApiOperationParameterDto(
    string Name,
    string In,
    bool Required,
    string Description,
    OpenApiSchemaDto? Schema);

public sealed record OpenApiOperationRequestBodyDto(
    bool Required,
    string Description,
    IReadOnlyDictionary<string, OpenApiOperationContentDto> Content);

public sealed record OpenApiOperationResponseDto(
    string Description,
    IReadOnlyDictionary<string, OpenApiOperationContentDto> Content);

public sealed record OpenApiOperationContentDto(OpenApiSchemaDto? Schema);

public sealed record OpenApiSchemaDto(
    string Type,
    string Format,
    bool Nullable,
    OpenApiSchemaItemDto? Items);

public sealed record OpenApiSchemaItemDto(string Type, string Format);
