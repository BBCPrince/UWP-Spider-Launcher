// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using SpiderManInstaller.SteamTransfer;
using SteamKit2;

namespace SpiderManInstaller
{
    internal sealed class SteamLanguageDepot
    {
        internal uint Id;
        internal ulong Manifest;
    }
    internal static class SteamLanguages
    {
        internal const string PreferenceFile = "steam-selected-language.txt";
        static readonly Dictionary<string, uint> AudioDepots = new Dictionary<string, uint>(StringComparer.Ordinal) {
            {"english",1817074},{"french",1817075},{"german",1817076},{"italian",1817077},
            {"japanese",1817078},{"polish",1817079},{"portuguese",1829890},{"brazilian",1829891},
            {"russian",1829892},{"spanish",1829893},{"latam",1829894},{"arabic",1829895}
        };
        internal static LanguagePack Pack(IList<LanguagePack> catalogue, string language)
        {
            LanguageImports.Validate(catalogue);
            LanguagePack pack = catalogue.SingleOrDefault(p => p.Language == language);
            if (pack == null || !AudioDepots.ContainsKey(language)) throw new InvalidDataException("Unsupported Steam audio language.");
            return pack;
        }
        internal static List<ExpectedFile> SelectFiles(IList<ExpectedFile> original, IList<LanguagePack> catalogue, string language)
        {
            var pack = Pack(catalogue, language);
            var audio = new HashSet<string>(catalogue.Select(p => p.File.Path), StringComparer.OrdinalIgnoreCase);
            var result = original.Where(p => !audio.Contains(p.Path)).ToList();
            result.Add(pack.File);
            return result;
        }
        internal static List<SteamLanguageDepot> Depots(string language, bool languageOnly)
        {
            uint depot;
            if (language == null || !AudioDepots.TryGetValue(language, out depot)) throw new InvalidDataException("Unsupported Steam audio language.");
            var result = new List<SteamLanguageDepot>();
            if (!languageOnly) {
                result.Add(new SteamLanguageDepot {Id=1817071, Manifest=7101193785692308687});
                result.Add(new SteamLanguageDepot {Id=1817072, Manifest=670672075597558132});
            }
            result.Add(new SteamLanguageDepot {Id=depot, Manifest=language=="english"?2767115330843299188UL:0});
            return result;
        }
        internal static ulong PublicManifest(KeyValue product, uint depot, string language)
        {
            // Voice archives must still match the pinned size AND full SHA-256.
            // Steam supplies the current manifest, never an arbitrary URL or a
            // replacement executable. New incompatible audio is rejected.
            var node = product["depots"][depot.ToString(CultureInfo.InvariantCulture)];
            string declared = node["config"]["language"].Value;
            if (!String.IsNullOrEmpty(declared) && declared != language) throw new InvalidDataException("Steam audio depot language differs.");
            var manifest = node["manifests"]["public"];
            string value = manifest["gid"].Value ?? manifest.Value;
            ulong id;
            if (!UInt64.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) || id == 0)
                throw new TransferProblem("Steam did not provide a public manifest for this voice pack. Try again later.");
            return id;
        }
        internal static void InstallVoice(string downloadedRoot, string gameRoot, LanguagePack pack, CancellationToken token)
        {
            // Both locations are fixed launcher-owned directories. Verify again
            // immediately before the move; a failed transfer cannot replace audio.
            TransferCore.Verify(downloadedRoot, pack.File, token);
            string source=TransferCore.FullPath(downloadedRoot,pack.File.Path),target=TransferCore.FullPath(gameRoot,pack.File.Path);
            token.ThrowIfCancellationRequested();
            if(File.Exists(target)) {
                // Existing installed packs are preserved, including when in use.
                TransferCore.Verify(gameRoot,pack.File,token);return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Move(source,target);
        }
        internal static void ApplyImportPreference(string import, string gameRoot, IList<LanguagePack> catalogue)
        {
            string file=TransferCore.FullPath(import,PreferenceFile);
            if(!File.Exists(file))return;
            if(new FileInfo(file).Length>64)throw new InvalidDataException("Invalid Steam language preference.");
            var pack=Pack(catalogue,File.ReadAllText(file).Trim());
            GameLanguageSettings.Apply(gameRoot,pack.Language,catalogue.Select(p=>new GameLanguageOption {
                Id=p.Language,Label=p.Label,Archive=Path.GetFileName(p.File.Path),Size=p.File.Size}).ToList());
        }
    }
}
