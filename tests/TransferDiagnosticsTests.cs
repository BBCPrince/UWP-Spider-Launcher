using System;
using System.IO;
using DepotDownloader;
class TransferDiagnosticsTests
{
    static void Main()
    {
        string path=Path.Combine(Path.GetTempPath(),"SpiderSteamTrace-"+Guid.NewGuid().ToString("N")+".txt");
        try
        {
            TransferDiagnostics.Begin(path);
            for(int i=0;i<240;i++)TransferDiagnostics.Record("stage "+i+" "+new string('a',400));
            if(File.ReadAllLines(path).Length!=96 || new FileInfo(path).Length>40000)throw new Exception("Unbounded trace");
            Console.WriteLine("PASS bounded trace length and retained tail");
            TransferDiagnostics.Error("network",new IOException("SECRET_NOT_FOR_LOG",new Exception("INNER_SECRET")));
            string text=File.ReadAllText(path);
            if(text.Contains("SECRET") || !text.Contains("exception=IOException"))throw new Exception("Unsafe exception logging");
            Console.WriteLine("PASS exception metadata excludes messages and credentials");
            TransferDiagnostics.Fail("Specific manifest failure");TransferDiagnostics.Fail("Generic abort");
            if(TransferDiagnostics.Failure!="Specific manifest failure")throw new Exception("Cause overwritten");
            Console.WriteLine("PASS first failure preserved across generic cancellation");
            TransferDiagnostics.Begin(path);
            if(TransferDiagnostics.Failure!=null || File.ReadAllLines(path).Length!=1)throw new Exception("Stale attempt state");
            Console.WriteLine("PASS each attempt resets previous failure and trace");
        }
        finally{File.Delete(path);}
    }
}
