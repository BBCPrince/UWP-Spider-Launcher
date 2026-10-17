using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace SpiderManInstaller
{
    public sealed partial class MainPage
    {
        bool languageBusy;
        HashSet<string> installedLanguageIds = new HashSet<string>(StringComparer.Ordinal);
        static string LanguageGameRoot => Path.Combine(GamePreparation.RuntimeRoot, "StartupContent");
        static List<GameLanguageOption> LanguageCatalogue() => GamePreparation.LoadLanguagePacks().Select(p =>
            new GameLanguageOption { Id = p.Language, Label = p.Label, Archive = Path.GetFileName(p.File.Path), Size = p.File.Size }).ToList();

        void SettingsTab_Changed(object sender, SelectionChangedEventArgs e)
        { if (uiReady && e.OriginalSource == MainTabs && MainTabs.SelectedIndex == 3) RefreshGameLanguages(); }
        void RefreshLanguages_Click(object sender, RoutedEventArgs e) => RefreshGameLanguages();
        async void RefreshGameLanguages()
        {
            if (languageBusy) return;
            languageBusy = true; ApplyLanguageButton.IsEnabled = DownloadLanguageButton.IsEnabled = false; GameLanguageList.IsEnabled = false;
            try
            {
                var catalogue = LanguageCatalogue();
                var installed = await Task.Run(() => GameLanguageSettings.Installed(LanguageGameRoot, catalogue));
                string current = await Task.Run(() => GameLanguageSettings.Current(LanguageGameRoot));
                installedLanguageIds=new HashSet<string>(installed.Select(p=>p.Id),StringComparer.Ordinal);
                GameLanguageList.Items.Clear();
                GameLanguageList.Items.Add(new GameLanguageOption { Id = "", Label = "Default / restore previous language" });
                foreach (var p in catalogue) GameLanguageList.Items.Add(new GameLanguageOption {
                    Id=p.Id,Label=p.Label+(installedLanguageIds.Contains(p.Id)?"":" — download from Steam"),Archive=p.Archive,Size=p.Size});
                int selected = catalogue.FindIndex(p => p.Id == current);
                GameLanguageList.SelectedIndex = selected >= 0 ? selected + 1 : 0;
                GameLanguageList.IsEnabled = true;
                GameLanguageStatus.Text = installed.Count == 0 ? "Prepare your game first, or choose a language and use Download from Steam to start a new installation." :
                    "Installed: " + String.Join(", ", installed.Select(p => p.Label)) + ". Missing languages can be downloaded from Steam. Restart the game after changing language.";
            }
            catch (Exception ex) { GameLanguageStatus.Text = "Could not check game languages: " + ex.Message; }
            finally { languageBusy = false; UpdateLanguageActions(); }
        }
        void GameLanguage_Changed(object sender, SelectionChangedEventArgs e) { if(!languageBusy)UpdateLanguageActions(); }
        void UpdateLanguageActions()
        {
            var choice=GameLanguageList.SelectedItem as GameLanguageOption;
            bool valid=choice!=null&&!busy&&!languageBusy;
            ApplyLanguageButton.IsEnabled=valid&&(choice.Id.Length==0||installedLanguageIds.Contains(choice.Id));
            DownloadLanguageButton.IsEnabled=valid&&choice.Id.Length>0&&!installedLanguageIds.Contains(choice.Id);
        }
        async void DownloadLanguage_Click(object sender,RoutedEventArgs e)
        {
            if(busy||languageBusy)return;
            var choice=GameLanguageList.SelectedItem as GameLanguageOption;if(choice==null||choice.Id.Length==0)return;
            if(!GamePreparation.Ready()) {
                SelectInstallMethod("Steam");SelectSteamLanguage(choice.Id);
                SteamInfo.Text="Choose a sign-in method to download the game with this voice language.";return;
            }
            var pack=SteamLanguages.Pack(GamePreparation.LoadLanguagePacks(),choice.Id);
            var dialog=new ContentDialog {Title="Download "+pack.Label+" audio?",Content="Download only this voice pack from Steam ("+(pack.File.Size/1073741824.0).ToString("F2")+" GiB). Steam ownership and sign-in are required. Existing game files and saves are kept. The language will be selected when installation finishes; restart the game afterward.",
                PrimaryButtonText="QR sign-in",SecondaryButtonText="Account and password",CloseButtonText="Cancel",DefaultButton=ContentDialogButton.Primary,RequestedTheme=ElementTheme.Dark};
            var result=await dialog.ShowAsync();if(result==ContentDialogResult.None)return;
            SelectInstallMethod("Steam");SelectSteamLanguage(choice.Id);
            await DownloadFromSteam(result==ContentDialogResult.Secondary,pack);
        }
        async void ApplyLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (languageBusy || busy) { GameLanguageStatus.Text = "Finish the current operation first."; return; }
            var choice = GameLanguageList.SelectedItem as GameLanguageOption;
            if (choice == null) return;
            languageBusy = true; busy = true; ApplyLanguageButton.IsEnabled = false; GameLanguageList.IsEnabled = false; PlayButton.IsEnabled = false;
            try
            {
                var catalogue = LanguageCatalogue();
                await Task.Run(() => GameLanguageSettings.Apply(LanguageGameRoot, choice.Id, catalogue));
                GameLanguageStatus.Text = choice.Id.Length == 0 ? "Previous language setting restored. Restart the game to apply." :
                    choice.Label + " selected. Restart the game to apply. In-game text-language settings can override the default.";
            }
            catch (Exception ex) { GameLanguageStatus.Text = "Language change stopped: " + ex.Message; }
            finally { languageBusy = false; busy = false; GameLanguageList.IsEnabled = true; UpdateLanguageActions(); RefreshPreparation(); }
        }
    }
}
