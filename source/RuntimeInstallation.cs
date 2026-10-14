using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.System.Profile;

namespace SpiderManInstaller
{
    // Initializes only the verified, separately installed runtime already on this Xbox.
    // The runtime initializes its own app data; this launcher never opens another
    // package's private data or restores saves, learned recipes or compiled caches.
    static class RuntimeInstallation
    {
        const string ManifestHash="ecf32b6c0ba1560e5f94e0a28fb6b624d6d4f93f2a97d62a5943fd9abe9e62c7";
        const string HostHash="b5bfd13d11a5156720bb9e1d89f38970b88af35d0c274f8b8dff8df5999a5079";
        const string SetupHash="248a78f8625a6bff0d87604a8b73187999cb34aea4e49ad3f5259e8421c5a07f";
        const string DefaultsHash="9eaa59c61dc85ecd4cba4b445c92c603a02cfb0817834e01434c4b385aea65cf";
        static string PendingPath => Path.Combine(ApplicationData.Current.LocalFolder.Path,"runtime-setup.pending");
        static string ReadyPath => Path.Combine(ApplicationData.Current.LocalFolder.Path,"runtime-setup.last-ok");
        internal static bool HasPending => File.Exists(PendingPath);
        internal static async Task<bool> NeedsRepair()
        {
            if(CombinedRuntime.Enabled)return false; // Included host is verified and missing settings seeded by Play.
            if(HasPending || !File.Exists(ReadyPath))return true;
            return await Launcher.QueryUriSupportAsync(new Uri("spiderman-runtime:play"),
                LaunchQuerySupportType.Uri,GamePreparation.RuntimeFamily)!=LaunchQuerySupportStatus.Available;
        }
        internal static async Task Repair(bool playAfter=false)
        {
            if(CombinedRuntime.Enabled){
                await Task.Run(()=>{CombinedRuntime.VerifyPayload(_=>{},System.Threading.CancellationToken.None);CombinedRuntime.SeedMissingSettings();});
                if(playAfter&&!await CombinedRuntime.Launch())throw new IOException("Xbox declined the included game launch. Retry Play.");
                return;
            }
            if(AnalyticsInfo.VersionInfo.DeviceFamily!="Windows.Xbox")
                throw new IOException("This runtime installation is for Xbox Developer Mode.");
            await Task.Run(()=> {
                foreach(var item in new[]{new{Path="AppxManifest.xml",Hash=ManifestHash},
                    new{Path="StartupContent/SpiderManNativeStartup.exe",Hash=HostHash},
                    new{Path="RuntimeSetup.exe",Hash=SetupHash},
                    new{Path="InitialSettings/RuntimeDefaults.json",Hash=DefaultsHash}})
                {
                    string file=Path.Combine(GamePreparation.RuntimeRoot,item.Path);
                    if(!File.Exists(file) || AssetPreparation.Bytes.FileHash(file)!=item.Hash)
                        throw new IOException("The compatible runtime setup files are missing or differ. Install the separate runtime setup through Device Portal first. Imported game files are preserved.");
                }
            });
            // Prepared content and app registration have independent lifetimes.
            // RegisterPackageAsync from this UWP only created UserMgr0 registration
            // after deletion; that was not a usable Xbox Dev Home installation.
            // Never create that partial registration or disturb an installed runtime.
            if(await Launcher.QueryUriSupportAsync(new Uri("spiderman-runtime-setup:initialize"),
                LaunchQuerySupportType.Uri,GamePreparation.RuntimeFamily)!=LaunchQuerySupportStatus.Available)
            {
                File.Delete(PendingPath);File.Delete(ReadyPath);
                throw new IOException("Game files are prepared, but the separate Spider-Man game app is not installed. Install/register that runtime through Xbox Device Portal or Xbox deployment tools, then press Play. Keep your prepared files; another game download is not needed.");
            }
            string request=Guid.NewGuid().ToString("N");
            File.WriteAllText(PendingPath,RuntimeRepairState.Begin(request,DateTime.UtcNow,playAfter));
            File.WriteAllText(Path.Combine(GamePreparation.RuntimeRoot,"runtime-setup.request"),request);
            var options=new LauncherOptions{TargetApplicationPackageFamilyName=GamePreparation.RuntimeFamily};
            try
            {
                if(!await Launcher.LaunchUriAsync(new Uri("spiderman-runtime-setup:initialize"),options))
                    throw new IOException("The separate Spider-Man app could not open. Its Xbox Dev Home registration may be incomplete. Install/register the runtime through Xbox Device Portal or Xbox deployment tools, then press Play. Keep the prepared game files; another download is not needed.");
            }
            catch { File.Delete(PendingPath); if(File.Exists(ReadyPath))File.Delete(ReadyPath); throw; }
        }
        internal static RuntimeRepairState ReadResult()
        {
            if(CombinedRuntime.Enabled)return null;
            if(!HasPending)return null;
            string path=Path.Combine(GamePreparation.RuntimeRoot,"runtime-setup.result");
            var result=RuntimeRepairState.Read(File.ReadAllText(PendingPath),
                File.Exists(path)?File.ReadAllText(path):null,DateTime.UtcNow);
            if(result.Status!="pending")
            {
                // Consume before any async launch: Loaded, polling and activation
                // must never replay the same auto-play request.
                File.Delete(PendingPath);
                if(result.Status=="ok")File.WriteAllText(ReadyPath,DateTime.UtcNow.ToString("o"));
                else if(File.Exists(ReadyPath))File.Delete(ReadyPath);
            }
            return result;
        }
    }
}
