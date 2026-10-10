using System;
using System.Text;
using SteamKit2;
using Windows.Storage;

namespace SpiderManInstaller
{
    // SteamKit's supported provider extension point avoids desktop-only registry
    // and physical-drive queries. This identifies this app installation only.
    internal sealed class SteamMachineIdentity : IMachineInfoProvider
    {
        readonly string id;
        internal SteamMachineIdentity()
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            id = values["steam-installation-id"] as string;
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                values["steam-installation-id"] = id;
            }
        }
        public byte[] GetMachineGuid() => Encoding.UTF8.GetBytes(id + "-app");
        public byte[] GetMacAddress() => Encoding.UTF8.GetBytes(id + "-network");
        public byte[] GetDiskId() => Encoding.UTF8.GetBytes(id + "-storage");
    }
}
