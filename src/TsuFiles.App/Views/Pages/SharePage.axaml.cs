using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TsuFiles.App.ViewModels.Pages;

namespace TsuFiles.App.Views.Pages;

public partial class SharePage : UserControl
{
    public SharePage()
    {
        InitializeComponent();
        DragDrop.AddDragOverHandler(DropZone, OnDragOver);
        DragDrop.AddDropHandler(DropZone, OnDrop);
    }

    private SharePageViewModel? Model => DataContext as SharePageViewModel;

    private static List<string> LocalFiles(DragEventArgs e) =>
        (e.DataTransfer.TryGetFiles() ?? []).Select(item => item.TryGetLocalPath()).OfType<string>().Where(File.Exists).ToList();

    // Luôn là Copy: báo Move thì Explorer xóa tệp gốc sau khi thả.
    private void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        Model?.AddFiles(LocalFiles(e));
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void OnAddFiles(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } top || Model is not { } model)
            return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = true });
        model.AddFiles(files.Select(f => f.TryGetLocalPath()).OfType<string>());
    }

    private async void OnBrowseFolder(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } top || Model is not { } model)
            return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path)
            model.SetReceiveFolder(path);
    }

    private async void OnCopyLink(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard && Model?.Link is { } link)
            await clipboard.SetTextAsync(link);
    }
}
