using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
namespace SpiderManInstaller
{
    sealed partial class App : Application
    {
        public App() { InitializeComponent(); }
        bool systemBackRegistered;
        void RegisterSystemBack()
        {
            if(systemBackRegistered)return;
            // Controls/dialogs handle their own Back action first. At the root,
            // consume the system request so B does not leave the launcher.
            SystemNavigationManager.GetForCurrentView().BackRequested += OnSystemBack;
            systemBackRegistered=true;
        }
        void OnSystemBack(object sender, BackRequestedEventArgs args)
        {
            args.Handled=true;
        }
        protected override void OnActivated(IActivatedEventArgs args)
        {
            if(args.Kind!=ActivationKind.Protocol)return;
            var protocol=(ProtocolActivatedEventArgs)args;
            if(protocol.Uri.Scheme!="uwp-spider-launcher" || protocol.Uri.AbsoluteUri!="uwp-spider-launcher:runtime-ready")return;
            RegisterSystemBack();
            var page=Window.Current.Content as MainPage;
            if(page==null){page=new MainPage();Window.Current.Content=page;}
            Window.Current.Activate();
            page.ShowRuntimeSetupResult();
        }
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseVisible);
            RegisterSystemBack();
            Window.Current.Content = new MainPage();
            Window.Current.Activate();
        }
    }
}
