// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SpiderManInstaller.SteamTransfer;
using Windows.Storage;

namespace SpiderManInstaller
{
    internal static class SteamChecks
    {
        internal static async Task<string> RunAsync()
        {
            string parent = Path.GetFullPath(ApplicationData.Current.LocalCacheFolder.Path);
            string root = Path.Combine(parent, "SteamSelfTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                byte[] payload = { 1, 2, 3, 4 };
                string hash;
                using (var data = new MemoryStream(payload)) hash = TransferCore.Hash(data, CancellationToken.None);
                var expected = new ExpectedFile { Path = "asset", Size = 4, Hash = hash };
                var file = new TransferFile { Expected = expected };
                int fetches = 0;
                file.Chunks.Add(new TransferChunk { Offset = 0, Length = 4, NetworkLength = 4,
                    Matches = b => b.Length == 4 && b[0] == 1 && b[1] == 2 && b[2] == 3 && b[3] == 4,
                    Fetch = ct => { ct.ThrowIfCancellationRequested(); fetches++; return Task.FromResult(payload); } });
                var tracker = new DownloadProgressTracker(s => { }); tracker.Begin(4);
                await TransferCore.DownloadFile(root, file, 2, tracker, CancellationToken.None);
                TransferCore.Verify(root, expected, CancellationToken.None);
                await TransferCore.DownloadFile(root, file, 2, tracker, CancellationToken.None);
                if (fetches != 1) throw new Exception("Verified file was downloaded again.");
                expected.Path = "../outside";
                bool rejected = false;
                try { file.Validate(); } catch (InvalidDataException) { rejected = true; }
                if (!rejected) throw new Exception("Traversal not rejected.");
                return "PASS verified download, reuse, and traversal checks\r\n";
            }
            finally
            {
                if (Path.GetFullPath(root).StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    Directory.Delete(root, true);
            }
        }
    }
}
