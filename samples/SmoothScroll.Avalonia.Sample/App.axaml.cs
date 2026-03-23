using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using SmoothScroll.Avalonia.Sample.ViewModels;
using SmoothScroll.Avalonia.Sample.Views;
using System.Diagnostics;

namespace SmoothScroll.Avalonia.Sample;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            desktop.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime activityLifetime)
        {
            activityLifetime.MainViewFactory = () => new MainView();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView();
        }

        Dispatcher.UnhandledException += OnUnhandledException;

        base.OnFrameworkInitializationCompleted();
    }

    private void OnUnhandledException(object sender, global::Avalonia.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Debugger.BreakForUserUnhandledException(e.Exception);
        e.Handled = true;
    }
}
