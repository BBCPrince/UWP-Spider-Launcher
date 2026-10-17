using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SpiderManInstaller
{
    internal sealed class GameLanguageOption
    {
        internal string Id, Label, Archive;
        internal long Size;
        public override string ToString() => Label;
    }

    // Only selects installed audio. It never edits the game archives or saves.
    internal static class GameLanguageSettings
    {
        const string ForceName = "force_language.txt";
        const string BackupName = "launcher-language-original.txt";
        const string BackupHeader = "launcher-language-v1\n";

        static void CheckPath(string path)
        {
            for (string p = Path.GetFullPath(path); p != null; p = Path.GetDirectoryName(p))
                if ((Directory.Exists(p) || File.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Game language paths cannot use links.");
        }
        static void Validate(IList<GameLanguageOption> catalogue)
        {
            if (catalogue == null || catalogue.Count == 0) throw new IOException("Language catalogue is missing.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in catalogue)
                if (p == null || String.IsNullOrEmpty(p.Id) || p.Id.Any(c => c < 'a' || c > 'z') || !ids.Add(p.Id) ||
                    String.IsNullOrEmpty(p.Label) || String.IsNullOrEmpty(p.Archive) || p.Archive != Path.GetFileName(p.Archive) ||
                    p.Archive.Contains(":") || p.Archive.Contains("\\") || p.Archive.Contains("/") || p.Archive.Contains("..") || p.Size <= 0)
                    throw new IOException("Invalid game language catalogue.");
        }
        internal static List<GameLanguageOption> Installed(string gameRoot, IList<GameLanguageOption> catalogue)
        {
            Validate(catalogue);
            var result = new List<GameLanguageOption>();
            foreach (var p in catalogue)
            {
                string file = Path.Combine(gameRoot, "asset_archive", p.Archive);
                CheckPath(file);
                // Import and preparation verify the complete archive hashes. Do
                // not reread multi-gigabyte voice packs when opening Settings.
                if (File.Exists(file) && new FileInfo(file).Length == p.Size) result.Add(p);
            }
            return result;
        }
        internal static string Current(string gameRoot)
        {
            string file = Path.Combine(gameRoot, "steam_settings", ForceName);
            CheckPath(file);
            if (!File.Exists(file)) return "";
            if (new FileInfo(file).Length > 1024) throw new IOException("Existing language configuration is too large.");
            return File.ReadAllText(file).Trim();
        }
        static void WriteAtomic(string file, byte[] bytes)
        {
            string temporary = file + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(file)) File.Replace(temporary, file, null);
                else File.Move(temporary, file);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal static void Apply(string gameRoot, string language, IList<GameLanguageOption> catalogue)
        {
            if (language == null) throw new ArgumentNullException(nameof(language));
            Validate(catalogue);
            string folder = Path.Combine(gameRoot, "steam_settings"), file = Path.Combine(folder, ForceName), backup = Path.Combine(folder, BackupName);
            CheckPath(file); CheckPath(backup);
            if (language.Length == 0)
            {
                // Restore a previous user configuration, if we replaced one.
                // With no launcher-owned backup, leave existing files alone.
                if (!File.Exists(backup)) return;
                if (new FileInfo(backup).Length > 2048) throw new IOException("Language backup is invalid.");
                string text = File.ReadAllText(backup);
                if (!text.StartsWith(BackupHeader, StringComparison.Ordinal)) throw new IOException("Language backup is invalid.");
                string original = text.Substring(BackupHeader.Length);
                if (original == "absent") { if (File.Exists(file)) File.Delete(file); }
                else WriteAtomic(file, Convert.FromBase64String(original));
                File.Delete(backup);
                return;
            }
            if (!Installed(gameRoot, catalogue).Any(p => p.Id == language))
                throw new IOException("That voice pack is not installed. Import and prepare it first.");
            Directory.CreateDirectory(folder);
            if (!File.Exists(backup))
            {
                if (File.Exists(file) && new FileInfo(file).Length > 1024) throw new IOException("Existing language configuration is too large.");
                string original = File.Exists(file) ? Convert.ToBase64String(File.ReadAllBytes(file)) : "absent";
                WriteAtomic(backup, Encoding.UTF8.GetBytes(BackupHeader + original));
            }
            WriteAtomic(file, Encoding.ASCII.GetBytes(language + "\n"));
        }
    }
}
