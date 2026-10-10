using System;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.Authentication;
using Windows.Storage;

namespace SpiderManInstaller
{
    internal static class SteamProbe
    {
        internal static async Task RunAsync()
        {
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("steam-probe.txt", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, "UWP SteamKit probe started " + DateTime.UtcNow.ToString("o") + "\r\n");
            SteamClient client = null;
            try
            {
                await FileIO.AppendTextAsync(file, await SteamChecks.RunAsync());
                client = new SteamClient(SteamConfiguration.Create(c => c.WithProtocolTypes(ProtocolTypes.WebSocket).WithMachineInfoProvider(new SteamMachineIdentity())));
                var callbacks = new CallbackManager(client);
                var result = new TaskCompletionSource<string>();
                var user = client.GetHandler<SteamUser>();
                callbacks.Subscribe<SteamClient.ConnectedCallback>(c => user.LogOnAnonymous());
                callbacks.Subscribe<SteamUser.LoggedOnCallback>(c => result.TrySetResult("anonymous_logon=" + c.Result));
                callbacks.Subscribe<SteamClient.DisconnectedCallback>(c => result.TrySetResult("disconnected"));
                client.Connect();
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
                {
                    while (!result.Task.IsCompleted)
                    {
                        timeout.Token.ThrowIfCancellationRequested();
                        callbacks.RunCallbacks();
                        await Task.Delay(25, timeout.Token);
                    }
                }
                await FileIO.AppendTextAsync(file, await result.Task + "\r\n");
                if (await result.Task == "anonymous_logon=OK")
                {
                    var servers = await client.GetHandler<SteamContent>().GetServersForSteamPipe();
                    await FileIO.AppendTextAsync(file, "content_servers=" + servers.Count + "\r\n");
                    var qr = await client.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails
                    {
                        DeviceFriendlyName = "Spider-Man Installer", IsPersistentSession = false
                    });
                    // Never write the authentication URL or tokens to diagnostics.
                    await FileIO.AppendTextAsync(file, "qr_challenge_received=" + !string.IsNullOrEmpty(qr.ChallengeURL) + "\r\n");
                    using (var generator = new QRCoder.QRCodeGenerator())
                    using (var data = generator.CreateQrCode(qr.ChallengeURL, QRCoder.QRCodeGenerator.ECCLevel.M))
                    using (var png = new QRCoder.PngByteQRCode(data))
                        await FileIO.AppendTextAsync(file, "qr_png_generated=" + (png.GetGraphic(6).Length > 100) + "\r\n");
                    client.Disconnect();
                    client = null;
                    bool adapterQr = false;
                    using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
                    {
                        DepotDownloader.ContentDownloader.Config = new DepotDownloader.DownloadConfig
                        { UseQrCode = true, RememberPassword = false, Cancellation = cancel.Token, LoginID = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0) };
                        DepotDownloader.DownloadUi.ShowQr = url => { adapterQr = !string.IsNullOrEmpty(url); cancel.CancelAfter(500); };
                        try { await Task.Run(() => DepotDownloader.ContentDownloader.InitializeSteam3(null, null)); }
                        catch (OperationCanceledException) { }
                        finally
                        {
                            await Task.Run(() => DepotDownloader.ContentDownloader.ShutdownSteam3());
                            DepotDownloader.DownloadUi.ShowQr = s => { };
                            DepotDownloader.AccountSettingsStore.Instance = new DepotDownloader.AccountSettingsStore();
                        }
                    }
                    await FileIO.AppendTextAsync(file, "depot_adapter_qr_and_cancel=" + adapterQr + "\r\n");
                }
            }
            // Exception messages/stacks may contain service URLs or authentication data.
            // Preserve fault metadata without persisting their contents.
            catch (Exception ex) { await FileIO.AppendTextAsync(file,
                "probe_failed exception=" + ex.GetType().Name + " hresult=0x" + ex.HResult.ToString("X8") + "\r\n"); }
            finally { client?.Disconnect(); }
        }
    }
}
