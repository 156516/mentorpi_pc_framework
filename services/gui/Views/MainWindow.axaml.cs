using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MentorpiPc.Gui.ViewModels;

namespace MentorpiPc.Gui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // VM 引用（每次访问 DataContext，避免强引用导致 VM 销毁延后）
    private MainViewModel? Vm => DataContext as MainViewModel;

    /// <summary>
    /// 「⏸/▶ 暂停/继续建图」按钮：直接调 VM 的 RelayCommand
    /// （按钮 Content 用 Binding 显示状态——所以不用按 Click 改 Content）
    /// </summary>
    private async void OnToggleMappingClick(object? sender, RoutedEventArgs e)
    {
        if (Vm?.ToggleMappingCommand.CanExecute(null) == true)
            await Vm.ToggleMappingCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// 「📂 导入图片...」按钮：弹 File picker 选 .yaml → 同目录找同名 .pgm → 调 VM 加载。
    /// Linux 下 Avalonia 走 GTK/Portal 文件对话框；dotnet 8 AOT 友好，无需额外引用。
    /// </summary>
    private async void OnImportMapClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var yamlType = new FilePickerFileType("YAML 地图描述")
        {
            Patterns = new[] { "*.yaml", "*.yml" },
            MimeTypes = new[] { "application/x-yaml", "text/yaml" },
        };
        var pgmType = new FilePickerFileType("PGM 灰度图")
        {
            Patterns = new[] { "*.pgm" },
            MimeTypes = new[] { "image/x-portable-graymap" },
        };
        var options = new FilePickerOpenOptions
        {
            Title = "选择地图 .yaml 文件（同目录会自动找同名 .pgm）",
            AllowMultiple = false,
            FileTypeFilter = new[] { yamlType, pgmType },
        };

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return;

        var yamlUri = files[0].Path;
        if (!yamlUri.IsFile || !File.Exists(yamlUri.LocalPath))
        {
            Vm.MapControlStatus = $"❌ 路径无效: {yamlUri}";
            return;
        }

        // 同目录找同名 .pgm
        var yamlPath = yamlUri.LocalPath;
        var dir = Path.GetDirectoryName(yamlPath)!;
        var stemName = Path.GetFileNameWithoutExtension(yamlPath);
        var pgmPath = Path.Combine(dir, stemName + ".pgm");
        if (!File.Exists(pgmPath))
        {
            Vm.MapControlStatus = $"❌ 找不到同名 .pgm: {pgmPath}";
            return;
        }

        await Vm.LoadMapFromFileAsync(yamlPath, pgmPath);
    }
}