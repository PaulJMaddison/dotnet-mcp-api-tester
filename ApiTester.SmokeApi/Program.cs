var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/pets", () => Results.Ok(new
{
    pets = new[]
    {
        new { id = 1, name = "Milo", species = "cat" },
        new { id = 2, name = "Rex", species = "dog" }
    }
}));

app.MapGet("/api/pets/{id:int}", (int id) =>
{
    if (id == 1)
        return Results.Ok(new { id = 1, name = "Milo", species = "cat" });
    if (id == 2)
        return Results.Ok(new { id = 2, name = "Rex", species = "dog" });

    return Results.NotFound(new { message = "pet not found" });
});

app.Run();
