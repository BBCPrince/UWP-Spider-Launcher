using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.System;
using Windows.System.Profile;

namespace SpiderManInstaller
{
    // Registers only the verified, separately supplied runtime already on this Xbox.
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
            if(HasPending || !File.Exists(ReadyPath))return true;
            return await Launcher.QueryUriSupportAsync(new Uri("spiderman-runtime:play"),
                LaunchQuerySupportType.Uri,GamePreparation.RuntimeFamily)!=LaunchQuerySupportStatus.Available;
        }
        internal static async Task Repair(bool playAfter=false)
        {
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
            // Xbox deployment also registers for Dev Home. Plain UWP registration
            // registered only UserMgr0 after a full uninstall and could not launch.
            var registration=await new PackageManager().RegisterPackageAsync(
                new Uri(Path.Combine(GamePreparation.RuntimeRoot,"AppxManifest.xml")),null,DeploymentOptions.DevelopmentMode);
            if(registration.ExtendedErrorCode!=null && registration.ExtendedErrorCode.HResult<0)
                throw new IOException("Xbox could not register the game app: 0x"+registration.ExtendedErrorCode.HResult.ToString("X8"));
            string request=Guid.NewGuid().ToString("N");
            File.WriteAllText(PendingPath,RuntimeRepairState.Begin(request,DateTime.UtcNow,playAfter));
            File.WriteAllText(Path.Combine(GamePreparation.RuntimeRoot,"runtime-setup.request"),request);
            var options=new LauncherOptions{TargetApplicationPackageFamilyName=GamePreparation.RuntimeFamily};
            try
            {
                if(!await Launcher.LaunchUriAsync(new Uri("spiderman-runtime-setup:initialize"),options))
                    throw new IOException("Game app registered, but its setup did not open. Press Play to retry repair.");
            }
            catch { File.Delete(PendingPath); throw; }
        }
        internal static RuntimeRepairState ReadResult()
        {
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
