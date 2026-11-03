using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using System.Threading.Tasks;

[assembly: System.Reflection.AssemblyTitle("Prayer Times")]
[assembly: System.Reflection.AssemblyProduct("Prayer Times")]
[assembly: System.Reflection.AssemblyDescription("Markaz us-Sunnah prayer timetable")]

static class Program {
    public static string Folder = AppDomain.CurrentDomain.BaseDirectory;
    public static SortedDictionary<DateTime,string[]> Days { get { return Tables.Days; } }
    public static DateTime? PreviewDate;
    public static DateTime Now { get { return PreviewDate??TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,"GMT Standard Time"); } }
    public static void Load() {
        Tables.Reload();
    }
    public static TimeSpan Jamaah(string[] row,int prayer) {
        int[] starts={1,4,6,8,9},ends={2,5,7,8,10};
        TimeSpan start=TimeSpan.Parse(row[starts[prayer]]), congregation=TimeSpan.Parse(row[ends[prayer]]);
        return congregation==start?congregation.Add(TimeSpan.FromMinutes(5)):congregation;
    }
    public static Tuple<DateTime,string> Next(DateTime now) {
        var events=new List<Tuple<DateTime,string>>();
        foreach(var d in Days.Where(x=>x.Key>=now.Date.AddDays(-1))) {
            int[] cols={2,3,5,7,8,10};
            string[] names={"Fajr jama‘ah","Sunrise",d.Key.DayOfWeek==DayOfWeek.Friday?"Jumu‘ah khutbah":"Dhuhr jama‘ah","Asr jama‘ah","Maghrib","Isha jama‘ah"};
            for(int i=0;i<cols.Length;i++) {
                DateTime time=d.Key.Add(i==1?TimeSpan.Parse(d.Value[3]):Jamaah(d.Value,i==0?0:i-1));
                events.Add(Tuple.Create(time,names[i]));
            }
            DateTime? midnight=Midnight(d.Key);
            if(midnight.HasValue) events.Add(Tuple.Create(midnight.Value,"Midnight"));
        }
        return events.Where(x=>x.Item1>now).OrderBy(x=>x.Item1).FirstOrDefault();
    }
    public static DateTime? Midnight(DateTime day) {
        string[] today,tomorrow;
        if(!Days.TryGetValue(day.Date,out today) || !Days.TryGetValue(day.Date.AddDays(1),out tomorrow)) return null;
        var zone=TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        DateTime sunset=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.Date.Add(TimeSpan.Parse(today[8])),DateTimeKind.Unspecified),zone);
        DateTime dawn=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.Date.AddDays(1).Add(TimeSpan.Parse(tomorrow[1])),DateTimeKind.Unspecified),zone);
        DateTime midpoint=TimeZoneInfo.ConvertTimeFromUtc(sunset.AddTicks((dawn-sunset).Ticks/2),zone);
        return midpoint.AddTicks(-(midpoint.Ticks % TimeSpan.TicksPerMinute));
    }
    [STAThread] public static void Main(string[] args) {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if(args.Contains("--enable-startup") || args.Contains("--disable-startup")) {
            try { Startup.SetEnabled(args.Contains("--enable-startup")); }
            catch(Exception) { Environment.Exit(1); }
            return;
        }
        if(args.Contains("--test")) { Environment.Exit(SelfTests.Run(args)); return; }
        string dataOverride=null;
        int dataIndex=Array.IndexOf(args,"--data-dir"); if(dataIndex>=0 && dataIndex+1<args.Length) dataOverride=args[dataIndex+1];
        int dateIndex=Array.IndexOf(args,"--inspect-date"); if(dataOverride!=null && dateIndex>=0) PreviewDate=DateTime.Parse(args[dateIndex+1],System.Globalization.CultureInfo.InvariantCulture);
        try { Tables.Initialize(Folder,dataOverride); } catch(Exception e) { MessageBox.Show("Unable to open saved timetables.\n"+e.Message,"Prayer Times"); return; }
        if(args.Contains("--preview") || args.Contains("--inspect")) { Application.Run(new Display(args.Contains("--inspect"))); return; }
        bool created;
        using(var mutex=new Mutex(true,"Local\\MarkazPrayerDisplay",out created)) {
            if(!created) { if(!args.Contains("--background")) { try { using(var signal=EventWaitHandle.OpenExisting("Local\\MarkazPrayerDisplayShow")) signal.Set(); } catch(WaitHandleCannotBeOpenedException) {} } return; }
            Application.Run(new IdleContext(!args.Contains("--background")));
        }
    }
}

