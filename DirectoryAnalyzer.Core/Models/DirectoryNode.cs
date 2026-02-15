namespace DirectoryAnalyzer.Core.Models;

public class DirectoryNode
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public bool IsDirectory { get; set; }
    public List<DirectoryNode> Children { get; set; } = new();

    public double PercentageOfParent { get; set; }
}

