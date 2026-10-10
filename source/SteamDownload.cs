// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SpiderManInstaller.SteamTransfer;

namespace SpiderManInstaller
{
    internal static class SteamDownload
    {
        static readonly SemaphoreSlim Gate = new SemaphoreSlim(1);
        internal static async Task RunAsync(string localFolder, List<ExpectedFile> expected, long freeBytes,
            Action<string> status, Action<string> qr, CancellationToken token,
            Action<DownloadProgressSnapshot> progress = null, int workers = 4)
        {
            if (!await Gate.WaitAsync(0)) throw new InvalidOperationException("A Steam download is already running.");
            SteamSession session = null;
            Timer pulse = null;
            TransferTrace trace = null;
            try
            {
                token.ThrowIfCancellationRequested();
                string folder = Path.Combine(localFolder, "SteamDownload");
                Directory.CreateDirectory(folder);
                trace = new TransferTrace(Path.Combine(localFolder, "steam-transfer.log"));
                if (expected == null || expected.Count == 0) throw new InvalidDataException("Supported game-file list is missing.");
                long required = TransferCore.RequiredFreeSpace(folder, expected);
                if (freeBytes < required) throw new TransferProblem("Need " + (required / 1073741824.0).ToString("F1") + " GiB free for download and safe replacement.");
                string receipt = TransferCore.FullPath(folder, "import-receipt.txt");
                if (File.Exists(receipt)) File.Delete(receipt);
                var tracker = new DownloadProgressTracker(progress ?? (p => { }));
                tracker.Begin(expected.Sum(f => f.Size));
                pulse = new Timer(_ => tracker.Pulse(), null, 1000, 1000);
                session = new SteamSession(new SteamMachineIdentity(), trace, token);
                status("Approve the QR sign-in in your Steam app. Your account must own Spider-Man Remastered.");
                await session.Login(qr).ConfigureAwait(false);
                tracker.Stage("Checking supported Steam version");
                status("Checking access to supported build 14752622...");
                var plan = await session.ReadPlan(expected, token).ConfigureAwait(false);
                int completed = 0;
                foreach (var file in plan)
                {
                    status("Downloading and verifying file " + (completed + 1) + " of " + plan.Count + ".");
                    await TransferCore.DownloadFile(folder, file, Math.Max(2, Math.Min(8, workers)), tracker, token).ConfigureAwait(false);
                    trace.Record(TransferEvent.VerifiedFile, ++completed);
                }
                token.ThrowIfCancellationRequested();
                File.WriteAllText(receipt, "Verified original Steam files\r\nUTC " + DateTime.UtcNow.ToString("o") +
                    "\r\nFiles " + expected.Count + "\r\nReady for preparation.\r\n");
                tracker.Stage("Complete"); trace.Record(TransferEvent.Completed, completed);
                status("Steam files verified. Choose Prepare game to enable Play.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            { trace?.Record(TransferEvent.Cancelled); throw; }
            catch (TransferProblem ex) { trace?.Record(TransferEvent.Failed, failure: ex); throw; }
            catch (Exception ex)
            {
                trace?.Record(TransferEvent.Failed, failure: ex);
                throw new IOException("Steam transfer stopped. Sign in again to retry and resume checked chunks. Error metadata is in steam-transfer.log.");
            }
            finally
            {
                pulse?.Dispose();
                try { if (session != null) await session.Close().ConfigureAwait(false); }
                finally { qr(null); Gate.Release(); }
            }
        }
        internal static void Verify(string folder, ExpectedFile file, CancellationToken token) => TransferCore.Verify(folder, file, token);
    }
}
