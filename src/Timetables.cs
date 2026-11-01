using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Tesseract;

public class Preferences {
    public bool IdleEnabled=true;
    public bool MainsOnly=true;
    public bool AutoCheck=true;
    public int IdleMinutes=5;
}
public class CheckRecord {
    public DateTime AttemptUtc;
    public string Message="";
    public bool Found;
}
static class Tables {
    public const string Header="date,fajr,fajr_jamaah,sunrise,dhuhr,dhuhr_jamaah,asr,asr_jamaah,maghrib,isha,isha_jamaah";
    public static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
    public static string Root;
    public static Preferences Settings=new Preferences();
    public static Dictionary<string,CheckRecord> Checks=new Dictionary<string,CheckRecord>();
    public static SortedDictionary<DateTime,string[]> Days=new SortedDictionary<DateTime,string[]>();
    public static bool Busy;
    public static string Error="";
    public static event Action Changed;
    public static string MonthKey(DateTime date) { return date.ToString("yyyy-MM"); }
    public static void Initialize(string folder,string dataOverride) {
        Root=dataOverride??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MarkazPrayerTimes","Data");
        Directory.CreateDirectory(Root); Directory.CreateDirectory(Path.Combine(Root,"months"));
        try { if(File.Exists(Path.Combine(Root,"settings.json"))) Settings=Json.Deserialize<Preferences>(File.ReadAllText(Path.Combine(Root,"settings.json"))); } catch { Error="Settings could not be read. Using defaults."; }
        Settings.IdleMinutes=Math.Max(1,Math.Min(120,Settings.IdleMinutes));
        try { if(File.Exists(Path.Combine(Root,"checks.json"))) Checks=Json.Deserialize<Dictionary<string,CheckRecord>>(File.ReadAllText(Path.Combine(Root,"checks.json"))); } catch { Checks=new Dictionary<string,CheckRecord>(); }
        if(Directory.GetFiles(Path.Combine(Root,"months"),"*.csv").Length==0 && File.Exists(Path.Combine(folder,"timetable.csv"))) {
            var seed=ParseCsv(File.ReadAllText(Path.Combine(folder,"timetable.csv")));
            foreach(var group in seed.GroupBy(x=>MonthKey(x.Key))) WriteAtomic(Path.Combine(Root,"months",group.Key+".csv"),Serialize(group));
        }
        Reload();
    }
    public static void WriteAtomic(string path,string text) {
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        File.WriteAllText(temp,text);
        if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
    }
    public static void SaveSettings() { WriteAtomic(Path.Combine(Root,"settings.json"),Json.Serialize(Settings)); }
    public static void Reload() {
        var all=new SortedDictionary<DateTime,string[]>();
        foreach(string file in Directory.GetFiles(Path.Combine(Root,"months"),"*.csv")) {
            try { foreach(var row in ParseCsv(File.ReadAllText(file))) all[row.Key]=row.Value; }
            catch { Error="A saved timetable could not be validated: "+Path.GetFileName(file); }
        }
        Days=all;
    }
    public static string Serialize(IEnumerable<KeyValuePair<DateTime,string[]>> rows) { return Header+"\r\n"+String.Join("\r\n",rows.OrderBy(x=>x.Key).Select(x=>String.Join(",",x.Value)))+"\r\n"; }
    public static SortedDictionary<DateTime,string[]> ParseCsv(string csv) {
        var rows=new SortedDictionary<DateTime,string[]>();
        string[] lines=csv.Trim().Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
        if(lines.Length<2 || lines[0].Trim('\uFEFF')!=Header) throw new Exception("The CSV columns do not match the supported timetable format.");
        foreach(string line in lines.Skip(1)) {
            string[] c=line.Trim().Split(',');
            if(c.Length!=11) throw new Exception("A timetable row has missing or extra times.");
            DateTime day=DateTime.ParseExact(c[0],"yyyy-MM-dd",CultureInfo.InvariantCulture);
            if(rows.ContainsKey(day)) throw new Exception("A date appears twice.");
            for(int i=1;i<c.Length;i++) DateTime.ParseExact(c[i],"HH:mm",CultureInfo.InvariantCulture);
            rows.Add(day,c);
        }
        Validate(rows); return rows;
    }
    public static void Validate(SortedDictionary<DateTime,string[]> rows) {
        if(rows.Count==0) throw new Exception("No timetable rows found.");
        foreach(var group in rows.GroupBy(x=>MonthKey(x.Key))) {
            var first=group.First().Key;
            if(group.Count()!=DateTime.DaysInMonth(first.Year,first.Month)) throw new Exception("The timetable must contain every day of the month.");
            foreach(var entry in group) {
                string[] c=entry.Value; var t=c.Skip(1).Select(TimeSpan.Parse).ToArray();
                if(!(t[0]<=t[1] && t[1]<t[2] && t[2]<t[3] && t[3]<=t[4] && t[4]<t[5] && t[5]<=t[6] && t[6]<t[7] && t[7]<t[8] && t[8]<=t[9])) throw new Exception("Prayer times are out of order on "+entry.Key.ToString("d MMM")+". Please check the source.");
                if(t[0].TotalHours>9 || t[3].TotalHours<10 || t[7].TotalHours<14) throw new Exception("Unusual start time detected. Please check the source.");
                string[] prior;
                if(rows.TryGetValue(entry.Key.AddDays(-1),out prior)) {
                    foreach(int col in new[]{1,3,4,6,8,9}) {
                        double delta=Math.Abs((TimeSpan.Parse(c[col])-TimeSpan.Parse(prior[col])).TotalMinutes);
                        // One-hour clock changes are allowed; arbitrary OCR jumps are not.
                        if(delta>15 && Math.Abs(delta-60)>15) throw new Exception("Unexpected day-to-day change on "+entry.Key.ToString("d MMM")+".");
                    }
                }
            }
        }
    }
    public static void Save(SortedDictionary<DateTime,string[]> rows,string provenance) {
        Validate(rows);
        foreach(var group in rows.GroupBy(x=>MonthKey(x.Key))) {
            WriteAtomic(Path.Combine(Root,"months",group.Key+".csv"),Serialize(group));
            WriteAtomic(Path.Combine(Root,"months",group.Key+".source.txt"),provenance+"\r\nImported UTC: "+DateTime.UtcNow.ToString("o"));
        }
        Reload();
    }
    public static DateTime[] WantedMonths(DateTime now) {
        var wanted=new List<DateTime>(); var month=new DateTime(now.Year,now.Month,1);
        if(!Days.ContainsKey(now.Date)) wanted.Add(month);
        if(now.Day==DateTime.DaysInMonth(now.Year,now.Month) && !Days.ContainsKey(month.AddMonths(1))) wanted.Add(month.AddMonths(1));
        return wanted.ToArray();
    }
    public static string Notice(DateTime now) {
        DateTime[] targets=WantedMonths(now);
        if(targets.Length==0) return "";
        DateTime target=targets[0]; CheckRecord record;
        if(Busy) return "Checking the masjid website for "+target.ToString("MMMM yyyy")+"…";
        if(Checks.TryGetValue(MonthKey(target),out record)) return record.Message;
        return target.ToString("MMMM yyyy")+" prayer timetable hasn't been found. Attach it if you have a copy.";
    }
    public static async Task CheckAsync(bool force) {
        if(Busy) return;
        var months=WantedMonths(Program.Now).Where(m=>force || !Checks.ContainsKey(MonthKey(m)) || DateTime.UtcNow-Checks[MonthKey(m)].AttemptUtc>TimeSpan.FromHours(6)).ToArray();
        if(months.Length==0 || (!force && !Settings.AutoCheck)) return;
        Busy=true; if(Changed!=null) Changed();
        try {
            foreach(var month in months) {
                var record=new CheckRecord {AttemptUtc=DateTime.UtcNow};
                try {
                    var rows=await Task.Run(()=>FindOnline(month));
                    Save(rows,"Downloaded from https://musunnah.com/prayer-timetable/; two OCR readings agreed and month validation passed.");
                    record.Found=true; record.Message=month.ToString("MMMM yyyy")+" timetable found and saved.";
                } catch(Exception ex) { record.Message=month.ToString("MMMM yyyy")+" prayer timetable hasn't been found or couldn't be validated. Attach it if you have a copy.\r\n"+ex.Message; }
                Checks[MonthKey(month)]=record;
            }
            WriteAtomic(Path.Combine(Root,"checks.json"),Json.Serialize(Checks));
        } catch(Exception ex) { Error="Unable to save update status: "+ex.Message; }
        finally { Busy=false; if(Changed!=null) Changed(); }
    }
    public static byte[] Fetch(Uri uri,int limit) {
        if(uri.Scheme!="https" || !(uri.Host=="musunnah.com" || uri.Host=="www.musunnah.com")) throw new Exception("Only the official masjid website is accepted.");
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        var req=(HttpWebRequest)WebRequest.Create(uri); req.AllowAutoRedirect=false; req.Timeout=20000; req.ReadWriteTimeout=20000; req.UserAgent="MarkazPrayerTimes/2.0";
        using(var response=(HttpWebResponse)req.GetResponse()) {
            if((int)response.StatusCode!=200) throw new Exception("The website did not return a timetable.");
            using(var input=response.GetResponseStream()) using(var output=new MemoryStream()) {
                byte[] b=new byte[8192]; int n;
                while((n=input.Read(b,0,b.Length))>0) { if(output.Length+n>limit) throw new Exception("Website file is too large."); output.Write(b,0,n); }
                return output.ToArray();
            }
        }
    }
    public static SortedDictionary<DateTime,string[]> FindOnline(DateTime month) {
        var pageUri=new Uri("https://musunnah.com/prayer-timetable/");
        string html=System.Text.Encoding.UTF8.GetString(Fetch(pageUri,2000000));
        var candidates=new List<Uri>();
        foreach(Match match in Regex.Matches(html,@"(?:href|src)\s*=\s*[""']([^""']+\.(?:jpe?g|png)(?:\?[^""']*)?)[""']",RegexOptions.IgnoreCase)) {
            Uri uri;
            if(Uri.TryCreate(pageUri,WebUtility.HtmlDecode(match.Groups[1].Value),out uri) && uri.Host==pageUri.Host && uri.AbsolutePath.Contains("/uploads/") && !Regex.IsMatch(uri.AbsolutePath,@"-\d+x\d+\.")) candidates.Add(uri);
        }
        foreach(var uri in candidates.Distinct().Take(8)) {
            string temp=Path.Combine(Root,Guid.NewGuid().ToString("N")+".download");
            try {
                File.WriteAllBytes(temp,Fetch(uri,15000000));
                var rows=ReadImage(temp,month,true);
                return rows;
            } catch(Exception) { /* Reject stale or unreadable candidates; never substitute another month. */ }
            finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        throw new Exception("No readable image for the requested month is linked on the timetable page. The app will retry in six hours.");
    }
    public static SortedDictionary<DateTime,string[]> Import(string file) {
        if(Path.GetExtension(file).Equals(".csv",StringComparison.OrdinalIgnoreCase)) return ParseCsv(File.ReadAllText(file));
        return ReadImage(file,null,false);
    }
    public static SortedDictionary<DateTime,string[]> ParseOcr(string text,DateTime? required) {
        var match=Regex.Match(text,@"\b(January|February|March|April|May|June|July|August|September|October|November|December)\s+(20\d{2})\b",RegexOptions.IgnoreCase);
        if(!match.Success) throw new Exception("The month and year could not be read.");
        DateTime month=DateTime.ParseExact(match.Value,"MMMM yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None);
        if(required.HasValue && MonthKey(month)!=MonthKey(required.Value)) throw new Exception("This is a different month's timetable.");
        var rows=new SortedDictionary<DateTime,string[]>();
        foreach(string rawLine in text.Split('\n')) { string line=Regex.Replace(rawLine,@"\bF\s*r\s*i\b","Fri",RegexOptions.IgnoreCase);
            var date=Regex.Match(line,@"^[^0-9]*([0-9]{1,2})[^:]*?(Sun|Mon|Tue|Wed|Thu|Fri|Sat)\b",RegexOptions.IgnoreCase);
            if(!date.Success) continue;
            int day=int.Parse(date.Groups[1].Value); if(day<1 || day>DateTime.DaysInMonth(month.Year,month.Month)) throw new Exception("Invalid date detected.");
            var d=new DateTime(month.Year,month.Month,day);
            if(!d.ToString("ddd",CultureInfo.InvariantCulture).Equals(date.Groups[2].Value,StringComparison.OrdinalIgnoreCase)) throw new Exception("Date and weekday do not match.");
            var times=Regex.Matches(line,@"[0-2O][0-9O]:[0-5O][0-9O]").Cast<Match>().Select(x=>x.Value.Replace('O','0')).ToArray();
            if(times.Length!=10) throw new Exception("Could not read all ten times for "+d.ToString("d MMM")+": "+line);
            var c=new[]{d.ToString("yyyy-MM-dd")}.Concat(times).ToArray();
            rows.Add(d,c);
        }
        Validate(rows); return rows;
    }
    public static SortedDictionary<DateTime,string[]> ReadImage(string file,DateTime? month,bool automatic) {
        using(var source=new Bitmap(file)) {
            if(source.Width<500 || source.Height<500 || (long)source.Width*source.Height>20000000) throw new Exception("Please use a clear, full timetable image (up to 20 megapixels).");
            using(var engine=new TesseractEngine(Path.Combine(Program.Folder,"tessdata"),"eng",EngineMode.Default)) {
                var first=ReadPass(engine,source,170,month);
                if(automatic) {
                    var second=ReadPass(engine,source,175,month);
                    if(Serialize(first)!=Serialize(second)) throw new Exception("The two image readings disagree; manual review is needed.");
                }
                return first;
            }
        }
    }
    static SortedDictionary<DateTime,string[]> ReadPass(TesseractEngine engine,Bitmap source,int threshold,DateTime? month) {
        int width=Math.Min(2400,source.Width*2),height=(int)((double)source.Height*width/source.Width);
        using(var image=new Bitmap(width,height,PixelFormat.Format24bppRgb)) {
            using(var g=Graphics.FromImage(image)) { g.Clear(Color.White); g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(source,0,0,width,height); }
            var data=image.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadWrite,PixelFormat.Format24bppRgb);
            byte[] buffer=new byte[data.Stride*height]; Marshal.Copy(data.Scan0,buffer,0,buffer.Length);
            for(int y=0;y<height;y++) for(int x=0;x<width;x++) { int at=y*data.Stride+x*3; byte value=(byte)((buffer[at]*0.114+buffer[at+1]*0.587+buffer[at+2]*0.299)<threshold?0:255); buffer[at]=buffer[at+1]=buffer[at+2]=value; }
            Marshal.Copy(buffer,0,data.Scan0,buffer.Length); image.UnlockBits(data);
            using(var bytes=new MemoryStream()) {
                image.Save(bytes,System.Drawing.Imaging.ImageFormat.Png);
                using(var pix=Pix.LoadFromMemory(bytes.ToArray())) {
                    string header; var boxes=new List<Rect>();
                    using(var page=engine.Process(pix,PageSegMode.SingleBlock)) {
                        header=page.GetText();
                        using(var iterator=page.GetIterator()) {
                            iterator.Begin();
                            do { string word=iterator.GetText(PageIteratorLevel.Word)??""; Rect rect;
                                if(Regex.IsMatch(word,@"[0-9O]{2}:[0-9O]{2}") && iterator.TryGetBoundingBox(PageIteratorLevel.Word,out rect)) boxes.Add(rect);
                            } while(iterator.Next(PageIteratorLevel.Word));
                        }
                    }
                    var groups=new List<List<Rect>>();
                    foreach(var box in boxes.OrderBy(b=>b.Y1+b.Y2)) {
                        double center=(box.Y1+box.Y2)/2.0;
                        var group=groups.FirstOrDefault(list=>Math.Abs(list.Average(b=>(b.Y1+b.Y2)/2.0)-center)<Math.Max(10,(box.Y2-box.Y1)*0.65));
                        if(group==null) {group=new List<Rect>();groups.Add(group);} group.Add(box);
                    }
                    string title=Regex.Match(header,@"\b(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+20\d{2}\b",RegexOptions.IgnoreCase).Value;
                    var lines=new List<string>{title};
                    foreach(var group in groups.Where(list=>list.Count>=8)) {
                        int top=Math.Max(0,group.Min(b=>b.Y1)-6),bottom=Math.Min(height,group.Max(b=>b.Y2)+6);
                        using(var cropped=image.Clone(new Rectangle((int)(width*0.062),top,width-(int)(width*0.062),bottom-top),PixelFormat.Format24bppRgb)) using(var bufferImage=new MemoryStream()) { cropped.Save(bufferImage,System.Drawing.Imaging.ImageFormat.Png); using(var rowPix=Pix.LoadFromMemory(bufferImage.ToArray())) using(var line=engine.Process(rowPix,PageSegMode.SingleLine)) lines.Add(line.GetText().Trim()); }
                    }
                    return ParseOcr(String.Join("\n",lines),month);
                }
            }
        }
    }
}











