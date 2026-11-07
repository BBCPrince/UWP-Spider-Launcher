using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace SpiderManInstaller
{
    public static class MapOfflineRepair
    {
        const long ModuleSize = 121923072;
        const string StockHash = "ee1bc97bc4c18ff8c6f2684e809e5233410ed4636becb71314204377fc937867";
        const string RepairedHash = "ac265d8017453ba07186043c246c7992f2df303fa05caabf2858c4e1abd319c7";
        static readonly long[] OfflineChecks = { 0x7bc852, 0x7c0a99 };

        internal static bool Supports(long size, string hash)
        {
            return size == ModuleSize && String.Equals(hash, StockHash, StringComparison.OrdinalIgnoreCase);
        }

        // The published 3.0618 compatibility recipe changed TEST AL, AL to XOR AL, AL
        // in map initialization and refresh. Restore the original tests so the game
        // decides when the map is available and still enforces its other restrictions.
        public static bool Ensure(string module, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            string temporary = module + ".map-offline-" + Guid.NewGuid().ToString("N");
            using (var operation = new FileStream(module + ".map-offline.lock", FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose))
            {
                try
                {
                    using (var input = File.OpenRead(module))
                    {
                        if (input.Length != ModuleSize)
                            throw new IOException("Unsupported prepared game module size.");
                        string hash = Digest(input);
                        if (hash == RepairedHash) return false;
                        if (hash != StockHash)
                            throw new IOException("Unsupported prepared game module. The file was preserved.");

                        cancel.ThrowIfCancellationRequested();
                        input.Position = 0;
                        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                        {
                            input.CopyTo(output);
                            foreach (long offset in OfflineChecks)
                            {
                                output.Position = offset;
                                output.WriteByte(0x84);
                            }
                            output.Flush(true);
                            output.Position = 0;
                            if (Digest(output) != RepairedHash)
                                throw new IOException("Map repair output failed verification.");
                        }
                    }

                    cancel.ThrowIfCancellationRequested();
                    string backup = module + ".before-map-offline-fix";
                    File.Replace(temporary, module, File.Exists(backup) ? null : backup);
                    return true;
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }
        }

        static string Digest(Stream file)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }
    }
}
