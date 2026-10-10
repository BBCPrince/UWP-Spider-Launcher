using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using SpiderManInstaller.SteamTransfer;
sealed class ProbeIdentity : IMachineInfoProvider
{
    readonly string id = Guid.NewGuid().ToString("N");
    public byte[] GetMachineGuid() => Encoding.UTF8.GetBytes(id + "-app");
    public byte[] GetMacAddress() => Encoding.UTF8.GetBytes(id + "-network");
    public byte[] GetDiskId() => Encoding.UTF8.GetBytes(id + "-storage");
}
class Program
{
    static async Task<int> Main()
    {
        using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(65)))
        {
            var trace = new TransferTrace(Path.Combine(AppContext.BaseDirectory, "probe.log"));
            var session = new SteamSession(new ProbeIdentity(), trace, stop.Token);
            try
            {
                await session.Connect();
                bool valid = false;
                var qr = await session.CreateQr(url => valid = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https");
                qr.ChallengeURLChanged = null;
                if (!valid) throw new Exception("Invalid QR challenge");
                bool cancelled = false;
                using (var approval = new CancellationTokenSource(TimeSpan.FromMilliseconds(800)))
                {
                    try { await qr.PollingWaitForResultAsync(approval.Token); }
                    catch (OperationCanceledException) { cancelled = true; }
                }
                if (!cancelled) throw new Exception("Approval polling did not stop");
                trace.Record(TransferEvent.Completed);
                Console.WriteLine("PASS Steam WebSocket, HTTPS QR challenge and approval cancellation (no challenge logged; no account sign-in)");
                return 0;
            }
            catch (Exception ex)
            {
                trace.Record(TransferEvent.Failed, failure: ex);
                Console.WriteLine("FAIL Steam probe: " + ex.GetType().Name + " 0x" + ex.HResult.ToString("X8"));
                return 1;
            }
            finally { await session.Close(); }
        }
    }
}
