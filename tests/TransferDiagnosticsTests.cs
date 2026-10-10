using System;
using System.IO;
using SpiderManInstaller.SteamTransfer;
class TransferDiagnosticsTests
{
    static void Main()
    {
        string path = Path.Combine(Path.GetTempPath(), "SteamTrace-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var trace = new TransferTrace(path);
            for (int i = 0; i < 240; i++) trace.Record(TransferEvent.Retry, i);
            if (File.ReadAllLines(path).Length != 96 || new FileInfo(path).Length > 30000) throw new Exception("Unbounded trace");
            Console.WriteLine("PASS bounded diagnostic history");
            trace.Record(TransferEvent.Failed, failure: new IOException("SECRET_TOKEN", new Exception("INNER_SECRET")));
            string text = File.ReadAllText(path);
            if (text.Contains("SECRET") || !text.Contains("IOException")) throw new Exception("Unsafe exception logging");
            Console.WriteLine("PASS exception metadata excludes credentials and service messages");
            new TransferTrace(path);
            if (File.ReadAllLines(path).Length != 1) throw new Exception("Old attempt retained");
            Console.WriteLine("PASS attempt isolation");
        }
        finally { File.Delete(path); }
    }
}
