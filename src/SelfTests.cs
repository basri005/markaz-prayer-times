using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

static class SelfTests {
    static void Check(bool condition,string name) { if(!condition) throw new Exception("FAIL: "+name); lines.Add("PASS: "+name); }
    static List<string> lines=new List<string>();
    public static int Run(string[] args) {
        try {
            Tables.Initialize(Program.Folder,Path.Combine(Path.GetTempPath(),"PrayerTimesTests-"+Guid.NewGuid().ToString("N")));
            Check(Program.Days.Count==30,"complete November seed");
            var row=Program.Days[new DateTime(2026,11,1)];
            Check(Program.Jamaah(row,3)==new TimeSpan(16,43,0),"Maghrib 16:38 becomes jama‘ah 16:43");
            Check(Program.Jamaah(row,0)==new TimeSpan(5,30,0),"distinct jama‘ah time is unchanged");
            var equal=(string[])row.Clone(); equal[2]=equal[1]; equal[5]=equal[4]; equal[7]=equal[6]; equal[10]=equal[9];
            foreach(int prayer in new[]{0,1,2,4}) Check(Program.Jamaah(equal,prayer)==TimeSpan.Parse(equal[new[]{1,4,6,8,9}[prayer]]).Add(TimeSpan.FromMinutes(5)),"matching-time rule prayer "+prayer);
            Check(Program.Next(new DateTime(2026,11,1,16,38,0)).Item1==new DateTime(2026,11,1,16,43,0),"countdown uses adjusted Maghrib");
            Check(Program.Midnight(new DateTime(2026,11,1))==new DateTime(2026,11,1,22,57,0),"midnight uses raw Maghrib and next Fajr, rounded down");
            Check(Program.Next(new DateTime(2026,11,1,5,30,0)).Item2=="Sunrise","sunrise countdown");
            Check(Program.Next(new DateTime(2026,11,1,20,0,0)).Item2=="Midnight","midnight countdown");
            Check(!Program.Midnight(new DateTime(2026,11,30)).HasValue,"missing next Fajr is not fabricated");
            Check(Tables.WantedMonths(new DateTime(2026,11,29)).Length==0,"no premature next-month check");
            Check(Tables.WantedMonths(new DateTime(2026,11,30)).Single()==new DateTime(2026,12,1),"last-day check targets next month");
            Check(Tables.WantedMonths(new DateTime(2026,12,2)).Single()==new DateTime(2026,12,1),"missed rollover catches up");
            Check(Tables.WantedMonths(new DateTime(2028,2,29)).Contains(new DateTime(2028,3,1)),"leap-year rollover");
            Check(Tables.WantedMonths(new DateTime(2026,12,31)).Contains(new DateTime(2027,1,1)),"year rollover");
            Check(IdleContext.ShouldShow(300000,true,false,false,301) && !IdleContext.ShouldShow(299999,true,false,false,301) && !IdleContext.ShouldShow(400000,false,false,false,301) && !IdleContext.ShouldShow(400000,true,false,true,301),"idle, AC and lock conditions");
            Tables.Settings.IdleMinutes=2; Tables.Settings.MainsOnly=false;
            Check(IdleContext.ShouldShow(120000,false,false,false,121),"configurable idle and battery options");
            Check(!IdleContext.ShouldShow(900000,true,false,false,900,true),"playing media blocks an otherwise due idle display");
            Check(!IdleContext.ShouldShow(900000,true,false,false,0,false) && IdleContext.ShouldShow(900000,true,false,false,121,false),"full idle delay after playback ends");
            Check(MediaActivity.IsPlaybackActive(Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) && !MediaActivity.IsPlaybackActive(Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused) && !MediaActivity.IsPlaybackActive(Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped),"playing blocks; paused and stopped media do not");
            Check(IdleContext.ShouldShow(900000,true,false,false,900,MediaActivity.IsPlaybackActive(Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)),"paused media permits due idle display regardless of window size");
            if(args.Contains("--media")) TestNativeMedia().GetAwaiter().GetResult();
            string csv=Tables.Serialize(Program.Days); bool refused=false;
            try { Tables.ParseCsv(csv.Replace("2026-11-01,05:15,05:30","2026-11-01,05:15,08:30")); } catch {refused=true;}
            Check(refused,"out-of-order OCR times rejected");
            refused=false; try { Tables.ParseCsv(String.Join("\n",csv.Split('\n').Take(15))); } catch {refused=true;}
            Check(refused,"partial month rejected");
            int image=Array.IndexOf(args,"--image");
            if(image>=0) {
                var imported=Tables.ReadImage(args[image+1],new DateTime(2026,11,1),true);
                Check(Tables.Serialize(imported)==csv,"two OCR passes agree and all 300 times match verified November data");
                refused=false; try { Tables.ReadImage(args[image+1],new DateTime(2026,12,1),true); } catch {refused=true;}
                Check(refused,"stale-month image refused");
            }
            File.WriteAllLines(Path.Combine(Program.Folder,"verification.txt"),lines); return 0;
        } catch(Exception ex) { lines.Add(ex.ToString()); File.WriteAllLines(Path.Combine(Program.Folder,"verification.txt"),lines); return 1; }
    }
    static async System.Threading.Tasks.Task TestNativeMedia() {
        bool baseline=await MediaActivity.ReadPlaybackAsync();
        Check(true,"native Windows media-session query completed");
        if(baseline) { lines.Add("SKIP: controlled media playback test: other media is playing"); return; }
        string wave=Path.Combine(Path.GetTempPath(),"PrayerTimesMediaTest-"+Guid.NewGuid().ToString("N")+".wav");
        using(var writer=new BinaryWriter(File.Create(wave))) {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(16000+36); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(16000); writer.Write(new byte[16000]);
        }
        try {
            using(var source=Windows.Media.Core.MediaSource.CreateFromUri(new Uri(wave)))
            using(var player=new Windows.Media.Playback.MediaPlayer()) {
                player.Volume=0; player.IsLoopingEnabled=true; player.Source=source; player.Play();
                bool detected=false;
                for(int i=0;i<30;i++) { await System.Threading.Tasks.Task.Delay(200); if(await MediaActivity.ReadPlaybackAsync()) { detected=true; break; } }
                Check(detected,"real muted Windows media playback detected");
                player.Pause();
                bool stopped=false;
                for(int i=0;i<30;i++) { await System.Threading.Tasks.Task.Delay(200); if(!await MediaActivity.ReadPlaybackAsync()) { stopped=true; break; } }
                Check(stopped,"paused Windows media releases suppression");
            }
        } finally { File.Delete(wave); }
    }
}
