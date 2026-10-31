using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Zydis.Viewer.Host;

/// <summary>
/// Reads revisions and references from the git repository of the checkout.
/// </summary>
internal static partial class GitRevision
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Determines whether <paramref name="revision"/> names a single revision that git cannot read as an option or as
    /// a range.
    /// </summary>
    /// <param name="revision">The revision to check, for example <c>master</c>, <c>v1.0</c> or <c>HEAD~1</c>.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="revision"/> starts with a letter or a digit, holds only letters,
    /// digits and the characters <c>._/^~-</c>, and contains no <c>..</c>, otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsValid(string revision)
    {
        return RevisionPattern().IsMatch(revision) && !revision.Contains("..", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the content of a file at a revision of the repository.
    /// </summary>
    /// <param name="repositoryPath">The path of the git checkout.</param>
    /// <param name="revision">The revision to read the file at.</param>
    /// <param name="relativePath">The path of the file relative to the root of the repository.</param>
    /// <param name="ct">The token that stops git.</param>
    /// <returns>The content of the file at <paramref name="revision"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <see cref="IsValid"/> rejects <paramref name="revision"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if git does not start, runs longer than 30 seconds or fails, for example because the revision or the file
    /// does not exist. The message names the cause, which for a failure is the error output of git.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="ct"/> is canceled.</exception>
    public static Task<string> ShowAsync(
        string repositoryPath, string revision, string relativePath, CancellationToken ct)
    {
        // The endpoint checks the revision too. This check keeps a later caller from handing git an option.
        if (!IsValid(revision))
        {
            throw new ArgumentException($"'{revision}' is not a valid revision.", nameof(revision));
        }

        return RunAsync(repositoryPath, ["show", "--end-of-options", $"{revision}:{relativePath}"], ct);
    }

    /// <summary>
    /// Returns the short names of the local branches, followed by those of the tags.
    /// </summary>
    /// <param name="repositoryPath">The path of the git checkout.</param>
    /// <param name="ct">The token that stops git.</param>
    /// <returns>
    /// The branch names, then the tag names, each in the order of their full reference names. Names that
    /// <see cref="IsValid"/> rejects are left out, since <see cref="ShowAsync"/> would refuse them.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if git does not start, runs longer than 30 seconds or fails. The message names the cause, which for a
    /// failure is the error output of git.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="ct"/> is canceled.</exception>
    public static async Task<IReadOnlyList<string>> ListRefsAsync(string repositoryPath, CancellationToken ct)
    {
        // for-each-ref sorts by the full reference name, which puts refs/heads before refs/tags.
        var output = await RunAsync(
                repositoryPath, ["for-each-ref", "--format=%(refname:short)", "refs/heads", "refs/tags"], ct)
            .ConfigureAwait(false);

        return Array.FindAll(
            output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), IsValid);
    }

    // The anchor \z rejects the trailing line break that $ would accept.
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._/^~-]*\z")]
    private static partial Regex RevisionPattern();

    private static async Task<string> RunAsync(string repositoryPath, string[] arguments, CancellationToken ct)
    {
        // ArgumentList hands each argument to git as it is, with no shell to split or expand it.
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(repositoryPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            // Raised when git is not installed or not on the PATH. The endpoints answer InvalidOperationException
            // with its message.
            throw new InvalidOperationException($"git did not start: {exception.Message}", exception);
        }

        // Both pipes are read while git runs, since git blocks once a pipe nobody reads is full. Killing git ends
        // both reads, so they run without a token.
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            try
            {
                // The reads finish before the using disposes their readers.
                await Task.WhenAll(output, error).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // The cancellation is the outcome. A read that broke with the killed pipe adds nothing to it.
            }

            // A client that went away keeps its cancellation. The timeout becomes a failure the endpoints answer.
            if (ct.IsCancellationRequested)
            {
                throw;
            }

            throw new InvalidOperationException($"git ran longer than {Timeout.TotalSeconds} seconds and was stopped.");
        }

        var standardOutput = await output.ConfigureAwait(false);
        var standardError = await error.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(standardError.Trim());
        }

        return standardOutput;
    }
}
