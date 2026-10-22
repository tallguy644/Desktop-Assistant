using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

// Media control for the phone companion. Uses the same media keys as a keyboard,
// so Spotify, YouTube in Chrome/Edge and most players respond without any integration.
namespace JarvisDesk {
 public static class MediaKeys {
  [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  const uint ExtendedKey = 0x1, KeyUp = 0x2;
  static readonly Dictionary<string, byte> keys = new Dictionary<string, byte> {
   { "playpause", 0xB3 }, { "next", 0xB0 }, { "previous", 0xB1 }, { "volup", 0xAF }, { "voldown", 0xAE }, { "mute", 0xAD }
  };
  public static bool Known(string action) { return action != null && keys.ContainsKey(action); }
  public static string Message(string action) {
   switch (action) { case "playpause": return "Play/pause sent."; case "next": return "Next track."; case "previous": return "Previous track."; case "volup": return "Volume up."; case "voldown": return "Volume down."; case "mute": return "Mute toggled."; default: return ""; }
  }
  public static bool Send(string action) {
   byte vk; if (action == null || !keys.TryGetValue(action, out vk)) return false;
   keybd_event(vk, 0, ExtendedKey, UIntPtr.Zero); keybd_event(vk, 0, ExtendedKey | KeyUp, UIntPtr.Zero);
   return true;
  }
 }

 public sealed class NowPlayingInfo { public string Source, Title; public bool Playing; }

 public static class NowPlayingReader {
  static readonly string[] browsers = { "chrome", "msedge", "firefox", "brave", "opera", "vivaldi" };
  static readonly Regex youTube = new Regex(@"^(?:\(\d+\)\s*)?(?<title>.+?) - YouTube(?: Music)?(?:\s+[-\u2013\u2014]\s+.+)?$", RegexOptions.Compiled);
  static NowPlayingInfo cached; static DateTime cachedAt;
  static readonly object gate = new object();

  /// <summary>Picks what's playing from (process name, window title) pairs. Spotify playing wins, then YouTube, then paused Spotify.</summary>
  public static NowPlayingInfo FromWindows(IEnumerable<KeyValuePair<string, string>> windows) {
   NowPlayingInfo spotify = null, tube = null;
   foreach (var w in windows) {
    string name = (w.Key ?? "").ToLowerInvariant(), title = (w.Value ?? "").Trim();
    if (title.Length == 0) continue;
    if (name == "spotify") {
     bool idle = title == "Spotify" || title.StartsWith("Spotify ", StringComparison.Ordinal);
     if (spotify == null || !idle) spotify = new NowPlayingInfo { Source = "Spotify", Title = idle ? "" : title, Playing = !idle };
    } else if (tube == null && browsers.Contains(name)) {
     var m = youTube.Match(title);
     if (m.Success) tube = new NowPlayingInfo { Source = "YouTube", Title = m.Groups["title"].Value.Trim(), Playing = true };
    }
   }
   if (spotify != null && spotify.Playing) return spotify;
   return tube ?? spotify;
  }

  public static NowPlayingInfo Read() {
   lock (gate) {
    if ((DateTime.UtcNow - cachedAt).TotalSeconds < 2) return cached;
    var windows = new List<KeyValuePair<string, string>>();
    foreach (var p in Process.GetProcesses()) {
     try {
      string name = p.ProcessName.ToLowerInvariant();
      if (name == "spotify" || browsers.Contains(name)) { string title = p.MainWindowTitle; if (!String.IsNullOrEmpty(title)) windows.Add(new KeyValuePair<string, string>(name, title)); }
     } catch { } finally { p.Dispose(); }
    }
    cached = FromWindows(windows); cachedAt = DateTime.UtcNow;
    return cached;
   }
  }
 }
}
