using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using SpiderManInstaller;
class ImportTests
{
    static int passed;
    static void Check(bool test, string name) { if (!test) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static void Reject(Action action, string name)
    { try { action(); } catch (InvalidDataException) { Check(true, name); return; } catch (IOException) { Check(true, name); return; } throw new Exception("Accepted " + name); }
    static MemoryStream Zip(string prefix = "", bool wrong = false, bool duplicate = false, string bad = null)
    {
        var input = new MemoryStream();
        using (var zip = new ZipArchive(input, ZipArchiveMode.Create, true))
        {
            Add(zip, prefix + "Spider-Man.exe", "exe"); Add(zip, prefix + "asset_archive/toc", "toc"); Add(zip, prefix + "asset_archive/g00s000", wrong ? "bad" : "dat");
            Add(zip, prefix + "extra-not-copied.txt", "extra");
            if (duplicate) Add(zip, prefix + "SPIDER-MAN.exe", "exe");
            if (bad != null) Add(zip, bad, "x");
        }
        input.Position = 0; return input;
    }
    static void Add(ZipArchive zip, string name, string value)
    { using (var stream = zip.CreateEntry(name).Open()) { var b = Encoding.UTF8.GetBytes(value); stream.Write(b, 0, b.Length); } }
    static IList<ExpectedFile> Expected()
    {
        var rows = new List<ExpectedFile>();
        foreach (var pair in new[] { new[] { "Spider-Man.exe", "exe" }, new[] { "asset_archive/toc", "toc" }, new[] { "asset_archive/g00s000", "dat" } })
            using (var s = new MemoryStream(Encoding.UTF8.GetBytes(pair[1]))) rows.Add(new ExpectedFile { Path = pair[0], Size = 3, Hash = ImportEngine.Digest(s) });
        return rows;
    }
    static void Main(string[] args)
    {
        string parent = Path.Combine(Path.GetTempPath(), "SpiderManImportTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(parent);
        try
        {
            foreach (string path in new[] { "../escape", "/root", "C:/drive", "a/../x", "a//b", "a/CON", "a/NUL.bin", "a/file:ads", "a/end.", "a/end ", "a/COM1", "a/LPT9", "a/\u0001bad" }) Reject(() => ImportEngine.SafePath(path), "reject " + path);
            Check(ImportEngine.SafePath("asset_archive/g00s000") == "asset_archive/g00s000", "extensionless files inside ZIP");
            foreach (string prefix in new[] { "", "Spider-Man Remastered/" })
            {
                string output = Path.Combine(parent, Guid.NewGuid().ToString("N"));
                using (var zip = Zip(prefix)) ImportEngine.Import(zip, output, Expected(), ImportEngine.Margin + 9, (a,b,c)=>{}, CancellationToken.None);
                Check(File.ReadAllText(Path.Combine(output,"asset_archive","g00s000")) == "dat", "streamed import " + prefix);
                Check(File.Exists(Path.Combine(output,"import-receipt.txt")), "verified receipt");
                Check(!File.Exists(Path.Combine(output,"extra-not-copied.txt")), "ignore unrelated files");
                using (var zip = Zip(prefix)) Reject(() => ImportEngine.Import(zip, output, Expected(), Int64.MaxValue, (a,b,c)=>{}, CancellationToken.None), "never overwrite existing import");
            }
            using (var zip = Zip()) Reject(() => ImportEngine.Import(zip, Path.Combine(parent,"low-space"), Expected(), ImportEngine.Margin + 8, (a,b,c)=>{}, CancellationToken.None), "insufficient disk space");
            Check(!Directory.Exists(Path.Combine(parent,"low-space")), "preflight makes no files");
            using (var zip = Zip(wrong:true)) Reject(() => ImportEngine.Import(zip, Path.Combine(parent,"bad-hash"), Expected(), Int64.MaxValue, (a,b,c)=>{}, CancellationToken.None), "SHA mismatch");
            Check(!File.Exists(Path.Combine(parent,"bad-hash","import-receipt.txt")), "failed copy never marked verified");
            using (var zip = Zip(duplicate:true)) Reject(() => ImportEngine.Import(zip, Path.Combine(parent,"duplicate"), Expected(), Int64.MaxValue, (a,b,c)=>{}, CancellationToken.None), "case-insensitive duplicate");
            using (var zip = Zip(bad:"../../escape")) Reject(() => ImportEngine.Import(zip, Path.Combine(parent,"traversal"), Expected(), Int64.MaxValue, (a,b,c)=>{}, CancellationToken.None), "unsafe unknown entry rejected");
            bool cancelled = false;
            using (var zip = Zip()) try { ImportEngine.Import(zip, Path.Combine(parent,"cancel"), Expected(), Int64.MaxValue, (a,b,c)=>{}, new CancellationToken(true)); } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "cancellation");
            if (args.Length == 1)
            {
                using (var zip = File.OpenRead(args[0])) ImportEngine.Import(zip, Path.Combine(parent,"zip64"), Expected(), Int64.MaxValue, (a,b,c)=>{}, CancellationToken.None);
                Check(File.ReadAllText(Path.Combine(parent,"zip64","asset_archive","g00s000")) == "dat", "ZIP64 directory offsets beyond 4 GiB");
            }
            Console.WriteLine("Passed " + passed + " checks.");
        }
        finally { Directory.Delete(parent, true); }
    }
}
