using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SpiderManInstaller.SteamTransfer;

class DownloadProgressTests
{
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static void Main()
    {
        double now=0;
        var snapshots=new List<DownloadProgressSnapshot>();
        var p=new DownloadProgressTracker(s=>snapshots.Add(s),()=>now);
        p.Begin(10000000);p.Existing(2000000);now=1;p.Pulse();
        var s=snapshots[snapshots.Count-1];
        Check(s.CompletedBytes==2000000 && s.NetworkBytesPerSecond==0 && double.IsNaN(s.SecondsRemaining),"resumed bytes never inflate speed");
        p.Stage("Downloading");
        int count=snapshots.Count;
        p.Chunk(1000000,500000);p.Chunk(1000000,500000);
        Check(snapshots.Count==count,"chunk callbacks are throttled");
        now=2;p.Pulse();s=snapshots[snapshots.Count-1];
        Check(s.CompletedBytes==4000000 && s.NetworkBytesPerSecond==1000000 && s.SecondsRemaining==3,"compressed speed and uncompressed ETA use correct units");
        now=3;p.Pulse();s=snapshots[snapshots.Count-1];
        Check(s.NetworkBytesPerSecond==500000,"idle transfer updates decay stale speed");
        p.Stage("Verifying game files");s=snapshots[snapshots.Count-1];
        Check(s.NetworkBytesPerSecond==0 && double.IsNaN(s.SecondsRemaining),"verification clears download ETA");
        p.Begin(10000);p.Stage("Downloading");
        Parallel.For(0,1000,i=>p.Chunk(10,5));now=4;p.Pulse();s=snapshots[snapshots.Count-1];
        Check(s.CompletedBytes==10000 && s.TotalBytes==10000,"concurrent workers preserve counters and phase reset");
        long sequence=0;foreach(var item in snapshots){if(item.Sequence<=sequence)throw new Exception("sequence");sequence=item.Sequence;}
        Check(true,"snapshot sequence is monotonic across phases");
        p.Begin(0);now=5;p.Pulse();s=snapshots[snapshots.Count-1];
        Check(s.TotalBytes==0 && double.IsNaN(s.SecondsRemaining),"empty transfer has no invalid ETA");
    }
}
