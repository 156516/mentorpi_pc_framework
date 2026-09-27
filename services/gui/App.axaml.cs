using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using MentorpiPc.Gui.Views;
using MentorpiPc.Gui.Services;

namespace MentorpiPc.Gui;

public partial class App : Application
{
    public override void Initialize()
        => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 启动时建 ROS 服务
            var ros = new RosService();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new ViewModels.MainViewModel(ros)
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}