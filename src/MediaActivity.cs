using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Media.Control;

// Reads playback state only: no media titles, URLs, history or screen capture.
static class MediaActivity {
    static GlobalSystemMediaTransportControlsSessionManager manager;
    static bool checking;
    static bool initialized;
    static DateTime nextCheck;
    public static bool Playing { get; private set; }
    public static bool BlocksAutomaticDisplay { get { return !initialized || Playing; } }
    public static bool IsPlaybackActive(GlobalSystemMediaTransportControlsSessionPlaybackStatus status) {
        return status==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
    }
    public static async Task<bool> ReadPlaybackAsync() {
        if(manager==null) {
            var request=GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            try {
                var deadline=DateTime.UtcNow.AddSeconds(5);
                while(request.Status==Windows.Foundation.AsyncStatus.Started) {
                    if(DateTime.UtcNow>deadline) { request.Cancel(); throw new TimeoutException(); }
                    await Task.Delay(50);
                }
                manager=request.GetResults();
            } finally { request.Close(); }
        }
        foreach(var session in manager.GetSessions()) {
            try { if(IsPlaybackActive(session.GetPlaybackInfo().PlaybackStatus)) return true; }
            catch(COMException) { /* A session may close while being read. */ }
        }
        return false;
    }
    public static async Task RefreshAsync() {
        if(checking || DateTime.UtcNow<nextCheck) return;
        checking=true;
        try { Playing=await ReadPlaybackAsync(); }
        catch { Playing=false; manager=null; }
        finally { initialized=true; nextCheck=DateTime.UtcNow.AddSeconds(2); checking=false; }
    }
}
