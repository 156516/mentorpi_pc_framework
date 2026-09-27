// MentorPi PC GUI - C# Avalonia 入口
// 启动 Avalonia 应用

using Avalonia;

namespace MentorpiPc.Gui;

internal static class Program
{
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Avalonia.Fonts.Inter 11.3+ 内嵌 Inter.ttf，无需装系统字体
            .WithInterFont()
            .LogToTrace();
}