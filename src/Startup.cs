using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

static class Startup {
    const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ApprovalKey=@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
    public static string ShortcutPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Prayer Times.lnk"); } }
    public static bool Enabled {
        get {
            if(!File.Exists(ShortcutPath)) return false;
            using(var key=Registry.CurrentUser.OpenSubKey(ApprovalKey)) {
                var state=key==null?null:key.GetValue("Prayer Times.lnk") as byte[];
                return state==null || state.Length==0 || (state[0]!=3 && state[0]!=7);
            }
        }
    }
    public static void SetEnabled(bool enabled) {
        if(enabled) {
            Directory.CreateDirectory(Path.GetDirectoryName(ShortcutPath));
            dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic shortcut=shell.CreateShortcut(ShortcutPath);
            try {
                shortcut.TargetPath=Application.ExecutablePath;
                shortcut.Arguments="--background";
                shortcut.WorkingDirectory=Program.Folder;
                shortcut.IconLocation=Application.ExecutablePath+",0";
                shortcut.Description="Start Prayer Times quietly when you sign in";
                shortcut.Save();
            } finally {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
            // An explicit enable action also clears a previous Task Manager disable.
            using(var key=Registry.CurrentUser.OpenSubKey(ApprovalKey,true)) if(key!=null) key.DeleteValue("Prayer Times.lnk",false);
        } else if(File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
        // Migrate the older registration only after the shortcut was saved successfully.
        using(var key=Registry.CurrentUser.OpenSubKey(RunKey,true)) if(key!=null) key.DeleteValue("MarkazPrayerDisplay",false);
    }
}
