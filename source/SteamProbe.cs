// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SpiderManInstaller.SteamTransfer;
using Windows.Storage;

namespace SpiderManInstaller
{
    internal static class SteamProbe
    {
        internal static async Task RunAsync()
        {
            string root = ApplicationData.Current.LocalFolder.Path;
            var trace = new TransferTrace(Path.Combine(root, "steam-probe.txt"));
            using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                var session = new SteamSession(new SteamMachineIdentity(), trace, stop.Token);
                try
                {
                    await SteamChecks.RunAsync();
                    await session.Connect();
                    var auth = await session.CreateQr(url => { });
                    using (var generator = new QRCoder.QRCodeGenerator())
                    using (var data = generator.CreateQrCode(auth.ChallengeURL, QRCoder.QRCodeGenerator.ECCLevel.M))
                    using (var png = new QRCoder.PngByteQRCode(data))
                        if (png.GetGraphic(6).Length < 100) throw new InvalidDataException("QR encoding failed.");
                    auth.ChallengeURLChanged = null;
                    trace.Record(TransferEvent.Completed);
                }
                catch (Exception ex) { trace.Record(TransferEvent.Failed, failure: ex); }
                finally { await session.Close(); }
            }
        }
    }
}
