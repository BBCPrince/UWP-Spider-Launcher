// SPDX-License-Identifier: GPL-3.0-or-later
// Independent launcher transfer implementation. No DepotDownloader source.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SpiderManInstaller.SteamTransfer
{
    internal sealed class TransferChunk
    {
        public long Offset;
        public int Length;
        public long NetworkLength;
        public Func<byte[], bool> Matches;
        public Func<CancellationToken, Task<byte[]>> Fetch;
    }

    internal sealed class TransferFile
    {
        public ExpectedFile Expected;
        public List<TransferChunk> Chunks = new List<TransferChunk>();

        public void Validate()
        {
            if (Expected == null) throw new InvalidDataException("Missing file metadata.");
            ImportEngine.SafePath(Expected.Path);
            if (Expected.Size < 0 || Expected.Hash == null || Expected.Hash.Length != 64 ||
                Expected.Hash.Any(c => !Uri.IsHexDigit(c)))
                throw new InvalidDataException("Invalid supported-file metadata.");
            long end = 0;
            foreach (var chunk in Chunks.OrderBy(c => c.Offset))
            {
                if (chunk.Offset != end || chunk.Length <= 0 || chunk.Length > 16 * 1024 * 1024 ||
                    chunk.NetworkLength <= 0 || chunk.NetworkLength > 32 * 1024 * 1024 ||
                    chunk.Matches == null || chunk.Fetch == null || chunk.Length > Expected.Size - end)
                    throw new InvalidDataException("Invalid, overlapping or incomplete Steam chunk layout.");
                end += chunk.Length;
            }
            if (end != Expected.Size) throw new InvalidDataException("Incomplete Steam file manifest.");
        }
    }

    internal static class TransferCore
    {
        // Workers finish before this method returns, including failure/cancellation paths.
        public static async Task RunWorkers(int count, int parallelism,
            Func<int, CancellationToken, Task> action, CancellationToken cancellation)
        {
            if (count < 0 || parallelism < 1 || parallelism > 8) throw new ArgumentOutOfRangeException();
            using (var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                int next = -1;
                Exception first = null;
                var tasks = Enumerable.Range(0, Math.Min(count, parallelism)).Select(async unused =>
                {
                    try
                    {
                        while (true)
                        {
                            stop.Token.ThrowIfCancellationRequested();
                            int index = Interlocked.Increment(ref next);
                            if (index >= count) return;
                            await action(index, stop.Token).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.CompareExchange(ref first, ex, null);
                        stop.Cancel();
                    }
                }).ToArray();
                await Task.WhenAll(tasks).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
            }
        }

        public static string FullPath(string root, string relative)
        {
            string basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(basePath, ImportEngine.SafePath(relative)));
            if (!path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe destination.");
            // A pre-existing junction must not redirect verified files outside this game folder.
            for (string part = path; part != null && part.Length >= basePath.Length - 1; part = Path.GetDirectoryName(part))
                if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Links are not accepted in the download destination.");
            return path;
        }

        public static string Hash(Stream input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (var hash = SHA256.Create())
            {
                var buffer = new byte[1024 * 1024];
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    hash.TransformBlock(buffer, 0, count, buffer, 0);
                }
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static void Verify(string root, ExpectedFile expected, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string path = FullPath(root, expected.Path);
            if (!File.Exists(path) || new FileInfo(path).Length != expected.Size)
                throw new InvalidDataException("Missing or wrong-sized supported game file: " + expected.Path);
            using (var input = File.OpenRead(path))
                if (!string.Equals(Hash(input, token), expected.Hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Unsupported or damaged game file: " + expected.Path);
        }

        public static string PartialPath(string root, ExpectedFile expected)
        {
            string id;
            using (var bytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
                expected.Path.ToLowerInvariant() + "\n" + expected.Hash.ToLowerInvariant())))
                id = Hash(bytes, CancellationToken.None);
            return FullPath(root, ".steamkit-transfer/" + id + ".part");
        }

        public static long RequiredFreeSpace(string root, List<ExpectedFile> expected)
        {
            long present = 0;
            foreach (var file in expected)
            {
                foreach (var path in new[] { FullPath(root, file.Path), PartialPath(root, file) })
                    if (File.Exists(path)) present = checked(present + Math.Min(new FileInfo(path).Length, file.Size));
            }
            // Reserve a largest-file replacement and 1 GiB. Existing partials count as
            // already allocated storage, not verified content (verification happens below).
            return Math.Max(1073741824L, checked(expected.Sum(f => f.Size) + expected.Max(f => f.Size) + 1073741824L - present));
        }

        public static async Task DownloadFile(string root, TransferFile plan, int workers,
            DownloadProgressTracker progress, CancellationToken token)
        {
            plan.Validate();
            token.ThrowIfCancellationRequested();
            string destination = FullPath(root, plan.Expected.Path);
            if (File.Exists(destination))
            {
                progress.Stage("Checking existing files");
                try { Verify(root, plan.Expected, token); progress.Existing(plan.Expected.Size); return; }
                catch (InvalidDataException) { /* Retain the original until a verified replacement exists. */ }
            }
            string partial = PartialPath(root, plan.Expected);
            Directory.CreateDirectory(Path.GetDirectoryName(partial));
            progress.Stage("Checking partial download");
            using (var stream = new FileStream(partial, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                long priorLength = stream.Length;
                if (priorLength > plan.Expected.Size) { stream.SetLength(0); priorLength = 0; }
                var missing = new List<TransferChunk>();
                foreach (var chunk in plan.Chunks)
                {
                    token.ThrowIfCancellationRequested();
                    bool valid = false;
                    if (chunk.Offset + chunk.Length <= priorLength)
                    {
                        var data = new byte[chunk.Length];
                        stream.Position = chunk.Offset;
                        int read = 0;
                        while (read < data.Length)
                        {
                            int got = stream.Read(data, read, data.Length - read);
                            if (got == 0) break;
                            read += got;
                        }
                        valid = read == data.Length && chunk.Matches(data);
                    }
                    if (valid) progress.Existing(chunk.Length); else missing.Add(chunk);
                }
                // Resume is based on rechecking bytes, never trusting a completion journal.
                progress.Stage("Downloading");
                await RunWorkers(missing.Count, workers, async (index, ct) =>
                {
                    var chunk = missing[index];
                    byte[] data = await chunk.Fetch(ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    if (data == null || data.Length != chunk.Length || !chunk.Matches(data))
                        throw new InvalidDataException("Downloaded chunk verification failed.");
                    lock (stream)
                    {
                        ct.ThrowIfCancellationRequested();
                        stream.Position = chunk.Offset;
                        stream.Write(data, 0, data.Length);
                    }
                    progress.Chunk(chunk.Length, chunk.NetworkLength);
                }, token).ConfigureAwait(false);
                stream.SetLength(plan.Expected.Size);
                stream.Flush();
                stream.Position = 0;
                progress.Stage("Verifying game files");
                if (!string.Equals(Hash(stream, token), plan.Expected.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    // A chunk checksum collision must not make every later resume fail.
                    stream.SetLength(0);
                    throw new InvalidDataException("Downloaded file is not the supported game version.");
                }
            }
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            // Checked partials survive interruption; a destination is never an unverified partial.
            // If interrupted between these moves, the next attempt uses the checked .part again.
            string backup = FullPath(root, ".steamkit-transfer/" + Path.GetFileNameWithoutExtension(partial) + ".previous");
            if (File.Exists(destination))
            {
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(destination, backup);
            }
            try { File.Move(partial, destination); }
            catch
            {
                if (!File.Exists(destination) && File.Exists(backup)) File.Move(backup, destination);
                throw;
            }
            if (File.Exists(backup)) File.Delete(backup);
        }
    }
}
