using ApiTester.Web.Diff;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Models;

namespace ApiTester.Web.UnitTests;

public sealed class OpenApiDiffEngineTests
{
    [Fact]
    public void Diff_DetectsBreakingAndNonBreakingChanges()
    {
        var before = ReadSpec("spec-a.json");
        var after = ReadSpec("spec-b.json");

        var result = OpenApiDiffEngine.Diff(before, after);

        Assert.Contains(result.Items, item =>
            item.Classification == OpenApiDiffClassification.Breaking
            && item.Change == OpenApiDiffChange.PathRemoved
            && item.Path == "/users");

        Assert.Contains(result.Items, item =>
            item.Classification == OpenApiDiffClassification.NonBreaking
            && item.Change == OpenApiDiffChange.PathAdded
            && item.Path == "/status");

        Assert.Contains(result.Items, item =>
            item.Classification == OpenApiDiffClassification.NonBreaking
            && item.Change == OpenApiDiffChange.MethodAdded
            && item.Path == "/pets"
            && item.Method == "POST");

        Assert.Contains(result.Items, item =>
            item.Classification == OpenApiDiffClassification.Breaking
            && item.Change == OpenApiDiffChange.ParameterRemoved
            && item.Path == "/pets"
            && item.Method == "GET");

        Assert.Contains(result.Items, item =>
            item.Classification == OpenApiDiffClassification.Breaking
            && item.Change == OpenApiDiffChange.ParameterAdded
            && item.Path == "/pets"
            && item.Method == "GET");

        Assert.Contains(result.Items, item =>
            item.Classification == OpenApiDiffClassification.Breaking
            && item.Change == OpenApiDiffChange.ParameterRequirednessChanged
            && item.Path == "/pets"
            && item.Method == "GET");

        Assert.Contains(result.Items, item =>
            item.Classification == OpenApiDiffClassification.NonBreaking
            && item.Change == OpenApiDiffChange.ParameterRequirednessChanged
            && item.Path == "/orders"
            && item.Method == "POST");

        Assert.Contains(result.Items, item =>
            item.Classification == OpenApiDiffClassification.Breaking
            && item.Change == OpenApiDiffChange.ResponseCodeRemoved
            && item.Path == "/pets"
            && item.Method == "GET");

        Assert.Contains(result.Items, item =>
            item.Classification == OpenApiDiffClassification.NonBreaking
            && item.Change == OpenApiDiffChange.ResponseCodeAdded
            && item.Path == "/pets"
            && item.Method == "GET");

        Assert.Contains(result.Items, item =>
            item.Classification == OpenApiDiffClassification.Breaking
            && item.Change == OpenApiDiffChange.ResponseSchemaChanged
            && item.Path == "/pets"
            && item.Method == "GET");
    }

    [Fact]
    public void Diff_WhenNoChanges_ReturnsInformational()
    {
        var before = ReadSpec("spec-a.json");
        var after = ReadSpec("spec-a.json");

        var result = OpenApiDiffEngine.Diff(before, after);

        Assert.Single(result.Items);
        Assert.Equal(OpenApiDiffClassification.Informational, result.Items[0].Classification);
        Assert.Equal(OpenApiDiffChange.NoChanges, result.Items[0].Change);
    }


    [Fact]
    public void Diff_WhenSchemaComparisonIsUncertain_ClassifiesAsInformational()
    {
        var before = BuildRecursiveResponseSpec(false);
        var after = BuildRecursiveResponseSpec(true);

        var result = OpenApiDiffEngine.Diff(before, after);

        Assert.Contains(result.Items, item =>
            item.Classification == OpenApiDiffClassification.Informational
            && item.Change == OpenApiDiffChange.ResponseSchemaChangedUncertain
            && item.Path == "/pets"
            && item.Method == "GET");
    }


    private static OpenApiDocument BuildRecursiveResponseSpec(bool includeExtraProperty)
    {
        var schema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>()
        };

        schema.Properties["name"] = new OpenApiSchema { Type = "string" };
        schema.Properties["child"] = schema;

        if (includeExtraProperty)
            schema.Properties["age"] = new OpenApiSchema { Type = "integer", Format = "int32" };

        return new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/pets"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new OpenApiOperation
                        {
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Description = "ok",
                                    Content = new Dictionary<string, OpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType
                                        {
                                            Schema = schema
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static Microsoft.OpenApi.Models.OpenApiDocument ReadSpec(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OpenApi", fileName);
        var json = File.ReadAllText(path);
        var reader = new OpenApiStringReader();
        var document = reader.Read(json, out _);
        Assert.NotNull(document);
        return document!;
    }
}
