// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SpiderManInstaller.SteamTransfer
{
    internal sealed class DownloadProgressSnapshot
    {
        public long Sequence, TotalBytes, CompletedBytes;
        public string Stage;
        public double NetworkBytesPerSecond, SecondsRemaining;
    }
    internal sealed class DownloadProgressTracker
    {
        readonly object sync = new object();
        readonly Func<double> clock;
        readonly Action<DownloadProgressSnapshot> publish;
        readonly Queue<Tuple<double, long, long>> history = new Queue<Tuple<double, long, long>>();
        long total, complete, network, downloaded, sequence;
        string stage = "Connecting";
        public DownloadProgressTracker(Action<DownloadProgressSnapshot> publish, Func<double> seconds = null)
        {
            var timer = Stopwatch.StartNew();
            clock = seconds ?? (() => timer.Elapsed.TotalSeconds);
            this.publish = publish;
        }
        public void Begin(long bytes)
        {
            lock (sync) { total = bytes; complete = network = downloaded = 0; history.Clear(); history.Enqueue(Tuple.Create(clock(), 0L, 0L)); }
            Pulse();
        }
        public void Stage(string value)
        {
            lock (sync)
            {
                if (stage == value) return;
                stage = value;
                history.Clear(); history.Enqueue(Tuple.Create(clock(), network, downloaded));
            }
            Pulse();
        }
        public void Existing(long bytes) { lock (sync) complete += bytes; }
        public void Chunk(long bytes, long wireBytes) { lock (sync) { complete += bytes; downloaded += bytes; network += wireBytes; } }
        public void Pulse()
        {
            DownloadProgressSnapshot result;
            lock (sync)
            {
                double now = clock();
                while (history.Count > 1 && now - history.Peek().Item1 > 5) history.Dequeue();
                var first = history.Peek();
                double elapsed = now - first.Item1;
                double rate = elapsed > 0 && stage == "Downloading" ? (network - first.Item2) / elapsed : 0;
                double outputRate = elapsed > 0 && stage == "Downloading" ? (downloaded - first.Item3) / elapsed : 0;
                result = new DownloadProgressSnapshot { Sequence = ++sequence, Stage = stage, TotalBytes = total,
                    CompletedBytes = Math.Min(total, complete), NetworkBytesPerSecond = rate,
                    SecondsRemaining = outputRate > 0 ? Math.Max(0, total - complete) / outputRate : double.NaN };
                if (now > first.Item1) history.Enqueue(Tuple.Create(now, network, downloaded));
            }
            publish(result);
        }
    }
}
