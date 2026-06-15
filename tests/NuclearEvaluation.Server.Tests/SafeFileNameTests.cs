using NuclearEvaluation.Server.Services.Files;

namespace NuclearEvaluation.Server.Tests;

public class SafeFileNameTests
{
    static readonly Guid FallbackId = Guid.Parse("2e07a075-8c52-4424-a604-65e1344aa8b3");

    [Theory]
    [InlineData("../evil.csv", "evil.csv")]
    [InlineData("..\\evil.csv", "evil.csv")]
    [InlineData("/tmp/report.tsv", "report.tsv")]
    [InlineData("C:\\temp\\report.dat", "report.dat")]
    [InlineData("nested/folder/sample.csv", "sample.csv")]
    public void FromClientFileNameKeepsOnlyTheBaseName(string input, string expected)
    {
        string result = SafeFileName.FromClientFileName(input, FallbackId);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../")]
    public void FromClientFileNameFallsBackWhenNoSafeNameRemains(string input)
    {
        string result = SafeFileName.FromClientFileName(input, FallbackId);

        Assert.Equal($"{FallbackId:N}.upload", result);
    }

    [Fact]
    public void FromClientFileNameReplacesControlCharacters()
    {
        string result = SafeFileName.FromClientFileName("sample\u0001.csv", FallbackId);

        Assert.Equal("sample_.csv", result);
    }
}
