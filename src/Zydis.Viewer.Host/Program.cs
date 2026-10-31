using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Zydis.Viewer.Host;

/// <summary>
/// Serves the viewer web app together with what static hosting lacks: the <c>datafiles</c> folder of the checkout and
/// the git endpoints the compare dialog reads the base database from.
/// </summary>
internal static partial class Program
{
    private const string Usage = "Usage: Zydis.Viewer.Host [--repo <path>] [--web-root <path>] [--port <n>] [--open]";

    private static async Task<int> Main(string[] args)
    {
        string? repo = null;
        string? webRoot = null;
        var port = 5280;
        var open = false;

        for (var i = 0; i < args.Length; i++)
        {
            var option = args[i];
            if (option == "--open")
            {
                open = true;
                continue;
            }

            if (i + 1 == args.Length)
            {
                await Console.Error.WriteLineAsync(Usage).ConfigureAwait(false);
                return 1;
            }

            var value = args[++i];
            switch (option)
            {
                case "--repo":
                    repo = value;
                    break;
                // Port 0 lets Kestrel pick a free port, which the printed URL would not show.
                case "--port"
                    when ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                    && parsed != 0:
                    port = parsed;
                    break;
                case "--web-root":
                    webRoot = value;
                    break;
                default:
                    await Console.Error.WriteLineAsync(Usage).ConfigureAwait(false);
                    return 1;
            }
        }

        repo = Path.GetFullPath(repo ?? FindRepository());
        webRoot = Path.GetFullPath(webRoot ?? Path.Combine(repo, "src", "Zydis.Viewer.Web", "dist"));
        var datafiles = Path.Combine(repo, "datafiles");
        var url = $"http://127.0.0.1:{port}";
        var serveApp = Directory.Exists(webRoot);

        if (!File.Exists(Path.Combine(webRoot, "index.html")))
        {
            Console.WriteLine(
                $"{webRoot} holds no index.html, so only the datafiles and the git endpoints answer. " +
                "Run npm run build in src/Zydis.Viewer.Web to build the app.");
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // The build output holds no appsettings.json, so a Kestrel section in the folder dotnet run was started
            // from cannot replace the bind address.
            ContentRootPath = AppContext.BaseDirectory,
            // The host creates a web root that does not exist, which would leave an empty folder behind a typo.
            WebRootPath = serveApp ? webRoot : null,
        });
        builder.WebHost.UseUrls(url);
        // Host filtering answers 400 to other Host headers, so a web page that rebinds its own name to 127.0.0.1
        // cannot read the checkout through the visitor's browser.
        builder.Configuration["AllowedHosts"] = "127.0.0.1;localhost";
        var app = builder.Build();

        if (serveApp)
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        app.MapGet("/datafiles/{name}", (string name) =>
        {
            // The pattern admits plain file names only, so the path stays inside the datafiles folder.
            if (!DatafileName().IsMatch(name))
            {
                return Results.NotFound();
            }

            var path = Path.Combine(datafiles, name);
            return File.Exists(path) ? Results.File(path, "application/json") : Results.NotFound();
        });

        app.MapGet("/api/git/refs", async (CancellationToken ct) =>
        {
            try
            {
                var refs = await GitRevision.ListRefsAsync(repo, ct).ConfigureAwait(false);
                return Results.Json(new RefsResponse(refs), HostSerializerContext.Default.RefsResponse);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Text(exception.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        app.MapGet("/api/git/show", async (string? rev, CancellationToken ct) =>
        {
            if (rev is null || !GitRevision.IsValid(rev))
            {
                return Results.Text($"'{rev}' is not a valid revision.", statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var content = await GitRevision.ShowAsync(repo, rev, "datafiles/instructions.json", ct)
                    .ConfigureAwait(false);
                return Results.Text(content, "application/json");
            }
            catch (InvalidOperationException exception)
            {
                return Results.Text(exception.Message, statusCode: StatusCodes.Status404NotFound);
            }
        });

        // Kestrel listens once the application has started, so a browser opened here finds the server.
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            Console.WriteLine($"Zydis viewer: {url}");
            if (open)
            {
                OpenBrowser(url);
            }
        });

        await app.RunAsync().ConfigureAwait(false);
        return 0;
    }

    // dotnet run starts the host from its build output, which lies inside the checkout.
    private static string FindRepository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "datafiles")))
            {
                return directory.FullName;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception exception)
        {
            // The host serves without a browser, so the printed URL is the fallback.
            Console.WriteLine($"No browser started ({exception.Message}). Open {url} by hand.");
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]+\.json\z")]
    private static partial Regex DatafileName();
}

/// <summary>
/// The answer of <c>/api/git/refs</c>.
/// </summary>
/// <param name="Refs">The short names of the local branches, followed by those of the tags.</param>
internal sealed record RefsResponse(IReadOnlyList<string> Refs);

/// <summary>
/// The source generated serializer of the host's JSON answers, since the shared build settings turn off reflection
/// based serialization.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RefsResponse))]
internal sealed partial class HostSerializerContext :
    JsonSerializerContext;
