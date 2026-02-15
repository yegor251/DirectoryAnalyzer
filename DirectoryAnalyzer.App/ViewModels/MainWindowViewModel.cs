using System.Collections.ObjectModel;
using System.Windows.Input;
using DirectoryAnalyzer.Core.Models;

namespace DirectoryAnalyzer.App.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly DirectoryAnalyzer.Core.Services.DirectoryAnalyzer _analyzer;
    private CancellationTokenSource? _cts;
    private bool _isScanning;
    private bool _canCancel;
    private string _statusMessage = "Select a folder to analyze.";

    public ObservableCollection<DirectoryNode> RootItems { get; } = new();

    public DirectoryNode? RootNode
    {
        get => _rootNode;
        private set => SetField(ref _rootNode, value);
    }
    private DirectoryNode? _rootNode;

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetField(ref _isScanning, value))
            {
                _selectFolderCommand.RaiseCanExecuteChanged();
                _cancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanCancel
    {
        get => _canCancel;
        private set
        {
            if (SetField(ref _canCancel, value))
            {
                _cancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    /// <summary>
    /// Function provided by the view to open the native folder picker dialog.
    /// </summary>
    public Func<Task<string?>>? FolderPicker { get; set; }

    public ICommand SelectFolderCommand => _selectFolderCommand;
    public ICommand CancelCommand => _cancelCommand;

    private readonly RelayCommand _selectFolderCommand;
    private readonly RelayCommand _cancelCommand;

    public MainWindowViewModel(DirectoryAnalyzer.Core.Services.DirectoryAnalyzer analyzer)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));

        _selectFolderCommand = new RelayCommand(async _ => await OnSelectFolderAsync(), _ => !IsScanning);
        _cancelCommand = new RelayCommand(_ => Cancel(), _ => CanCancel);
    }

    private async Task OnSelectFolderAsync()
    {
        if (IsScanning)
            return;

        if (FolderPicker is null)
            return;

        var path = await FolderPicker.Invoke();
        if (string.IsNullOrWhiteSpace(path))
            return;

        await StartScanAsync(path);
    }

    private async Task StartScanAsync(string path)
    {
        Cancel();

        _cts = new CancellationTokenSource();
        IsScanning = true;
        CanCancel = true;
        StatusMessage = $"Scanning: {path}";

        try
        {
            var root = await _analyzer.AnalyzeAsync(path, _cts.Token);
            RootNode = root;

            RootItems.Clear();
            RootItems.Add(root);

            StatusMessage = $"Completed: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            CanCancel = false;

            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Cancel()
    {
        if (_cts is { IsCancellationRequested: false })
        {
            _cts.Cancel();
            StatusMessage = "Cancellation requested...";
        }
    }
}

