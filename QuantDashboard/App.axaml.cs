using Avalonia;
using Avalonia.Controls. ApplicationLifetimes;
using Avalonia. Markup.Xaml;
using QuantDashboard. Managers;

namespace QuantDashboard
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var settings = SettingsManager.Instance. CurrentSettings;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (settings.UISettings.ShowWindow)
                {
                    desktop.MainWindow = new MainWindow();
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}