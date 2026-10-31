using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

namespace Zydis.Viewer.Host.Tests;

// The compare dialog of the viewer reads its base through these routes when the local host serves it, so a change to a
// gate or a route would reach that dialog with nothing else failing.
public sealed class HostEndpointTests :
    IDisposable
{
    // An empty web root keeps the static file middleware in front of the routes, as in a host that serves the app,
    // while no file of a web build can answer in place of a route.
    private readonly DirectoryInfo _webRoot = Directory.CreateTempSubdirectory("zydis-viewer-host-tests-");

    public void Dispose()
    {
        _webRoot.Delete(recursive: true);
    }

    [Fact]
    public async Task ShowRejectsAnOptionBeforeGitRuns()
    {
        using var response = await GetAsync("/api/git/show?rev=-x");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ShowRejectsARangeBeforeGitRuns()
    {
        using var response = await GetAsync("/api/git/show?rev=a..b");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RefsRejectsAForeignHost()
    {
        using var response = await GetAsync("/api/git/refs", "evil.example");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RefsAnswersAnObjectWithARefsArray()
    {
        using var response = await GetAsync("/api/git/refs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // A CI checkout can have no local branch, so the test checks the shape alone.
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("refs").ValueKind);
    }

    [Fact]
    public async Task DatafilesRejectsANameOutsideTheJsonPattern()
    {
        // The file exists, so the name gate is all that answers 404 for it.
        var repo = Directory.CreateTempSubdirectory("zydis-viewer-host-tests-repo-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(repo.CreateSubdirectory("datafiles").FullName, "x.txt"), "x");

            using var response = await GetAsync("/datafiles/x.txt", repo: repo.FullName);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            repo.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task DatafilesServesTheDatabaseAsJson()
    {
        using var response = await GetAsync("/datafiles/instructions.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string? host = null, string? repo = null)
    {
        // The walk of the host finds the checkout above the build output of the tests as well.
        var options = new HostOptions(repo ?? Program.FindRepository(), _webRoot.FullName, 5280, false);
        var app = Program.CreateApp(options, builder => builder.UseTestServer());
        await using (app.ConfigureAwait(false))
        {
            await app.StartAsync().ConfigureAwait(false);
            using var client = app.GetTestClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
            request.Headers.Host = host;
            var response = await client.SendAsync(request).ConfigureAwait(false);
            // The body streams from the test server, which the dispose below stops.
            await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);
            return response;
        }
    }
}
