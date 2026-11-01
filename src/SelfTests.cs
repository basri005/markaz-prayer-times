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
}
