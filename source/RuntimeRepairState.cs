using System;
using System.Globalization;

namespace SpiderManInstaller
{
    // Pure receipt validation, shared by foreground polling and protocol activation.
    internal sealed class RuntimeRepairState
    {
        internal string Status, Message;
        internal bool Play;
        internal static string Begin(string id, DateTime now, bool play)
        { return id+"\n"+now.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture)+"\n"+(play?"play":"repair"); }
        internal static RuntimeRepairState Read(string pending, string receipt, DateTime now)
        {
            if(pending==null)return null;
            var p=pending.Replace("\r","").Split('\n');
            long ticks;Guid id;
            if(p.Length!=3 || !Guid.TryParseExact(p[0],"N",out id) ||
               !long.TryParse(p[1],out ticks) || ticks<0 || ticks>DateTime.MaxValue.Ticks ||
               (p[2]!="play" && p[2]!="repair"))
                return new RuntimeRepairState{Status="error",Message="Previous setup was interrupted. Press Play to retry repair."};
            var age=now.ToUniversalTime()-new DateTime(ticks,DateTimeKind.Utc);
            if(age.TotalMinutes>3 || age.TotalSeconds< -5)
                return new RuntimeRepairState{Status="error",Message="Xbox did not finish game-app setup. Press Play to retry. If this repeats, register the separate runtime through Device Portal. Your game files are preserved."};
            var r=(receipt??"").Replace("\r","").Split('\n');
            if(r.Length<2 || r[0]!=p[0])return new RuntimeRepairState{Status="pending",Message="Repairing the game app and restoring startup settings…"};
            if(r[1]=="ok")return new RuntimeRepairState{Status="ok",Play=p[2]=="play",Message="Game app repaired. Startup settings restored; existing files kept."};
            return new RuntimeRepairState{Status="error",Message=r[1]=="error" ? "Runtime setup stopped: "+(r.Length>2?r[2]:"Please retry.") : "Invalid runtime setup reply. Press Play to retry."};
        }
    }
}
