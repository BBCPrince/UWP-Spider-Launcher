using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SpiderManInstaller;
using SpiderManInstaller.SteamTransfer;

class SteamTransferTests
{
    static int passed;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static string Digest(byte[] bytes) { using (var s = new MemoryStream(bytes)) return TransferCore.Hash(s, CancellationToken.None); }
    static DownloadProgressTracker Tracker(long total) { var p = new DownloadProgressTracker(s => { }); p.Begin(total); return p; }
    static TransferFile Plan(byte[] bytes, string name = "game/asset.bin")
    {
        var p = new TransferFile { Expected = new ExpectedFile { Path = name, Size = bytes.Length, Hash = Digest(bytes) } };
        for (int i = 0; i < bytes.Length; i += 4)
        {
            var data = bytes.Skip(i).Take(4).ToArray();
            p.Chunks.Add(new TransferChunk { Offset = i, Length = data.Length, NetworkLength = data.Length,
                Matches = b => b.SequenceEqual(data), Fetch = ct => Task.FromResult(data) });
        }
        return p;
    }
    static void Reject(Action action, string name)
    {
        bool failed = false; try { action(); } catch (InvalidDataException) { failed = true; }
        Check(failed, name);
    }
    static async Task Run(string root)
    {
        var payload = Enumerable.Range(1, 12).Select(n => (byte)n).ToArray();
        var plan = Plan(payload);
        long initialSpace = TransferCore.RequiredFreeSpace(root, new System.Collections.Generic.List<ExpectedFile> { plan.Expected });
        int fetches = 0;
        foreach (var c in plan.Chunks) { var original = c.Fetch; c.Fetch = async ct => { Interlocked.Increment(ref fetches); await Task.Delay(2, ct); return await original(ct); }; }
        await TransferCore.DownloadFile(root, plan, 2, Tracker(12), CancellationToken.None);
        Check(File.ReadAllBytes(Path.Combine(root, "game/asset.bin")).SequenceEqual(payload) && fetches == 3, "download writes checked bytes at correct offsets");
        await TransferCore.DownloadFile(root, plan, 2, Tracker(12), CancellationToken.None);
        Check(fetches == 3, "verified completed file needs no network chunks");
        File.WriteAllBytes(Path.Combine(root, "game/asset.bin"), new byte[] { 9 });
        var partial = payload.ToArray(); partial[5] = 99;
        string id = Digest(System.Text.Encoding.UTF8.GetBytes(plan.Expected.Path.ToLowerInvariant() + "\n" + plan.Expected.Hash));
        string staged = Path.Combine(root, ".steamkit-transfer", id + ".part");
        File.WriteAllBytes(staged, partial);
        long resumedSpace = TransferCore.RequiredFreeSpace(root, new System.Collections.Generic.List<ExpectedFile> { plan.Expected });
        Check(resumedSpace == initialSpace - partial.Length - 1, "storage check credits partial bytes without trusting their content");
        await TransferCore.DownloadFile(root, plan, 2, Tracker(12), CancellationToken.None);
        Check(fetches == 4, "resume rechecks bytes and downloads only corrupt chunk");
        TransferCore.Verify(root, plan.Expected, CancellationToken.None);
        Check(!File.Exists(staged), "verified staged file committed and old file removed");
        var bad = Plan(payload, "bad.bin");
        bad.Chunks[0].Fetch = ct => Task.FromResult(new byte[] { 0, 0, 0, 0 });
        File.WriteAllBytes(Path.Combine(root, "bad.bin"), new byte[] { 7, 7 });
        bool rejected = false;
        try { await TransferCore.DownloadFile(root, bad, 2, Tracker(12), CancellationToken.None); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && File.ReadAllBytes(Path.Combine(root, "bad.bin")).SequenceEqual(new byte[] { 7, 7 }), "bad network chunk never replaces existing file");
        bad = Plan(payload, "wrong-build.bin"); bad.Expected.Hash = new string('0', 64);
        rejected = false;
        try { await TransferCore.DownloadFile(root, bad, 2, Tracker(12), CancellationToken.None); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && !File.Exists(Path.Combine(root, "wrong-build.bin")), "final SHA-256 rejects unsupported build despite valid chunk checks");
        Check(new FileInfo(TransferCore.PartialPath(root, bad.Expected)).Length == 0, "failed final verification clears partial data so retry can repair it");
        var empty = Plan(new byte[0], "empty.bin");
        await TransferCore.DownloadFile(root, empty, 2, Tracker(0), CancellationToken.None);
        Check(new FileInfo(Path.Combine(root, "empty.bin")).Length == 0, "zero-length file verified and committed");
        foreach (int workers in new[] { 2, 4, 8 })
        {
            int active = 0, peak = 0, done = 0; object sync = new object();
            await TransferCore.RunWorkers(24, workers, async (i, ct) =>
            { lock(sync) { active++; peak = Math.Max(peak, active); } await Task.Delay(10, ct); lock(sync) active--; Interlocked.Increment(ref done); }, CancellationToken.None);
            Check(peak == workers && active == 0 && done == 24, "bounded " + workers + "-worker schedule");
        }
        bool drained = false, failed = false;
        try { await TransferCore.RunWorkers(2, 2, async (i, ct) => { if (i == 0) { await Task.Delay(10); throw new IOException("fault"); } await Task.Delay(70); drained = true; }, CancellationToken.None); }
        catch (IOException) { failed = true; }
        Check(failed && drained, "failure drains other workers before returning");
        using (var cancel = new CancellationTokenSource())
        {
            bool cancelled = false; drained = false;
            var job = TransferCore.RunWorkers(1, 2, async (i, ct) => { cancel.Cancel(); await Task.Delay(15); drained = true; }, cancel.Token);
            try { await job; } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && drained, "cancellation drains in-flight workers");
        }
        var invalid = Plan(payload); invalid.Chunks[1].Offset++;
        Reject(invalid.Validate, "chunk gap rejected");
        invalid = Plan(payload); invalid.Chunks[1].Offset--;
        Reject(invalid.Validate, "overlapping chunks rejected");
        invalid = Plan(payload); invalid.Chunks.RemoveAt(2);
        Reject(invalid.Validate, "truncated chunk layout rejected");
        invalid = Plan(payload); invalid.Chunks[0].Length = 17 * 1024 * 1024;
        Reject(invalid.Validate, "oversized chunk allocation rejected");
        invalid = Plan(payload); invalid.Expected.Hash = "not-a-hash";
        Reject(invalid.Validate, "bad expected hash rejected");
        Reject(() => TransferCore.FullPath(root, "../outside"), "parent path escape rejected");
        Reject(() => TransferCore.FullPath(root, "C:\\outside"), "absolute destination rejected");
        bool earlyCancel = false;
        try { TransferCore.Verify(root, plan.Expected, new CancellationToken(true)); } catch (OperationCanceledException) { earlyCancel = true; }
        Check(earlyCancel, "verification respects cancellation");
        Check(!File.Exists(Path.Combine(root, "import-receipt.txt")), "core cannot mark an incomplete installation ready");
    }
    static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "SteamTransferTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { Run(root).GetAwaiter().GetResult(); Console.WriteLine("Transfer checks passed: " + passed); }
        finally { Directory.Delete(root, true); }
    }
}
