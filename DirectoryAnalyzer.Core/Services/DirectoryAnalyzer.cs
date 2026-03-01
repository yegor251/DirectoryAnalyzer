using DirectoryAnalyzer.Core.Models;

namespace DirectoryAnalyzer.Core.Services;

public class DirectoryAnalyzer
{
    private readonly SemaphoreSlim _semaphore;

    private int _currentWorkers;
    private int _maxConcurrentWorkers;

    public int MaxDegreeOfParallelism { get; }

    public int MaxConcurrentWorkersObserved => _maxConcurrentWorkers;

    public DirectoryAnalyzer(int maxDegreeOfParallelism = 4)
    {
        if (maxDegreeOfParallelism <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));

        MaxDegreeOfParallelism = maxDegreeOfParallelism;
        _semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
    }

    public async Task<DirectoryNode> AnalyzeAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));

        var rootDirectoryInfo = new DirectoryInfo(path);
        if (!rootDirectoryInfo.Exists)
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        var rootNode = new DirectoryNode
        {
            Name = rootDirectoryInfo.Name,
            IsDirectory = true
        };

        var pendingWorkItems = 0;
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnWorkItemCompleted()
        {
            if (Interlocked.Decrement(ref pendingWorkItems) == 0)
            {
                completionSource.TrySetResult();
            }
        }

        void QueueDirectory(DirectoryInfo directoryInfo, DirectoryNode node)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Interlocked.Increment(ref pendingWorkItems);

            _semaphore.WaitAsync(cancellationToken).ContinueWith(async waitTask =>
            {
                if (waitTask.IsCanceled || waitTask.IsFaulted)
                {
                    OnWorkItemCompleted();
                    return;
                }

                try
                {
                    WorkerStarted();
                    await Task.Run(() => ProcessDirectory(directoryInfo, node, QueueDirectory, cancellationToken));
                }
                finally
                {
                    WorkerFinished();
                    _semaphore.Release();
                    OnWorkItemCompleted();
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)
            .Unwrap();
        }

        QueueDirectory(rootDirectoryInfo, rootNode);

        await completionSource.Task.ConfigureAwait(false);

        AggregateDirectorySize(rootNode);
        ComputePercentages(rootNode, null);

        return rootNode;
    }

    private void WorkerStarted()
    {
        var current = Interlocked.Increment(ref _currentWorkers);
        int snapshot;
        do
        {
            snapshot = _maxConcurrentWorkers;
            if (current <= snapshot)
                break;
        } while (Interlocked.CompareExchange(ref _maxConcurrentWorkers, current, snapshot) != snapshot);
    }

    private void WorkerFinished()
    {
        Interlocked.Decrement(ref _currentWorkers);
    }

    private static void ProcessDirectory(
        DirectoryInfo directoryInfo,
        DirectoryNode directoryNode,
        Action<DirectoryInfo, DirectoryNode> queueDirectory,
        CancellationToken cancellationToken)
    {
        if (directoryInfo.LinkTarget is not null)
        {
            return;
        }

        FileInfo[] files;
        DirectoryInfo[] subdirectories;

        try
        {
            files = directoryInfo.GetFiles();
        }
        catch (Exception) when (directoryInfo.Exists == false)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        try
        {
            subdirectories = directoryInfo.GetDirectories();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException || !directoryInfo.Exists)
        {
            return;
        }

        long filesSize = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (file.LinkTarget is not null)
            {
                continue;
            }

            long fileSize;
            try
            {
                fileSize = file.Length;
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            filesSize += fileSize;

            var fileNode = new DirectoryNode
            {
                Name = file.Name,
                IsDirectory = false,
                Size = fileSize
            };

            directoryNode.Children.Add(fileNode);
        }

        directoryNode.Size = filesSize;

        foreach (var subdirectory in subdirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (subdirectory.LinkTarget is not null)
            {
                continue;
            }

            var childNode = new DirectoryNode
            {
                Name = subdirectory.Name,
                IsDirectory = true
            };

            directoryNode.Children.Add(childNode);

            queueDirectory(subdirectory, childNode);
        }
    }

    private static long AggregateDirectorySize(DirectoryNode node)
    {
        if (!node.IsDirectory || node.Children.Count == 0)
        {
            return node.Size;
        }

        long total = node.Size;

        foreach (var child in node.Children)
        {
            total += AggregateDirectorySize(child);
        }

        node.Size = total;
        return total;
    }

    private static void ComputePercentages(DirectoryNode node, long? parentSize)
    {
        if (parentSize is null || parentSize.Value <= 0)
        {
            node.PercentageOfParent = 100.0;
        }
        else
        {
            node.PercentageOfParent = parentSize.Value == 0
                ? 0
                : (double)node.Size / parentSize.Value * 100.0;
        }

        foreach (var child in node.Children)
        {
            ComputePercentages(child, node.Size);
        }
    }
}