class Display : Form {
    System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    public DateTime Opened=DateTime.UtcNow;
    Point initial;
    Button attach,previous,retry;
    DateTime fallbackFor=DateTime.MinValue,ignoreUpcoming=DateTime.MinValue;
    bool importing,inspectMode;
    public bool Interactive { get { return importing || NeedsAttention(Program.Now); } }
    bool UsingPrevious(DateTime now) { return fallbackFor==now.Date && !Program.Days.ContainsKey(now.Date) && Program.Days.ContainsKey(now.Date.AddDays(-1)); }
    bool NeedsAttention(DateTime now) {
        if(UsingPrevious(now)) return false;
        if(!Program.Days.ContainsKey(now.Date)) return true;
        foreach(var month in Tables.WantedMonths(now)) {
            CheckRecord record;
            if(ignoreUpcoming!=now.Date && Tables.Checks.TryGetValue(Tables.MonthKey(month),out record) && !record.Found) return true;
        }
        return false;
    }
    Color ink=Color.FromArgb(237,241,234), muted=Color.FromArgb(159,181,173), gold=Color.FromArgb(218,185,112);
    public Display(bool inspect=false) {
        inspectMode=inspect;
        Text="Prayer Times"; Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); FormBorderStyle=FormBorderStyle.None;
        Bounds=Screen.PrimaryScreen.Bounds; StartPosition=FormStartPosition.Manual;
        TopMost=true; ShowInTaskbar=true; DoubleBuffered=true; BackColor=Color.FromArgb(12,27,25);
        KeyPreview=true; initial=Cursor.Position;
        KeyDown+=(s,e)=> { if(e.KeyCode==Keys.Escape || (!inspect && !Interactive)) Close(); }; MouseDown+=(s,e)=> { if(!inspect && !Interactive) Close(); };
        MouseMove+=(s,e)=> { if(!inspect && !Interactive && (DateTime.UtcNow-Opened).TotalSeconds>1 && (Math.Abs(Cursor.Position.X-initial.X)>5 || Math.Abs(Cursor.Position.Y-initial.Y)>5)) Close(); };
        attach=MakeButton("Attach timetable",async (s,e)=>await Upload());
        previous=MakeButton("Display yesterday temporarily",(s,e)=> { if(Program.Days.ContainsKey(Program.Now.Date)) ignoreUpcoming=Program.Now.Date; else if(Program.Days.ContainsKey(Program.Now.Date.AddDays(-1))) fallbackFor=Program.Now.Date; Opened=DateTime.UtcNow; initial=Cursor.Position; RefreshButtons(); Invalidate(); });
        retry=MakeButton("Check website again",async (s,e)=>await Tables.CheckAsync(true));
        timer.Interval=1000; timer.Tick+=(s,e)=> { RefreshButtons(); Invalidate(); }; timer.Start();
        Resize+=(s,e)=>RefreshButtons(); RefreshButtons();
        FormClosed+=(s,e)=>timer.Dispose();
    }
    Button MakeButton(string title,EventHandler action) { var b=new Button {Text=title,BackColor=Color.FromArgb(32,58,47),ForeColor=ink,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",12),TabStop=true}; b.Click+=action; Controls.Add(b); return b; }
    void RefreshButtons() {
        if(attach==null) return;
        bool show=NeedsAttention(Program.Now);
        float scale=Math.Min(ClientSize.Width/1440f,ClientSize.Height/900f),ox=(ClientSize.Width-1440*scale)/2,oy=(ClientSize.Height-900*scale)/2;
        var buttons=new[]{attach,previous,retry};
        for(int i=0;i<buttons.Length;i++) { buttons[i].Visible=show; buttons[i].SetBounds((int)(ox+(70+i*440)*scale),(int)(oy+650*scale),(int)(410*scale),(int)(64*scale)); buttons[i].Enabled=!importing && !Tables.Busy; }
        previous.Text=Program.Days.ContainsKey(Program.Now.Date)?"Continue today's timetable":"Display yesterday temporarily";
        previous.Enabled=previous.Enabled && (Program.Days.ContainsKey(Program.Now.Date) || Program.Days.ContainsKey(Program.Now.Date.AddDays(-1)));
    }
    public async Task Upload() {
        importing=true; TopMost=false;
        try {
            using(var picker=new OpenFileDialog {Title="Attach the masjid timetable",Filter="Timetable image or CSV|*.jpg;*.jpeg;*.png;*.csv",Multiselect=false}) {
                if(picker.ShowDialog(this)!=DialogResult.OK) return;
                Cursor=Cursors.WaitCursor;
                var rows=await Task.Run(()=>Tables.Import(picker.FileName)); Cursor=Cursors.Default;
                using(var review=new ImportReview(rows,picker.FileName)) {
                    if(review.ShowDialog(this)==DialogResult.OK) { Tables.Save(rows,"User attached timetable; reviewed before import."); fallbackFor=DateTime.MinValue; ignoreUpcoming=DateTime.MinValue; }
                }
            }
        } catch(Exception ex) { MessageBox.Show(this,"The timetable could not be imported. No saved times were changed.\n\n"+ex.Message+"\n\nTry a clear full-month JPG/PNG, or a CSV in the supplied format.","Timetable needs attention"); }
        finally { Cursor=Cursors.Default; importing=false; TopMost=true; Opened=DateTime.UtcNow; initial=Cursor.Position; RefreshButtons(); Invalidate(); }
    }
    void TextAt(Graphics g,string text,float size,Color color,float x,float y,float w,float h,bool bold=false,StringAlignment align=StringAlignment.Near) {
        using(var font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))
        using(var brush=new SolidBrush(color))
        using(var fmt=new StringFormat { Alignment=align, LineAlignment=StringAlignment.Center })
        g.DrawString(text,font,brush,new RectangleF(x,y,w,h),fmt);
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e); Graphics g=e.Graphics;
        float scale=Math.Min(ClientSize.Width/1440f,ClientSize.Height/900f);
        g.TranslateTransform((ClientSize.Width-1440*scale)/2,(ClientSize.Height-900*scale)/2); g.ScaleTransform(scale,scale);
        g.SmoothingMode=SmoothingMode.AntiAlias; g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
        DateTime now=Program.Now;
        TextAt(g,"MARKAZ US-SUNNAH",25,gold,70,40,700,40,true);
        TextAt(g,"ALPERTON  /  PRAYER TIMETABLE",16,muted,70,82,700,30);
        TextAt(g,now.ToString("dddd, d MMMM yyyy"),21,ink,840,45,530,40,false,StringAlignment.Far);
        TextAt(g,"LONDON TIME",14,muted,1020,85,350,26,false,StringAlignment.Far);
        TextAt(g,now.ToString("HH:mm"),126,ink,60,132,590,150,true);
        TextAt(g,now.ToString("ss"),32,gold,455,219,80,52);
        string[] row;
        bool fallback=UsingPrevious(now);
        bool valid=Program.Days.TryGetValue(fallback?now.Date.AddDays(-1):now.Date,out row);
        bool attention=NeedsAttention(now);
        var next=valid && !fallback && !attention?Program.Next(now):null;
        using(var brush=new SolidBrush(Color.FromArgb(24,46,40))) g.FillRectangle(brush,750,151,620,137);
        TextAt(g,fallback?"TEMPORARY · YESTERDAY'S TIMES":next==null?"TIMETABLE UPDATE":("NEXT  ·  "+next.Item2.ToUpperInvariant()),17,gold,778,163,560,34,true);
        string countdown=fallback?"Not today's confirmed times":"New timetable needed";
        if(next!=null) { TimeSpan left=next.Item1-now; countdown=String.Format("{0:00}:{1:00}:{2:00}",(int)left.TotalHours,left.Minutes,left.Seconds); }
        TextAt(g,countdown,next==null?27:52,ink,775,198,560,65,true);
        if(attention || !valid) {
            TextAt(g,"Prayer timetable not found",42,ink,70,340,1300,90,true);
            TextAt(g,Tables.Notice(now),25,muted,70,430,1300,170);
            TextAt(g,"Attach a full-month JPG, PNG or CSV. You can review it before saving. Press Esc to return.",20,muted,70,744,1300,60);
        } else {
            TextAt(g,"PRAYER",17,muted,92,325,440,40,true);
            TextAt(g,"BEGINS",17,muted,650,325,300,40,true,StringAlignment.Center);
            TextAt(g,"JAMA‘AH",17,gold,1040,325,300,40,true,StringAlignment.Center);
            DateTime sourceDate=fallback?now.Date.AddDays(-1):now.Date;
            string[] names={"Fajr",sourceDate.DayOfWeek==DayOfWeek.Friday?"Jumu‘ah":"Dhuhr","Asr","Maghrib","Isha"};
            int[] starts={1,4,6,8,9}, ends={2,5,7,8,10};
            for(int i=0;i<5;i++) {
                float y=375+i*76;
                TimeSpan jamaah=Program.Jamaah(row,i);
                bool selected=next!=null && next.Item1.Date==now.Date && next.Item1.TimeOfDay==jamaah;
                if(selected) { using(var b=new SolidBrush(Color.FromArgb(32,58,47))) g.FillRectangle(b,70,y,1300,70); using(var b=new SolidBrush(gold)) g.FillRectangle(b,70,y,4,70); }
                TextAt(g,names[i],32,ink,92,y+5,410,60,true);
                TextAt(g,row[starts[i]],36,muted,650,y+5,300,60,false,StringAlignment.Center);
                TextAt(g,jamaah.ToString(@"hh\:mm"),42,selected?gold:ink,1040,y+3,300,60,true,StringAlignment.Center);
                if(i==1 && sourceDate.DayOfWeek==DayOfWeek.Friday) TextAt(g,"Khutbah",14,gold,1255,y+44,105,24);
            }
            TextAt(g,"SUNRISE  "+row[3],20,next!=null && next.Item2=="Sunrise"?gold:muted,92,773,330,40);
            DateTime? midnight=fallback?null:Program.Midnight(now.Date);
            TextAt(g,"MIDNIGHT  "+(midnight.HasValue?midnight.Value.ToString("HH:mm"):"Needs next Fajr"),20,next!=null && next.Item2=="Midnight"?gold:muted,440,773,430,40);
            var friday=Program.Days.FirstOrDefault(x=>x.Key>=now.Date && x.Key.DayOfWeek==DayOfWeek.Friday);
            TextAt(g,friday.Value==null?"":"JUMU‘AH KHUTBAH  "+friday.Value[5],20,gold,820,773,520,40,false,StringAlignment.Far);
        }
        using(var pen=new Pen(Color.FromArgb(54,75,66))) g.DrawLine(pen,70,830,1370,830);
        TextAt(g,fallback?"TEMPORARY: "+now.AddDays(-1).ToString("d MMM yyyy")+" timetable · Not confirmed for today":now.ToString("MMMM yyyy")+" · Matching adhan/jama‘ah times use a 5-minute jama‘ah delay",16,fallback?gold:muted,70,843,1000,32);
        TextAt(g,attention?"Press Esc to return":"Move mouse or press any key to return",15,muted,1000,843,370,32,false,StringAlignment.Far);
    }
}

