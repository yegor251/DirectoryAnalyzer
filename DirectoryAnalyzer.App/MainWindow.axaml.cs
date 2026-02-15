using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using DirectoryAnalyzer.App.ViewModels;

namespace DirectoryAnalyzer.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainWindowViewModel vm)
        {
            vm.FolderPicker = PickFolderAsync;
        }
        else
        {
            DataContextChanged += (_, _) =>
            {
                if (DataContext is MainWindowViewModel vm2)
                {
                    vm2.FolderPicker = PickFolderAsync;
                }
            };
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async Task<string?> PickFolderAsync()
    {
        var topLevel = this.GetVisualRoot() as TopLevel;
        if (topLevel?.StorageProvider == null)
            return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        return folder?.Path.LocalPath;
    }
}

