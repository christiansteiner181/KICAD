using Microsoft.AspNetCore.Mvc.Testing;

namespace Hlks.Web.Tests;

public class Rauchtest(WebApplicationFactory<Program> fabrik) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task StartseiteUndStatusAntworten()
    {
        var c = fabrik.CreateClient();
        var html = await c.GetStringAsync("/");
        Assert.Contains("HLKS-CAD", html);
        var status = await c.GetStringAsync("/api/status");
        Assert.Contains("\"offen\"", status);
    }
}