class IdleContext : ApplicationContext {
    [StructLayout(LayoutKind.Sequential)] struct LastInput { public uint size; public uint time; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInput info);
    [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint state);
    EventWaitHandle showSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\MarkazPrayerDisplayShow");
    NotifyIcon tray; System.Windows.Forms.Timer timer; Display display; bool paused,locked,manual; DateTime dismissed=DateTime.UtcNow,lastCheck=DateTime.MinValue;
    public IdleContext(bool showNow=false) {
        var menu=new ContextMenuStrip();
        menu.Items.Add("Show timetable now",null,(s,e)=>ShowDisplay());
        var pause=new ToolStripMenuItem("Pause automatic display"); pause.CheckOnClick=true; pause.CheckedChanged+=(s,e)=> { paused=pause.Checked; if(paused && display!=null) display.Close(); }; menu.Items.Add(pause);
        menu.Items.Add("Reload timetable",null,(s,e)=> { try { Program.Load(); } catch(Exception ex) { MessageBox.Show(ex.Message,"Timetable error"); } });
        menu.Items.Add("Attach timetable…",null,async (s,e)=> { ShowDisplay(); if(display!=null) await display.Upload(); });
        menu.Items.Add("Check masjid website",null,async (s,e)=>await Tables.CheckAsync(true));
        menu.Items.Add("Settings…",null,(s,e)=> { using(var settings=new SettingsDialog()) settings.ShowDialog(); });
        menu.Items.Add("Open display folder",null,(s,e)=>System.Diagnostics.Process.Start("explorer.exe",Program.Folder));
        menu.Items.Add("Disable startup and quit",null,(s,e)=> { try { Startup.SetEnabled(false); ExitThread(); } catch(Exception ex) { MessageBox.Show(ex.Message,"Unable to change startup"); } });
        menu.Items.Add("Quit for now",null,(s,e)=>ExitThread());
        tray=new NotifyIcon { Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath),Text="Prayer Times",ContextMenuStrip=menu,Visible=true };
        tray.DoubleClick+=(s,e)=>ShowDisplay();
        SystemEvents.SessionSwitch+=OnSession;
        timer=new System.Windows.Forms.Timer { Interval=500 }; timer.Tick+=Tick; timer.Start();
        if(showNow) ShowDisplay();
    }
    void OnSession(object sender,SessionSwitchEventArgs e) {
        if(e.Reason==SessionSwitchReason.SessionLock || e.Reason==SessionSwitchReason.SessionLogoff || e.Reason==SessionSwitchReason.ConsoleDisconnect || e.Reason==SessionSwitchReason.RemoteDisconnect) { locked=true; if(display!=null) display.Close(); }
        if(e.Reason==SessionSwitchReason.SessionUnlock || e.Reason==SessionSwitchReason.ConsoleConnect || e.Reason==SessionSwitchReason.RemoteConnect) { locked=false; dismissed=DateTime.UtcNow; }
    }
    public static bool ShouldShow(uint idle,bool ac,bool paused,bool locked,double sinceDismiss) { return Tables.Settings.IdleEnabled && idle>=Tables.Settings.IdleMinutes*60000 && (ac || !Tables.Settings.MainsOnly) && !paused && !locked && sinceDismiss>=Tables.Settings.IdleMinutes*60; }
    async void Tick(object sender,EventArgs e) {
        if(DateTime.UtcNow-lastCheck>TimeSpan.FromMinutes(1)) { lastCheck=DateTime.UtcNow; await Tables.CheckAsync(false); }
        if(showSignal.WaitOne(0)) ShowDisplay();
        LastInput info=new LastInput { size=(uint)Marshal.SizeOf(typeof(LastInput)) }; if(!GetLastInputInfo(ref info)) return;
        uint idle=unchecked((uint)Environment.TickCount-info.time);
        bool ac=SystemInformation.PowerStatus.PowerLineStatus==PowerLineStatus.Online;
        if(display!=null) { if((!ac && !manual && Tables.Settings.MainsOnly) || locked || (!display.Interactive && idle<1000 && (DateTime.UtcNow-display.Opened).TotalSeconds>2)) display.Close(); }
        else if(ShouldShow(idle,ac,paused,locked,(DateTime.UtcNow-dismissed).TotalSeconds)) { ShowDisplay(); manual=false; }
    }
    void ShowDisplay() {
        if(locked || display!=null) return;
        manual=true;
        display=new Display();
        display.FormClosed+=(s,e)=> { display=null; dismissed=DateTime.UtcNow; SetThreadExecutionState(0x80000000); };
        if(SystemInformation.PowerStatus.PowerLineStatus==PowerLineStatus.Online) SetThreadExecutionState(0x80000003);
        display.Show(); display.Activate();
    }
    protected override void ExitThreadCore() {
        timer.Stop(); timer.Dispose(); showSignal.Dispose(); SystemEvents.SessionSwitch-=OnSession;
        if(display!=null) display.Close(); tray.Visible=false; tray.Dispose();
        SetThreadExecutionState(0x80000000); base.ExitThreadCore();
    }
}

