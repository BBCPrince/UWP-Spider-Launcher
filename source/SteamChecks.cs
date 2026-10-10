using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DepotDownloader;
using SteamKit2;
using Windows.Storage;

namespace SpiderManInstaller
{
    internal static class SteamChecks
    {
        internal static async Task<string> RunAsync()
        {
            var messages = new List<string>();
            string root = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "SteamSelfTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var payload = new byte[] { 1, 2, 3, 4 };
                File.WriteAllBytes(Path.Combine(root, "asset"), payload);
                string hash;
                using (var input = new MemoryStream(payload)) hash = ImportEngine.Digest(input);
                var row = new ExpectedFile { Path = "asset", Size = payload.Length, Hash = hash };
                SteamDownload.Verify(root, row, CancellationToken.None);
                messages.Add("PASS correct file SHA-256");
                row.Hash = new string('0', 64);
                Reject(() => SteamDownload.Verify(root, row, CancellationToken.None));
                messages.Add("PASS corrupt file rejected");
                row.Hash = hash; row.Size++;
                Reject(() => SteamDownload.Verify(root, row, CancellationToken.None));
                messages.Add("PASS wrong-sized file rejected");
                row.Size--; row.Path = "missing";
                Reject(() => SteamDownload.Verify(root, row, CancellationToken.None));
                messages.Add("PASS missing file rejected");
                row.Path = "../outside";
                Reject(() => SteamDownload.Verify(root, row, CancellationToken.None));
                messages.Add("PASS traversal rejected");
                row.Path = "asset";
                bool cancelled = false;
                try { SteamDownload.Verify(root, row, new CancellationToken(true)); }
                catch (OperationCanceledException) { cancelled = true; }
                if (!cancelled) throw new Exception("Cancellation ignored");
                messages.Add("PASS verification cancellation");
                DownloadUi.Expected = new Dictionary<string, ExpectedFile>(StringComparer.OrdinalIgnoreCase) { { "asset", row } };
                DownloadUi.ValidateFile("asset", 4, 0);
                Reject(() => DownloadUi.ValidateFile("other", 4, 0));
                Reject(() => DownloadUi.ValidateFile("asset", 5, 0));
                Reject(() => DownloadUi.ValidateFile("asset", 4, EDepotFileFlag.Symlink));
                Reject(() => DownloadUi.ValidateFile("asset", 4, EDepotFileFlag.Directory));
                messages.Add("PASS manifest allowlist, size, symlink and directory guards");
                foreach(int workerLimit in new[]{2,4,8})
                {
                int active = 0, peak = 0, completed = 0;
                object peakLock=new object();
                await Util.InvokeAsync(Enumerable.Range(0, 20).Select(i => new Func<Task>(async () =>
                {
                    int now = Interlocked.Increment(ref active);
                    lock(peakLock)peak = Math.Max(peak, now);
                    await Task.Delay(3);
                    Interlocked.Decrement(ref active); Interlocked.Increment(ref completed);
                })), workerLimit);
                if (peak > workerLimit || peak!=workerLimit || completed != 20 || active != 0) throw new Exception("Worker limit broken");
                messages.Add("PASS bounded "+workerLimit+"-worker download scheduling");
                }
                bool secondFinished = false, failed = false;
                try
                {
                    await Util.InvokeAsync(new Func<Task>[] {
                        async () => { await Task.Delay(2); throw new IOException("test fault"); },
                        async () => { await Task.Delay(20); secondFinished = true; }
                    }, 2);
                }
                catch (IOException) { failed = true; }
                if (!failed || !secondFinished) throw new Exception("Outstanding writer escaped cleanup");
                messages.Add("PASS failure waits for outstanding writer");
                return string.Join("\r\n", messages) + "\r\n";
            }
            finally
            {
                DownloadUi.Expected = null;
                // Only this newly generated self-test directory can be removed.
                string parent = Path.GetFullPath(ApplicationData.Current.LocalCacheFolder.Path) + Path.DirectorySeparatorChar;
                if (Path.GetFullPath(root).StartsWith(parent, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
            }
        }
        static void Reject(Action action)
        {
            try { action(); }
            catch (InvalidDataException) { return; }
            throw new Exception("Expected rejection did not occur");
        }
    }
}
