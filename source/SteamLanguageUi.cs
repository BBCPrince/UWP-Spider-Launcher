// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Windows.UI.Xaml.Controls;

namespace SpiderManInstaller
{
    public sealed partial class MainPage
    {
        void InitializeSteamLanguages()
        {
            foreach(var pack in GamePreparation.LoadLanguagePacks())
                SteamLanguageList.Items.Add(new ComboBoxItem {Content=pack.Label,Tag=pack.Language});
            SelectSteamLanguage("english");
        }
        void SelectSteamLanguage(string id)
        {
            foreach(ComboBoxItem item in SteamLanguageList.Items)
                if((string)item.Tag==id){SteamLanguageList.SelectedItem=item;return;}
        }
        LanguagePack SelectedSteamLanguage()
        {
            string id=(SteamLanguageList.SelectedItem as ComboBoxItem)?.Tag as string;
            return SteamLanguages.Pack(GamePreparation.LoadLanguagePacks(),id??"english");
        }
    }
}
