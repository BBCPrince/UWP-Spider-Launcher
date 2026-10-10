using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
namespace SpiderManInstaller
{
    sealed partial class App : Application
    {
        public App() { InitializeComponent(); }
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            Window.Current.Content = new MainPage();
            Window.Current.Activate();
            await StorageProbe.RunIfRequestedAsync();
            if (args.Arguments == "--steam-probe") await SteamProbe.RunAsync();
            if (args.Arguments == "--folder-probe") await FolderProbe.RunAsync();
        }
    }
}
