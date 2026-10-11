using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
namespace SpiderManInstaller
{
    sealed partial class App : Application
    {
        public App() { InitializeComponent(); }
        protected override void OnActivated(IActivatedEventArgs args)
        {
            if(args.Kind!=ActivationKind.Protocol)return;
            var protocol=(ProtocolActivatedEventArgs)args;
            if(protocol.Uri.Scheme!="uwp-spider-launcher" || protocol.Uri.AbsoluteUri!="uwp-spider-launcher:runtime-ready")return;
            var page=Window.Current.Content as MainPage;
            if(page==null){page=new MainPage();Window.Current.Content=page;}
            Window.Current.Activate();
            page.ShowRuntimeSetupResult();
        }
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
