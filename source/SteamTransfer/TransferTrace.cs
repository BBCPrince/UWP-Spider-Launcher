// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;

namespace SpiderManInstaller.SteamTransfer
{
    internal enum TransferEvent { Started, Connected, QrReady, LogonResult, LicenseResult, DepotKeyResult, ManifestAccess, Authenticated, Servers, Manifest, Retry, VerifiedFile, Completed, Cancelled, Failed, Disconnected }
    // Only launcher-authored, safe messages may be displayed directly.
    internal sealed class TransferProblem : IOException
    {
        public TransferProblem(string message) : base(message) { }
    }
    internal sealed class TransferTrace
    {
        readonly object sync = new object();
        readonly Queue<string> lines = new Queue<string>();
        readonly string path;
        public TransferTrace(string path) { this.path = path; Record(TransferEvent.Started); }
        // No arbitrary service strings, URLs, tokens, credentials or exception messages.
        public void Record(TransferEvent kind, long number = 0, Exception failure = null)
        {
            lock (sync)
            {
                string row = DateTime.UtcNow.ToString("o") + " " + kind + " n=" + number;
                if (failure != null) row += " exception=" + failure.GetType().Name + " hresult=" + failure.HResult.ToString("X8");
                lines.Enqueue(row);
                while (lines.Count > 96) lines.Dequeue();
                try { File.WriteAllLines(path, lines); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
