using System;
using System.Diagnostics;

namespace DepotDownloader
{
    internal sealed class DownloadProgressSnapshot
    {
        internal long Sequence, CompletedBytes, TotalBytes;
        internal double NetworkBytesPerSecond, SecondsRemaining;
        internal string Stage;
    }

    // One bounded snapshot per 500 ms. Resumed/verified bytes count toward completion,
    // never toward network speed. ETA uses newly written (decompressed) bytes.
    internal sealed class DownloadProgressTracker
    {
        readonly object sync = new object();
        readonly Func<double> seconds;
        readonly Action<DownloadProgressSnapshot> publish;
        long total, completed, wire, written, lastWire, lastWritten, sequence;
        double lastTime, wireRate, writeRate;
        string stage = "Connecting";

        internal DownloadProgressTracker(Action<DownloadProgressSnapshot> publish, Func<double> seconds = null)
        {
            var clock = Stopwatch.StartNew();
            this.seconds = seconds ?? (() => clock.Elapsed.TotalSeconds);
            this.publish = publish;
            lastTime = this.seconds();
        }

        internal void Begin(long bytes)
        {
            lock(sync)
            {
                total = Math.Max(0, bytes); completed = wire = written = lastWire = lastWritten = 0;
                wireRate = writeRate = 0; lastTime = seconds(); stage = "Checking files";
                Emit(true);
            }
        }
        internal void Stage(string value)
        {
            lock(sync)
            {
                stage = value; wireRate = writeRate = 0;
                lastTime = seconds(); lastWire = wire; lastWritten = written;
                Emit(true);
            }
        }
        internal void Existing(long bytes)
        {
            lock(sync) { completed = Math.Min(total, checked(completed + Math.Max(0, bytes))); Emit(false); }
        }
        internal void Pulse(){lock(sync)Emit(false);}
        internal void Chunk(long uncompressed, long compressed)
        {
            lock(sync)
            {
                completed = Math.Min(total, checked(completed + Math.Max(0, uncompressed)));
                written = checked(written + Math.Max(0, uncompressed));
                wire = checked(wire + Math.Max(0, compressed));
                Emit(false);
            }
        }
        void Emit(bool force)
        {
            double now = seconds(), elapsed = now - lastTime;
            if(!force && elapsed < .5) return;
            if(elapsed >= .5)
            {
                double nextWire = (wire-lastWire)/elapsed, nextWrite = (written-lastWritten)/elapsed;
                wireRate = wireRate == 0 ? nextWire : .5 * wireRate + .5 * nextWire;
                writeRate = writeRate == 0 ? nextWrite : .5 * writeRate + .5 * nextWrite;
                lastTime = now; lastWire = wire; lastWritten = written;
            }
            publish(new DownloadProgressSnapshot {
                Sequence=++sequence, Stage=stage, CompletedBytes=completed, TotalBytes=total,
                NetworkBytesPerSecond=wireRate,
                SecondsRemaining=stage=="Downloading" && writeRate>0 ? Math.Max(0,total-completed)/writeRate : double.NaN
            });
        }
    }
}
