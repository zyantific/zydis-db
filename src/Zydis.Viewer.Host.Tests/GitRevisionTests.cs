using Xunit;

namespace Zydis.Viewer.Host.Tests;

public class GitRevisionTests
{
    [Theory]
    [InlineData("master", true)]
    [InlineData("origin/master", true)]
    [InlineData("HEAD~1", true)]
    [InlineData("v1.0", true)]
    [InlineData("-x", false)]
    [InlineData("--output=/tmp/x", false)]
    [InlineData("a..b", false)]
    [InlineData("", false)]
    public void IsValidRejectsOptionsAndRanges(string revision, bool expected)
    {
        Assert.Equal(expected, GitRevision.IsValid(revision));
    }
}
