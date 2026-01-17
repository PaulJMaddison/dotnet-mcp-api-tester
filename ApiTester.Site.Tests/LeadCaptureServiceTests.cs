using ApiTester.Site.Data;
using ApiTester.Site.Models;
using ApiTester.Site.Services;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.Site.Tests;

public class LeadCaptureServiceTests
{
    [Fact]
    public async Task SubmitAsync_ReturnsValidationErrors_WhenRequestIsInvalid()
    {
        var service = BuildService(out var store);

        var request = new LeadCaptureRequest
        {
            FirstName = "",
            LastName = "",
            Email = "not-an-email",
            Message = ""
        };

        var result = await service.SubmitAsync(request, default);

        Assert.False(result.IsAccepted);
        Assert.False(result.IsHoneypot);
        Assert.NotEmpty(result.Errors);

        var stored = await store.GetAllAsync(default);
        Assert.Empty(stored);
    }

    [Fact]
    public async Task SubmitAsync_BlocksHoneypotSubmissions()
    {
        var service = BuildService(out var store);

        var request = new LeadCaptureRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Message = "Interested in a demo.",
            Website = "https://spam.example.com"
        };

        var result = await service.SubmitAsync(request, default);

        Assert.False(result.IsAccepted);
        Assert.True(result.IsHoneypot);

        var stored = await store.GetAllAsync(default);
        Assert.Empty(stored);
    }

    [Fact]
    public async Task SubmitAsync_PersistsValidSubmissions()
    {
        var service = BuildService(out var store);

        var request = new LeadCaptureRequest
        {
            FirstName = "Maya",
            LastName = "Lin",
            Email = "maya@example.com",
            Company = "Northwind",
            Message = "We need help with regression coverage."
        };

        var result = await service.SubmitAsync(request, default);

        Assert.True(result.IsAccepted);
        Assert.False(result.IsHoneypot);

        var stored = await store.GetAllAsync(default);
        Assert.Single(stored);
        Assert.Equal("Maya", stored[0].FirstName);
        Assert.Equal("Lin", stored[0].LastName);
        Assert.Equal("maya@example.com", stored[0].Email);
        Assert.Equal("Northwind", stored[0].Company);
    }

    private static ILeadCaptureService BuildService(out ILeadCaptureStore store)
    {
        var options = new DbContextOptionsBuilder<LeadCaptureDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new LeadCaptureDbContext(options);
        store = new LeadCaptureStore(context);
        return new LeadCaptureService(store);
    }
}
