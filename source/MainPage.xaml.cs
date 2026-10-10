using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using QRCoder;
using DepotDownloader;

namespace SpiderManInstaller
{
    public sealed partial class MainPage : Page
    {
        readonly List<object> sources = new List<object>();
        bool reportActive;
        long importDone, importTotal;
        CancellationTokenSource cancellation;
        WifiReceiver receiver;
        bool busy;
        DateTime lastUpdate;
        int qrGeneration;
        int wifiGeneration;
        bool wifiComplete;
        int downloadGeneration;
        long lastProgressSequence;
        bool downloadActive, uiReady;
        List<string> imports = new List<string>();
        public MainPage()
        {
            InitializeComponent();
            object savedWorkers;
            if(ApplicationData.Current.LocalSettings.Values.TryGetValue("download-workers",out savedWorkers))
                DownloadWorkers.SelectedIndex=Convert.ToInt32(savedWorkers)==2?0:Convert.ToInt32(savedWorkers)==8?2:1;
            uiReady=true;
            Loaded += async (s, e) =>
            {
                RefreshStorage();
                RefreshPreparation();
                (PlayButton.IsEnabled?PlayButton:SteamTile).Focus(FocusState.Programmatic);
                // Explicit one-shot requests used only for deployment verification.
                try
                {
                    string request=Path.Combine(GameStorage.OpenRoot(),"prepare.request");
                    if(File.Exists(request))
                    {
                        string name=File.ReadAllText(request).Trim(); File.Delete(request);
                        int selected=imports.FindIndex(p=>Path.GetFileName(p)==name);
                        if(selected>=0){ImportList.SelectedIndex=selected;await PrepareSelected();}
                    }
                    string play=Path.Combine(GameStorage.OpenRoot(),"play.request");
                    if(File.Exists(play)){File.Delete(play);await PlayGame();}
                }
                catch(Exception ex){PreparationStatus.Text="Preparation request: "+ex.Message;}
                // One-shot connection/download check requested through Device Portal.
                // Never stores credentials or accepts arbitrary paths or commands.
                try
                {
                    string request=Path.Combine(ApplicationData.Current.LocalFolder.Path,"steam-action.request");
                    if(File.Exists(request))
                    {
                        string action=File.ReadAllText(request).Trim();File.Delete(request);
                        MainTabs.SelectedIndex=1;
                        if(action=="probe")
                        {
                            busy=true;SteamInfo.Text="Checking Xbox connectivity to Steam…";
                            try{await SteamProbe.RunAsync();SteamInfo.Text="Connection check complete.";}
                            finally{busy=false;}
                        }
                        else if(action=="download")await DownloadFromSteam();
                    }
                }
                catch(Exception ex){SteamInfo.Text="Steam check: "+ex.Message;}
                try
                {
                    var stamp = await ApplicationData.Current.LocalFolder.CreateFileAsync("importer-startup.txt", CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteTextAsync(stamp, "Launcher UI loaded UTC " + DateTime.UtcNow.ToString("o") + "\r\n");
                }
                catch { }
                await CheckLayoutRequest();
            };
        }
        void Manage_Click(object sender,RoutedEventArgs e){MainTabs.SelectedIndex=2;RefreshStorage();}
        void InstallMethod_Click(object sender,RoutedEventArgs e)
        {
            SelectInstallMethod((string)((FrameworkElement)sender).Tag);
        }
        void SelectInstallMethod(string method)
        {
            MainTabs.SelectedIndex=1;
            SteamPanel.Visibility=method=="Steam"?Visibility.Visible:Visibility.Collapsed;
            UsbPanel.Visibility=method=="Usb"?Visibility.Visible:Visibility.Collapsed;
            WifiPanel.Visibility=method=="Wifi"?Visibility.Visible:Visibility.Collapsed;
        }
        void Workers_Changed(object sender,SelectionChangedEventArgs e)
        {
            if(!uiReady)return;
            ApplicationData.Current.LocalSettings.Values["download-workers"]=SelectedWorkers();
        }
        int SelectedWorkers()=>DownloadWorkers.SelectedIndex==0?2:DownloadWorkers.SelectedIndex==2?8:4;
        // Fixed read-only preview actions, used to verify the real Xbox layout after deployment.
        async Task CheckLayoutRequest()
        {
            string request=Path.Combine(ApplicationData.Current.LocalFolder.Path,"ui-preview.request");
            if(!File.Exists(request))return;
            string mode=File.ReadAllText(request).Trim();File.Delete(request);
            if(mode=="checks")
            {
                string result=await SteamChecks.RunAsync();
                File.WriteAllText(Path.Combine(ApplicationData.Current.LocalFolder.Path,"ui-checks.txt"),result);
                return;
            }
            if(mode=="home")MainTabs.SelectedIndex=0;
            else if(mode=="steam" || mode=="usb" || mode=="wifi")SelectInstallMethod(mode=="steam"?"Steam":mode=="usb"?"Usb":"Wifi");
            else if(mode=="wifi-start"){SelectInstallMethod("Wifi");await StartWifi();}
            else if(mode=="storage")MainTabs.SelectedIndex=2;
            else if(mode=="settings")MainTabs.SelectedIndex=3;
            else if(mode=="prepare")
            {
                SelectInstallMethod("Steam");PrepareCard.Visibility=Visibility.Visible;
                SteamQr.Source=new BitmapImage(new Uri("ms-appx:///Assets/SmallLogo.png"));SteamQr.Visibility=Visibility.Visible;
                SteamInfo.Text="Layout preview only — no Steam sign-in request.";
            }
            else if(mode=="import-complete")
            {
                SelectInstallMethod("Usb");Interlocked.Exchange(ref importTotal,70802502451L);CompleteImport();
                Status.Text="Preview: game folder copied and verified. Choose Prepare game.";
            }
            else if(mode=="progress")
            {
                MainTabs.SelectedIndex=0;Status.Text="Preview: downloading game";Progress.Value=38;
                TransferSize.Text="24.80 / 65.95 GiB";TransferStats.Text="12.5 MB/s  ·  About 55 min left";TransferPercent.Text="38.0%";
            }
            else return;
            await Task.Delay(600);UpdateLayout();
            var rows=new JsonArray();
            foreach(var name in new[]{"PlayButton","SteamTile","UsbTile","WifiTile","SteamButton","SteamQr","PrepareCard","ImportList","ScanButton","ArchiveList","UsbImportButton","WifiButton","IncomingButton","WifiAddress","WifiPairing","WifiQr","WifiInfo","DestinationInfo","DeleteGameButton","DownloadWorkers","Status","Progress","CancelButton"})
            {
                var element=FindName(name) as FrameworkElement;
                if(element==null || element.ActualWidth==0 || element.Visibility!=Visibility.Visible)continue;
                var origin=element.TransformToVisual(this).TransformPoint(new Windows.Foundation.Point());
                rows.Add(new JsonObject { ["name"]=JsonValue.CreateStringValue(name),["x"]=JsonValue.CreateNumberValue(origin.X),["y"]=JsonValue.CreateNumberValue(origin.Y),["width"]=JsonValue.CreateNumberValue(element.ActualWidth),["height"]=JsonValue.CreateNumberValue(element.ActualHeight) });
            }
            File.WriteAllText(Path.Combine(ApplicationData.Current.LocalFolder.Path,"ui-layout.json"),new JsonObject { ["screen"]=JsonValue.CreateStringValue(mode),["width"]=JsonValue.CreateNumberValue(ActualWidth),["height"]=JsonValue.CreateNumberValue(ActualHeight),["elements"]=rows }.Stringify());
        }
        void RefreshPreparation()
        {
            if(busy)return;
            try
            {
                imports=GamePreparation.Imports();ImportList.Items.Clear();
                foreach(string p in imports)ImportList.Items.Add(Path.GetFileName(p));
                ImportList.SelectedIndex=imports.Count>0?0:-1;
                bool ready=GamePreparation.Ready();PlayButton.IsEnabled=ready;PrepareButton.IsEnabled=!ready;
                PrepareCard.Visibility=ready?Visibility.Collapsed:Visibility.Visible;
                PreparationStatus.Text=ready?"Game files prepared. Ready to launch.":"Import your game from the Install tab, then choose Prepare below.";
                if(File.Exists(Path.Combine(GamePreparation.RuntimeRoot,"deletion-pending.txt")))
                {PreparationStatus.Text="Game removal is incomplete. Open Storage to finish cleanup.";PrepareButton.IsEnabled=false;}
            }
            catch(Exception ex){PlayButton.IsEnabled=false;PreparationStatus.Text="Game readiness: "+ex.Message;}
        }
        void Refresh_Click(object sender,RoutedEventArgs e){RefreshPreparation();}
        void RefreshStorage()
        {
            try
            {
                var destination=GameStorage.OpenRoot();
                DestinationInfo.Text="Game storage: "+(GameStorage.FreeBytes(destination)/1073741824.0).ToString("F2")+" GiB free";
                HomeStorage.Text=(GameStorage.FreeBytes(destination)/1073741824.0).ToString("F1")+" GiB free in game storage";
                long appFree=GameStorage.FreeBytes(ApplicationData.Current.LocalFolder.Path);
                DestinationInfo.Text+="\nApp-data storage: "+(appFree/1073741824.0).ToString("F2")+" GiB free";
                if(appFree<512L*1024*1024)DestinationInfo.Text+="\nApp-data space is low. Back up and clear old diagnostic logs before updates.";
            }
            catch(Exception ex){DestinationInfo.Text="Storage unavailable: "+ex.Message;}
        }
        void StorageRefresh_Click(object sender,RoutedEventArgs e){if(!busy)RefreshStorage();}
        async void DeleteGame_Click(object sender,RoutedEventArgs e)
        {
            if(busy || (receiver!=null && receiver.Uploading)){CleanupStatus.Text="Finish or stop the current transfer first.";return;}
            busy=true;DeleteGameButton.IsEnabled=false;PlayButton.IsEnabled=false;PrepareButton.IsEnabled=false;
            StopWifi();
            try
            {
                CleanupStatus.Text="Checking installed game files…";
                var plan=await Task.Run(()=>GamePreparation.CleanupPlan());
                var dialog=new ContentDialog {
                    Title="Delete installed game files?",
                    Content="Remove "+(plan.Bytes/1073741824.0).ToString("F2")+" GiB in "+plan.Count+" files from this launcher's prepared game?\n\nClose Spider-Man before continuing. This cannot be undone; import and prepare the game again to play.\n\nYour saves, runtime app, USB originals and separate import copies are kept.",
                    PrimaryButtonText="Delete game files",CloseButtonText="Keep game",DefaultButton=ContentDialogButton.Close,RequestedTheme=ElementTheme.Dark
                };
                if(await dialog.ShowAsync()!=ContentDialogResult.Primary){CleanupStatus.Text="Nothing deleted.";return;}
                CancelButton.IsEnabled=false;Progress.Value=0;Status.Text="Deleting installed game files…";
                await Task.Run(()=>plan.Delete((done,total)=> {
                    var ignored=Dispatcher.RunAsync(CoreDispatcherPriority.Low,()=>{Progress.Value=total==0?100:100.0*done/total;Status.Text="Deleting game files: "+done+" / "+total;});
                }));
                Progress.Value=100;CleanupStatus.Text="Installed game files removed. Saves and the runtime app were kept.";
                Status.Text="Game files removed. Import your copy again to reinstall.";
            }
            catch(Exception ex){CleanupStatus.Text="Cleanup stopped: "+ex.Message+" Close the game and retry. Saves were not targeted.";}
            finally{busy=false;DeleteGameButton.IsEnabled=true;CancelButton.IsEnabled=true;RefreshPreparation();RefreshStorage();}
        }
        async void Prepare_Click(object sender,RoutedEventArgs e){await PrepareSelected();}
        async Task PrepareSelected()
        {
            if(busy || (receiver!=null && receiver.Uploading))return;
            int selected=ImportList.SelectedIndex;
            if(selected<0 || selected>=imports.Count){PreparationStatus.Text="Import a matching game first, then refresh imports.";return;}
            StopWifi();
            string source=imports[selected];busy=true;cancellation=new CancellationTokenSource();CancelButton.Visibility=Visibility.Visible;
            PrepareButton.IsEnabled=false;PlayButton.IsEnabled=false;Progress.IsIndeterminate=true;
            string outcome;
            try
            {
                PreparationStatus.Text="Verifying game archives…";
                var token=cancellation.Token;
                await Task.Run(()=>GamePreparation.Prepare(source,text=>
                {
                    File.WriteAllText(Path.Combine(GameStorage.OpenRoot(),"preparation-status.txt"),DateTime.UtcNow.ToString("o")+"\n"+text);
                    var ignored=Dispatcher.RunAsync(CoreDispatcherPriority.Low,()=>{if(busy){PreparationStatus.Text=text;Status.Text=text;}});
                },token));
                outcome="Game prepared and verified. Ready to play.";
            }
            catch(OperationCanceledException){outcome="Preparation cancelled. Select the same import to resume; its archives are preserved.";}
            catch(Exception ex){outcome="Preparation stopped: "+ex.Message;}
            finally{cancellation.Dispose();cancellation=null;busy=false;Progress.IsIndeterminate=false;CancelButton.Visibility=Visibility.Collapsed;}
            RefreshPreparation();PreparationStatus.Text=outcome;Status.Text=outcome;
            try{File.WriteAllText(Path.Combine(GameStorage.OpenRoot(),"preparation-status.txt"),DateTime.UtcNow.ToString("o")+"\n"+outcome);}catch{}
        }
        async void Play_Click(object sender,RoutedEventArgs e){await PlayGame();}
        async Task PlayGame()
        {
            if(busy || (receiver!=null && receiver.Uploading))return;
            try
            {
                StopWifi();
                File.WriteAllText(Path.Combine(GameStorage.OpenRoot(),"launch-status.txt"),DateTime.UtcNow.ToString("o")+"\nRequesting installed runtime through spiderman-runtime:play");
                bool started=await GamePreparation.Play();
                PreparationStatus.Text=started?"Game launch requested.":"Xbox could not open the runtime. Check that Spider-Man Runtime is installed.";
                File.WriteAllText(Path.Combine(GameStorage.OpenRoot(),"launch-status.txt"),DateTime.UtcNow.ToString("o")+"\n"+PreparationStatus.Text);
            }
            catch(Exception ex){PreparationStatus.Text="Launch: "+ex.Message;}
        }
        async Task<List<ExpectedFile>> LoadExpected()
        {
            var manifest = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///SteamFiles.json"));
            var rows = JsonArray.Parse(await FileIO.ReadTextAsync(manifest));
            var expected = new List<ExpectedFile>();
            foreach (var value in rows)
            {
                var obj = value.GetObject();
                expected.Add(new ExpectedFile { Path = obj.GetNamedString("path"), Size = (long)obj.GetNamedNumber("size"), Hash = obj.GetNamedString("sha256") });
            }
            return expected;
        }
        async void Steam_Click(object sender, RoutedEventArgs e){await DownloadFromSteam();}
        async Task DownloadFromSteam()
        {
            if (busy || (receiver != null && receiver.Uploading)) return;
            StopWifi();
            busy = true;
            downloadActive=true;CancelButton.Visibility=Visibility.Visible;
            int generation=++downloadGeneration;
            lastProgressSequence=0;
            TransferSize.Text=TransferStats.Text=TransferPercent.Text="";
            Progress.Value=0;Progress.IsIndeterminate=true;
            cancellation = new CancellationTokenSource();
            SteamButton.IsEnabled = false;
            PlayButton.IsEnabled=PrepareButton.IsEnabled=false;
            int workers=SelectedWorkers();
            string outcome="Steam files downloaded and verified. Choose Prepare game to enable Play.";
            string local = null;
            try
            {
                local = GameStorage.OpenRoot();
                var expected = await LoadExpected();
                var free = GameStorage.FreeBytes(local);
                var token = cancellation.Token;
                await Task.Run(() => SteamDownload.RunAsync(local, expected, free,
                    text=>SteamStatus(text,generation), ShowSteamQr, token,
                    snapshot=>SteamProgress(snapshot,generation),workers));
                Progress.Value=100;TransferPercent.Text="100%";
            }
            catch (OperationCanceledException) { outcome="Download stopped. Sign in again to verify and resume partial files."; }
            catch (Exception ex) { outcome="Steam download stopped: " + ex.Message; }
            finally
            {
                downloadActive=false;++downloadGeneration;CancelButton.Visibility=Visibility.Collapsed;
                ShowSteamQr(null);
                cancellation.Dispose(); cancellation = null;
                busy = false; SteamButton.IsEnabled = true;
                Progress.IsIndeterminate=false;TransferStats.Text="";
                RefreshPreparation();RefreshStorage();
                SteamInfo.Text=Status.Text=outcome;
            }
        }
        void SteamStatus(string text,int generation)
        {
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {if(downloadActive && generation==downloadGeneration){SteamInfo.Text=text;Status.Text=text;}});
        }
        void SteamProgress(DownloadProgressSnapshot snapshot,int generation)
        {
            var ignored=Dispatcher.RunAsync(CoreDispatcherPriority.Low,()=>
            {
                if(!downloadActive || generation!=downloadGeneration || snapshot.Sequence<=lastProgressSequence)return;
                lastProgressSequence=snapshot.Sequence;
                bool downloading=snapshot.Stage=="Downloading";
                Progress.IsIndeterminate=!downloading;
                Progress.Value=snapshot.TotalBytes==0?0:100.0*snapshot.CompletedBytes/snapshot.TotalBytes;
                TransferSize.Text=snapshot.TotalBytes==0?"":(snapshot.CompletedBytes/1073741824.0).ToString("F2")+" / "+(snapshot.TotalBytes/1073741824.0).ToString("F2")+" GiB";
                TransferPercent.Text=snapshot.TotalBytes==0?"":Progress.Value.ToString("F1")+"%";
                string eta=double.IsNaN(snapshot.SecondsRemaining)?"Estimating time…":snapshot.SecondsRemaining<60?"Less than a minute left":"About "+Math.Ceiling(snapshot.SecondsRemaining/60).ToString("F0")+" min left";
                TransferStats.Text=downloading?(snapshot.NetworkBytesPerSecond/1000000.0).ToString("F1")+" MB/s  ·  "+eta:snapshot.Stage;
                if(downloading)Status.Text="Downloading game";
            });
        }
        void ShowSteamQr(string url)
        {
            int generation = Interlocked.Increment(ref qrGeneration);
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (generation != qrGeneration) return;
                if (url == null) { SteamQr.Source = null; SteamQr.Visibility = Visibility.Collapsed; SteamQrState("hidden"); return; }
                try
                {
                    using (var generator = new QRCodeGenerator())
                    using (var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M))
                    using (var qr = new PngByteQRCode(data))
                    using (var input = new MemoryStream(qr.GetGraphic(6)))
                    {
                        var bitmap = new BitmapImage();
                        await bitmap.SetSourceAsync(input.AsRandomAccessStream());
                        if (generation != qrGeneration) return;
                        SteamQr.Source = bitmap; SteamQr.Visibility = Visibility.Visible;
                        SteamQrState("visible");
                        SteamInfo.Text = "Scan with Steam's mobile app and approve Spider-Man Installer on this device.";
                    }
                }
                catch { SteamQrState("failed");SteamInfo.Text = "Unable to show Steam's sign-in code. Cancel and try again."; cancellation?.Cancel(); }
            });
        }
        void SteamQrState(string state)
        {
            try{File.WriteAllText(Path.Combine(ApplicationData.Current.LocalFolder.Path,"steam-qr-state.txt"),state+" "+DateTime.UtcNow.ToString("o"));}catch{}
        }
        async void Scan_Click(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            try
            {
                busy = true;
                Status.Text = "Looking for USB storage…";
                sources.Clear(); ArchiveList.Items.Clear();
                var drives = await KnownFolders.RemovableDevices.GetFoldersAsync();
                foreach (var drive in drives)
                {
                    await AddFolderCandidate(drive, drive.Name);
                    foreach (var folder in await drive.GetFoldersAsync())
                        await AddFolderCandidate(folder, drive.Name + " / " + folder.Name);
                    foreach (var file in await drive.GetFilesAsync())
                        if (file.FileType.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                        { sources.Add(file); ArchiveList.Items.Add("ZIP: " + drive.Name + " / " + file.Name); }
                }
                ArchiveList.SelectedIndex = sources.Count == 0 ? -1 : 0;
                Status.Text = sources.Count == 0 ? "No game folder or ZIP found. Put the Steam game folder directly on the USB drive, then scan again. The folder must contain asset_archive and Spider-Man.exe." : sources.Count + " source(s) found. Select a folder or ZIP, then Import selected.";
            }
            catch (Exception ex) { Status.Text = "USB scan: " + ex.Message; }
            finally { busy = false; }
        }
        async Task AddFolderCandidate(StorageFolder candidate, string label)
        {
            try
            {
                await candidate.GetFolderAsync("asset_archive");
                sources.Add(candidate); ArchiveList.Items.Add("Folder: " + label);
            }
            catch (FileNotFoundException) { }
            catch (UnauthorizedAccessException) { }
        }
        async void Import_Click(object sender, RoutedEventArgs e)
        {
            if (busy) return;
            int index = ArchiveList.SelectedIndex;
            if (index < 0 || index >= sources.Count) { Status.Text = "Find and select a game folder or ZIP first."; return; }
            var folder = sources[index] as StorageFolder;
            if (folder != null) await ImportFolder(folder);
            else await ImportFile((StorageFile)sources[index]);
        }
        async void Incoming_Click(object sender, RoutedEventArgs e)
        {
            if (busy || (receiver != null && receiver.Uploading)) return;
            try { await ImportFile(null, Path.Combine(GameStorage.OpenRoot(), "received-game.zip")); }
            catch (Exception ex) { Status.Text = "No complete upload: " + ex.Message; }
        }
        async Task ImportFile(StorageFile file, string receivedPath = null)
        {
            if (receiver != null && receiver.Uploading) { Status.Text = "Wait for the Wi-Fi upload to finish first."; return; }
            StopWifi();
            busy = true; reportActive = true; lastUpdate = DateTime.MinValue; cancellation = new CancellationTokenSource();
            string local = null;
            string destination = null;
            try
            {
                BeginImport(); Status.Text = "Checking ZIP, Steam version and voice packs…"; Progress.Value = 0;
                local = GameStorage.OpenRoot();
                var expected = await LoadExpected();
                long free = GameStorage.FreeBytes(local);
                destination = Path.Combine(local, "SteamImport-" + Guid.NewGuid().ToString("N"));
                var token = cancellation.Token;
                using (var input = file != null ? await file.OpenStreamForReadAsync() : File.OpenRead(receivedPath))
                    await Task.Run(() => ImportEngine.Import(input, destination, expected, free, Report, token, GamePreparation.LoadLanguagePacks()));
                if (receivedPath != null && receivedPath.Equals(Path.Combine(local, "received-game.zip"), StringComparison.OrdinalIgnoreCase))
                    File.Delete(receivedPath);
                CompleteImport(); Status.Text = "Steam files copied and verified.\n" + destination + "\nChoose Prepare game below to enable Play.";
                Progress.Value = 100;
            }
            catch (OperationCanceledException) { Status.Text = "Import cancelled. Incomplete copy retained in the import destination."; }
            catch (Exception ex) { Status.Text = "Import stopped: " + ex.Message + "\nYour SSD and existing game were not changed."; }
            finally { reportActive = false; cancellation.Dispose(); cancellation = null; busy = false; CancelButton.Visibility=Visibility.Collapsed; RefreshPreparation();RefreshStorage(); }
        }
        async Task ImportFolder(StorageFolder folder)
        {
            if (receiver != null && receiver.Uploading) { Status.Text = "Wait for the Wi-Fi upload to finish first."; return; }
            StopWifi();
            busy = true; reportActive = true; lastUpdate = DateTime.MinValue;
            cancellation = new CancellationTokenSource();
            string local = null;
            string destination = null;
            try
            {
                BeginImport(); Progress.Value = 0; Status.Text = "Checking game folder, version and voice packs…";
                local = GameStorage.OpenRoot();
                var expected = await LoadExpected();
                long free = GameStorage.FreeBytes(local);
                destination = Path.Combine(local, "SteamImport-" + Guid.NewGuid().ToString("N"));
                var source = new UsbFolderSource(folder);
                var token = cancellation.Token;
                await Task.Run(() => FolderImportEngine.ImportAsync(source, destination, expected, free, Report, token, GamePreparation.LoadLanguagePacks()));
                CompleteImport();
                Status.Text = "Game folder copied and verified.\n" + destination + "\nChoose Prepare game below to enable Play.";
            }
            catch (OperationCanceledException) { reportActive = false; Status.Text = "Import cancelled. Incomplete copy retained in the import destination; no success receipt was written."; }
            catch (Exception ex) { reportActive = false; Status.Text = "Folder import stopped: " + ex.Message + "\nYour USB files were not changed."; }
            finally { reportActive = false; cancellation.Dispose(); cancellation = null; busy = false; CancelButton.Visibility=Visibility.Collapsed; RefreshPreparation();RefreshStorage(); }
        }
        void BeginImport()
        {
            Interlocked.Exchange(ref importDone,0);Interlocked.Exchange(ref importTotal,0);
            TransferSize.Text="";TransferPercent.Text="";TransferStats.Text="Checking files…";
            CancelButton.Visibility=Visibility.Visible;
        }
        void CompleteImport()
        {
            reportActive=false;Progress.IsIndeterminate=false;Progress.Value=100;
            long total=Interlocked.Read(ref importTotal);
            TransferSize.Text=(total/1073741824.0).ToString("F2")+" / "+(total/1073741824.0).ToString("F2")+" GiB";
            TransferPercent.Text="100%";TransferStats.Text="Copy complete and verified — choose Prepare game.";
            CancelButton.Visibility=Visibility.Collapsed;
        }
        void Report(long done, long total, string file)
        {
            Interlocked.Exchange(ref importDone,done);Interlocked.Exchange(ref importTotal,total);
            if ((DateTime.UtcNow - lastUpdate).TotalMilliseconds < 500 && done != total) return;
            lastUpdate = DateTime.UtcNow;
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (!reportActive) return;
                Progress.IsIndeterminate=false;Progress.Value=total==0?0:100.0*done/total;
                Status.Text="Importing "+file;
                TransferSize.Text=(done/1073741824.0).ToString("F2")+" / "+(total/1073741824.0).ToString("F2")+" GiB";
                TransferPercent.Text=Progress.Value.ToString("F1")+"%";TransferStats.Text="Copying and verifying files";
            });
        }
        async void Wifi_Click(object sender, RoutedEventArgs e){await StartWifi();}
        void StopWifi()
        {
            ++wifiGeneration;receiver?.Dispose();receiver=null;
            WifiQr.Source=null;WifiQr.Visibility=WifiQrHint.Visibility=Visibility.Collapsed;
            WifiAddress.Text=WifiPairing.Text="";
        }
        async Task StartWifi()
        {
            if(busy || (receiver!=null && receiver.Uploading))return;
            StopWifi();int generation=wifiGeneration;busy=true;wifiComplete=false;WifiButton.IsEnabled=false;
            try
            {
                var expected=await LoadExpected();
                var file=await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///WifiTransfer.html"));
                string page=await FileIO.ReadTextAsync(file);
                if(generation!=wifiGeneration)return;
                var candidate=new WifiReceiver(GameStorage.OpenRoot(),page,expected,GamePreparation.LoadLanguagePacks(),text=>
                {var ignored=Dispatcher.RunAsync(CoreDispatcherPriority.Normal,()=>{if(generation==wifiGeneration)WifiInfo.Text=text;});},
                (done,total,name)=>WifiProgress(done,total,name,generation),
                complete=>{var ignored=Dispatcher.RunAsync(CoreDispatcherPriority.Normal,()=>
                {
                    if(generation!=wifiGeneration)return;
                    wifiComplete=true;reportActive=false;Progress.Value=100;TransferPercent.Text="100%";
                    long bytes=Interlocked.Read(ref importTotal);
                    TransferSize.Text=(bytes/1073741824.0).ToString("F2")+" / "+(bytes/1073741824.0).ToString("F2")+" GiB";
                    TransferStats.Text=complete?"Files verified  -  choose Prepare game.":"ZIP received  -  choose Import received ZIP.";
                    Status.Text=TransferStats.Text;RefreshPreparation();RefreshStorage();
                });});
                receiver=candidate;await candidate.Start();
                if(generation!=wifiGeneration){candidate.Dispose();return;}
                BeginImport();Progress.Value=0;Progress.IsIndeterminate=false;reportActive=true;lastUpdate=DateTime.MinValue;
                WifiAddress.Text=candidate.Address??"http://<Xbox IP>:8077/";
                WifiPairing.Text="Pairing code: "+candidate.PairCode;
                WifiInfo.Text="Open this address on your PC, enter the code, then choose your game folder.";
                Status.Text="Wi-Fi receiver ready";TransferStats.Text="Waiting for your PC";
                if(candidate.QrAddress!=null)
                {
                    using(var generator=new QRCodeGenerator())
                    using(var data=generator.CreateQrCode(candidate.QrAddress,QRCodeGenerator.ECCLevel.M))
                    using(var qr=new PngByteQRCode(data))
                    using(var input=new MemoryStream(qr.GetGraphic(6)))
                    {
                        var bitmap=new BitmapImage();await bitmap.SetSourceAsync(input.AsRandomAccessStream());
                        if(generation!=wifiGeneration)return;
                        WifiQr.Source=bitmap;WifiQr.Visibility=WifiQrHint.Visibility=Visibility.Visible;
                    }
                }
            }
            catch(Exception ex){if(generation==wifiGeneration){StopWifi();WifiInfo.Text="Wi-Fi receiver: "+ex.Message;CancelButton.Visibility=Visibility.Collapsed;}}
            finally{busy=false;WifiButton.IsEnabled=true;}
        }
        void WifiProgress(long done,long total,string name,int generation)
        {
            if(generation!=wifiGeneration)return;
            Interlocked.Exchange(ref importDone,done);Interlocked.Exchange(ref importTotal,total);
            if(done<total)wifiComplete=false;
            // Throttle before dispatch so network writes cannot flood the UI queue.
            if((DateTime.UtcNow-lastUpdate).TotalMilliseconds<500 && done!=total)return;
            lastUpdate=DateTime.UtcNow;
            var ignored=Dispatcher.RunAsync(CoreDispatcherPriority.Low,()=>
            {
                if(generation!=wifiGeneration || wifiComplete)return;
                Progress.IsIndeterminate=false;Progress.Value=total==0?0:100.0*done/total;
                TransferSize.Text=(done/1073741824.0).ToString("F2")+" / "+(total/1073741824.0).ToString("F2")+" GiB";
                TransferPercent.Text=Progress.Value.ToString("F1")+"%";
                Status.Text=name;TransferStats.Text="Receiving and verifying game files";
            });
        }
        void Cancel_Click(object sender, RoutedEventArgs e)
        {
            cancellation?.Cancel(); StopWifi();
            CancelButton.Visibility=Visibility.Collapsed;
            WifiInfo.Text = "Wi-Fi receiver stopped.";
        }
    }
    static class StorageSpace
    {
        internal static async Task<long> FreeBytes(StorageFolder folder)
        {
            var properties = await folder.Properties.RetrievePropertiesAsync(new[] { "System.FreeSpace" });
            object value;
            if (!properties.TryGetValue("System.FreeSpace", out value) || value == null)
                throw new IOException("Cannot check storage space safely on this device.");
            return checked((long)Convert.ToUInt64(value));
        }
    }
}
