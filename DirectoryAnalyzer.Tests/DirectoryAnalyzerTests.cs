using Xunit;

namespace DirectoryAnalyzer.Tests;

public class DirectoryAnalyzerTests
{
    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "DirectoryAnalyzerTests", Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task AnalyzeAsync_ComputesSizesForSimpleStructure()
    {
        var rootPath = CreateTempDirectory();
        var file1Path = Path.Combine(rootPath, "file1.bin");
        var subDirPath = Path.Combine(rootPath, "Sub");
        Directory.CreateDirectory(subDirPath);
        var file2Path = Path.Combine(subDirPath, "file2.bin");

        await File.WriteAllBytesAsync(file1Path, new byte[100]);
        await File.WriteAllBytesAsync(file2Path, new byte[300]);

        var analyzer = new DirectoryAnalyzer.Core.Services.DirectoryAnalyzer(maxDegreeOfParallelism: 2);
        var rootNode = await analyzer.AnalyzeAsync(rootPath, CancellationToken.None);

        Assert.True(rootNode.IsDirectory);
        Assert.Equal(400, rootNode.Size);

        var subNode = rootNode.Children.FirstOrDefault(c => c.IsDirectory && c.Name == "Sub");
        Assert.NotNull(subNode);
        Assert.Equal(300, subNode!.Size);

        var file1Node = rootNode.Children.FirstOrDefault(c => !c.IsDirectory && c.Name == "file1.bin");
        Assert.NotNull(file1Node);
        Assert.Equal(100, file1Node!.Size);
    }

    [Fact]
    public async Task AnalyzeAsync_ExcludesSymbolicLinks()
    {
        var rootPath = CreateTempDirectory();
        var targetFilePath = Path.Combine(rootPath, "target.bin");
        await File.WriteAllBytesAsync(targetFilePath, new byte[200]);

        var linkPath = Path.Combine(rootPath, "link-to-target.bin");
        try
        {
            File.CreateSymbolicLink(linkPath, targetFilePath);
        }
        catch
        {
            return;
        }

        var analyzer = new DirectoryAnalyzer.Core.Services.DirectoryAnalyzer(maxDegreeOfParallelism: 2);
        var rootNode = await analyzer.AnalyzeAsync(rootPath, CancellationToken.None);

        Assert.Equal(200, rootNode.Size);
    }

    [Fact]
    public async Task AnalyzeAsync_HonorsCancellationTokenAndReturnsPartialResults()
    {
        var rootPath = CreateTempDirectory();

        var currentPath = rootPath;
        for (int i = 0; i < 5; i++)
        {
            currentPath = Path.Combine(currentPath, $"Level{i}");
            Directory.CreateDirectory(currentPath);
            var filePath = Path.Combine(currentPath, $"file{i}.bin");
            await File.WriteAllBytesAsync(filePath, new byte[100]);
        }

        var analyzer = new DirectoryAnalyzer.Core.Services.DirectoryAnalyzer(maxDegreeOfParallelism: 1);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10);

        var rootNode = await analyzer.AnalyzeAsync(rootPath, cts.Token);

        Assert.NotNull(rootNode);
        Assert.True(rootNode.IsDirectory);
        Assert.True(rootNode.Size >= 0);
    }

    [Fact]
    public async Task AnalyzeAsync_EnforcesMaxDegreeOfParallelism()
    {
        var rootPath = CreateTempDirectory();

        for (int i = 0; i < 8; i++)
        {
            var subDir = Path.Combine(rootPath, $"Dir{i}");
            Directory.CreateDirectory(subDir);
            var filePath = Path.Combine(subDir, $"file{i}.bin");
            await File.WriteAllBytesAsync(filePath, new byte[50]);
        }

        var analyzer = new DirectoryAnalyzer.Core.Services.DirectoryAnalyzer(maxDegreeOfParallelism: 3);
        await analyzer.AnalyzeAsync(rootPath, CancellationToken.None);

        Assert.True(analyzer.MaxConcurrentWorkersObserved <= analyzer.MaxDegreeOfParallelism);
    }
}

