using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DepotDownloader;

namespace SpiderManInstaller
{
    internal static class SteamDownload
    {
        static readonly SemaphoreSlim Gate = new SemaphoreSlim(1);
        internal static async Task RunAsync(string localFolder, List<ExpectedFile> expected,
            long freeBytes, Action<string> status, Action<string> qr, CancellationToken token,
            Action<DownloadProgressSnapshot> progress = null, int workers = 4)
        {
            if (!await Gate.WaitAsync(0)) throw new InvalidOperationException("A Steam download is already running.");
            string folder = Path.Combine(localFolder, "SteamDownload");
            Timer progressTimer=null;
            try
            {
                TransferDiagnostics.Begin(Path.Combine(localFolder,"steam-transfer.log"));
                // Require room for the complete game plus one largest-file staging copy.
                // Upstream can retain an old file while updating it. Partial data never
                // receives a success receipt, and the original Steam install is untouched.
                long required = checked(expected.Sum(f => f.Size) + expected.Max(f => f.Size) + 1073741824L);
                long existing = 0;
                foreach (var f in expected)
                {
                    var path = Path.Combine(folder, ImportEngine.SafePath(f.Path));
                    if (File.Exists(path)) existing = checked(existing + Math.Min(new FileInfo(path).Length, f.Size));
                }
                if (freeBytes < required - existing) throw new IOException("Need " + ((required - existing) / 1073741824.0).ToString("F1") + " GiB free for this download and its staging files.");
                Directory.CreateDirectory(folder);
                string receipt = Path.Combine(folder, "import-receipt.txt");
                if (File.Exists(receipt)) File.Delete(receipt);
                DownloadUi.Expected = expected.ToDictionary(f => ImportEngine.SafePath(f.Path), StringComparer.OrdinalIgnoreCase);
                DownloadUi.Status = status;
                DownloadUi.ShowQr = qr;
                DownloadUi.Progress = new DownloadProgressTracker(progress ?? (p => { }));
                var tracker=DownloadUi.Progress;
                progressTimer=new Timer(_=>tracker.Pulse(),null,1000,1000);
                AccountSettingsStore.Instance = new AccountSettingsStore();
                ContentDownloader.Config = new DownloadConfig
                {
                    InstallDirectory = folder, UseQrCode = true, RememberPassword = false,
                    VerifyAll = true, UsingFileList = true,
                    FilesToDownload = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Spider-Man.exe", "asset_archive/toc" },
                    FilesToDownloadRegex = new List<System.Text.RegularExpressions.Regex>(),
                    MaxDownloads = Math.Max(2, Math.Min(8, workers)), MaxServers = 8, Cancellation = token,
                    LoginID = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0)
                };
                status("Connecting to Steam. Approve the QR sign-in in the Steam mobile app.");
                if (!ContentDownloader.InitializeSteam3(null, null)) throw new IOException("Steam sign-in was not completed.");
                TransferDiagnostics.Record("authenticated_session_ready");
                qr(null);
                status("Checking Steam access to supported build 14752622 and downloading its version files…");
                await DownloadSelected();
                TransferDiagnostics.Record("version_files_downloaded");
                DownloadUi.Progress.Stage("Verifying version");
                foreach (var name in new[] { "Spider-Man.exe", "asset_archive/toc" })
                    Verify(folder, DownloadUi.Expected[name], token);
                token.ThrowIfCancellationRequested();
                status("Game version matches. Downloading required game files…");
                ContentDownloader.Config.FilesToDownload = new HashSet<string>(expected.Select(f => f.Path.Replace('\\', '/')), StringComparer.OrdinalIgnoreCase);
                await DownloadSelected();
                DownloadUi.Progress.Stage("Verifying game files");
                int count = 0;
                foreach (var f in expected)
                {
                    status("Verifying " + (++count) + "/" + expected.Count + ": " + f.Path);
                    Verify(folder, f, token);
                }
                token.ThrowIfCancellationRequested();
                File.WriteAllText(receipt, "Verified original Steam files\r\nUTC " + DateTime.UtcNow.ToString("o") + "\r\nFiles " + expected.Count + "\r\nReady for preparation.\r\n");
            }
            catch(OperationCanceledException ex) when(!token.IsCancellationRequested)
            {
                TransferDiagnostics.Error("internal_download_cancellation",ex);
                throw new IOException(TransferDiagnostics.Failure ?? "Steam stopped the download internally. The connection/manifest result is saved in steam-transfer.log; this was not a user cancellation.",ex);
            }
            catch(Exception ex)
            {
                TransferDiagnostics.Error(token.IsCancellationRequested?"user_cancelled":"transfer_stopped",ex);throw;
            }
            finally
            {
                progressTimer?.Dispose();
                // Disconnect callbacks must not replace the successful result or an error.
                DownloadUi.Status = s => { };
                qr(null);
                try { ContentDownloader.ShutdownSteam3(); }
                finally
                {
                    AccountSettingsStore.Instance = new AccountSettingsStore();
                    DownloadUi.ShowQr = s => { }; DownloadUi.Status = s => { }; DownloadUi.Expected = null;
                    DownloadUi.Progress = null;
                    Gate.Release();
                }
            }
            status("Steam files downloaded and verified. Choose Prepare game to enable Play.");
        }
        // Pin the build required by the runtime recipe instead of following today's public branch.
        // IDs come from the local installed-depot metadata; EXE and TOC SHA-1 match its
        // Steam manifests and SHA-256 match our recipe. Steam still checks ownership
        // and issues manifest permission normally. Never fall back to an unverified version.
        static Task DownloadSelected() => ContentDownloader.DownloadAppAsync(1817070,
            new List<(uint depotId, ulong manifestId)>
            {
                (1817071U, 7101193785692308687UL),
                (1817072U, 670672075597558132UL),
                (1817074U, 2767115330843299188UL)
            }, "public", "windows", "64", "english", false, false);

        internal static void Verify(string folder, ExpectedFile file, CancellationToken token)
        {
            string path = Path.Combine(folder, ImportEngine.SafePath(file.Path));
            if (!File.Exists(path) || new FileInfo(path).Length != file.Size)
                throw new InvalidDataException("Missing or unsupported Steam file: " + file.Path);
            using (var input = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var buffer = new byte[1024 * 1024];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    sha.TransformBlock(buffer, 0, read, buffer, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                string hash = BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
                if (!hash.Equals(file.Hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Steam file does not match the supported game version: " + file.Path + ". No game launch will be attempted.");
            }
        }
    }
}
