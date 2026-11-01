using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

class ImportReview : Form {
    public ImportReview(SortedDictionary<DateTime,string[]> rows,string source) {
        Text="Review timetable before saving"; Width=1250; Height=800; StartPosition=FormStartPosition.CenterParent;
        var split=new SplitContainer {Size=new Size(1200,650),Dock=DockStyle.Fill,SplitterDistance=460};
        var grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.AllCells,RowHeadersVisible=false};
        string[] headers={"Date","Fajr","Fajr jama‘ah","Sunrise","Dhuhr","Dhuhr jama‘ah / khutbah","Asr","Asr jama‘ah","Maghrib","Isha","Isha jama‘ah"};
        for(int i=0;i<headers.Length;i++) grid.Columns.Add("c"+i,headers[i]);
        foreach(var row in rows) grid.Rows.Add(row.Value.Cast<object>().ToArray());
        split.Panel2.Controls.Add(grid);
        if(!Path.GetExtension(source).Equals(".csv",StringComparison.OrdinalIgnoreCase)) {
            var picture=new PictureBox {Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom};
            using(var image=Image.FromFile(source)) picture.Image=new Bitmap(image);
            split.Panel1.Controls.Add(picture); FormClosed+=(s,e)=>picture.Image.Dispose();
        } else split.Panel1.Controls.Add(new Label {Text="CSV timetable\n\nReview the complete month before saving.",Dock=DockStyle.Fill,Padding=new Padding(20),Font=new Font("Segoe UI",14)});
        var bottom=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=95,Padding=new Padding(14),FlowDirection=FlowDirection.LeftToRight};
        bottom.Controls.Add(new Label {Text="These are the printed source times. Matching adhan/jama‘ah times will display with a 5-minute jama‘ah delay.",Width=1160,Height=28});
        var save=new Button {Text="I checked these times — Save",Width=280,Height=35,DialogResult=DialogResult.OK};
        var cancel=new Button {Text="Cancel",Width=100,Height=35,DialogResult=DialogResult.Cancel};
        bottom.Controls.Add(save); bottom.Controls.Add(cancel); CancelButton=cancel;
        Controls.Add(split); Controls.Add(bottom);
    }
}
class SettingsDialog : Form {
    public SettingsDialog() {
        Text="Prayer Times settings"; Width=560; Height=420; StartPosition=FormStartPosition.CenterScreen; FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false;
        var idle=new CheckBox {Text="Show the timetable automatically when idle",Left=25,Top=25,Width=470,Checked=Tables.Settings.IdleEnabled};
        var minutes=new NumericUpDown {Left=170,Top=65,Width=75,Minimum=1,Maximum=120,Value=Tables.Settings.IdleMinutes};
        var ac=new CheckBox {Text="Automatic idle display only while plugged in",Left=25,Top=110,Width=470,Checked=Tables.Settings.MainsOnly};
        var auto=new CheckBox {Text="Check the masjid website for new timetables",Left=25,Top=150,Width=470,Checked=Tables.Settings.AutoCheck};
        var startup=new CheckBox {Text="Start quietly when I sign in to Windows",Left=25,Top=190,Width=470};
        using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) startup.Checked=key!=null && key.GetValue("MarkazPrayerDisplay")!=null;
        var save=new Button {Text="Save",Left=25,Top=310,Width=120,Height=35};
        save.Click+=(s,e)=> { Tables.Settings.IdleEnabled=idle.Checked; Tables.Settings.MainsOnly=ac.Checked; Tables.Settings.AutoCheck=auto.Checked; Tables.Settings.IdleMinutes=(int)minutes.Value; Tables.SaveSettings(); using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) { if(startup.Checked) key.SetValue("MarkazPrayerDisplay","\""+Application.ExecutablePath+"\" --background"); else key.DeleteValue("MarkazPrayerDisplay",false); } Close(); };
        Controls.AddRange(new Control[]{idle,new Label {Text="Idle delay (minutes)",Left=25,Top=68,Width=140},minutes,ac,auto,startup,new Label {Text="Checks start on the last day of the month, retry every six hours when needed, and catch up when the app next runs. Images are read locally.",Left=25,Top=235,Width=475,Height=60},save});
    }
}
