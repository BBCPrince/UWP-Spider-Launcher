using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpiderManInstaller
{
    // Exact, audited Steam builds. Unknown executables are still rejected;
    // accepting the experimental profile does not disable file verification.
    public static class GameVersionSupport
    {
        public const string Original="3.0618",Newer="4.0630";
        public const long NewExecutableSize=121901008;
        public const string NewExecutableHash="e297d4d94f1ffe4febf289745e79e7b6fa233a788e7a00f480fc77c55db81ad1";
        public const string OriginalExecutableHash="9154e4e7bedbc5704da75ed1a362286bfc287d4cab5168c4ce6d22da18f94db1";
        static readonly ExpectedFile[] Changed={
            new ExpectedFile{Path="Spider-Man.exe",Size=NewExecutableSize,Hash=NewExecutableHash},
            new ExpectedFile{Path="amd_fidelityfx_dx12.dll",Size=6675664,Hash="12a5081257ec95b0b53ad51b4a87fb3c03f97fe0bbb59f9496968f8d50ef93a6"},
            new ExpectedFile{Path="crs-client.dll",Size=479400,Hash="3436f313213dc9cbc22329401e0b1e427c135b22a9c55b1bd3725a31c0dbe464"},
            new ExpectedFile{Path="crs-handler.exe",Size=1247400,Hash="5e522db78f20c7c97823588ea2b9814e2a94c90c327eba05d64e9dd733a0b2b2"},
            new ExpectedFile{Path="gattaca.dll",Size=262144,Hash="56086c1885acfd6d0ea99b732ec06e86cd8150709b916d9027711e49417dbc8f"}
        };
        public static IList<ExpectedFile> Plan(IList<ExpectedFile> original,long executableSize)
        {
            if(executableSize!=NewExecutableSize)return original;
            return original.Select(row=>{
                var replacement=Changed.SingleOrDefault(r=>String.Equals(r.Path,row.Path,StringComparison.OrdinalIgnoreCase));
                return replacement==null?row:new ExpectedFile{Path=replacement.Path,Size=replacement.Size,Hash=replacement.Hash};
            }).ToList();
        }
        public static IList<ExpectedFile> Select(IList<ExpectedFile> original,long executableSize,string executableHash)
        {
            var baseline=original.Single(r=>r.Path.Equals("Spider-Man.exe",StringComparison.OrdinalIgnoreCase));
            if(executableSize==baseline.Size&&String.Equals(executableHash,baseline.Hash,StringComparison.OrdinalIgnoreCase))return original;
            if(executableSize==NewExecutableSize&&String.Equals(executableHash,NewExecutableHash,StringComparison.OrdinalIgnoreCase))return Plan(original,executableSize);
            throw new InvalidDataException("Unsupported or damaged Steam executable. Supported builds: 3.0618 and experimental 4.0630.");
        }
        internal static string Inspect(string executable)
        {
            using(var file=File.OpenRead(executable)){
                string hash=ImportEngine.Digest(file);
                if(hash==OriginalExecutableHash&&file.Length==121920952)return Original;
                if(hash==NewExecutableHash&&file.Length==NewExecutableSize)return Newer;
                throw new InvalidDataException("The executable does not match either supported Steam build. Original files were preserved.");
            }
        }
        internal static string ReadPreparedProfile(string runtime)
        {
            string marker=Path.Combine(runtime,"game-version.txt");
            string profile=File.Exists(marker)?File.ReadAllText(marker).Trim():Original;
            if(profile!=Original&&profile!=Newer)throw new InvalidDataException("Prepared game-version marker is invalid. Prepare your original files again.");
            return profile;
        }
    }
}
