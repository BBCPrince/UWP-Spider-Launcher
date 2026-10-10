using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using SteamKit2;
using SpiderManInstaller;

namespace DepotDownloader
{
    // Deliberately memory-only: no passwords, refresh tokens, sentry data or QR URLs on disk.
    internal sealed class AccountSettingsStore
    {
        public static AccountSettingsStore Instance = new AccountSettingsStore();
        public Dictionary<string, byte[]> SentryData = new Dictionary<string, byte[]>();
        public ConcurrentDictionary<string, int> ContentServerPenalty = new ConcurrentDictionary<string, int>();
        public Dictionary<string, string> LoginTokens = new Dictionary<string, string>();
        public static void Save() { }
    }
    internal static class HttpClientFactory
    {
        public static HttpClient CreateHttpClient() => new HttpClient();
    }
    internal static class PlatformUtilities
    {
        // Executable permission bits have no meaning for data stored in UWP LocalState.
        public static void SetExecutable(string path, bool executable) { }
    }
    internal static class DownloadUi
    {
        internal static Action<string> Status = s => { };
        internal static Action<string> ShowQr = s => { };
        internal static DownloadProgressTracker Progress;
        internal static Dictionary<string, ExpectedFile> Expected;
        internal static DateTime last;
        internal static readonly object Sync = new object();
        public static TextWriter Error = new StatusWriter();
        public static void Write(string text, params object[] values) => WriteLine(text, values);
        public static void WriteLine() { }
        public static void WriteLine(string text, params object[] values)
        {
            lock (Sync)
            {
                if ((DateTime.UtcNow - last).TotalMilliseconds < 500) return;
                last = DateTime.UtcNow;
                Status(values.Length == 0 ? text : string.Format(text, values));
            }
        }
        public static string ReadLine() => throw new NotSupportedException("Please cancel and sign in again using Steam mobile QR sign-in.");
        public static void ValidateFile(string name, ulong size, EDepotFileFlag flags)
        {
            string safe = ImportEngine.SafePath(name);
            ExpectedFile expected;
            if (Expected == null || !Expected.TryGetValue(safe, out expected) || size != (ulong)expected.Size)
                throw new InvalidDataException("Steam serves a different game version: " + safe + ". Use the supported USB/Wi-Fi import instead.");
            if (flags.HasFlag(EDepotFileFlag.Directory) || flags.HasFlag(EDepotFileFlag.Symlink))
                throw new InvalidDataException("Expected a regular game file: " + safe);
        }
        sealed class StatusWriter : StringWriter
        {
            public override void WriteLine(string value) { DownloadUi.WriteLine(value); }
        }
    }
}
