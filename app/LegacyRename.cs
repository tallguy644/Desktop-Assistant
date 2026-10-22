using System;
using System.IO;
using Microsoft.Win32;

namespace JarvisDesk {
 public partial class Desk {
  // Skynet.exe was renamed to Syra.exe. If "Start with Windows" still points at the old file
  // in this same folder (for example after replacing the exe by hand), point it at this one.
  void MigrateLegacyStartup() {
   try {
    if (PackageEnvironment.IsPackaged) return;
    string self = System.Reflection.Assembly.GetExecutingAssembly().Location;
    string legacy = Path.Combine(Path.GetDirectoryName(self), "Skynet.exe");
    if (String.Equals(self, legacy, StringComparison.OrdinalIgnoreCase)) return;
    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
     if (key == null) return;
     var value = key.GetValue("DesktopAssistant") as string;
     if (String.Equals(value, "\"" + legacy + "\"", StringComparison.OrdinalIgnoreCase)) key.SetValue("DesktopAssistant", "\"" + self + "\"", RegistryValueKind.String);
    }
   } catch { /* Startup can still be set again from Setup > Desktop & startup. */ }
  }
 }
}
