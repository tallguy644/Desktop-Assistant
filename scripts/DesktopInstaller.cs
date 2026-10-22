using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

class DesktopInstaller : Form {
 readonly ComboBox monitors=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Top};
 readonly CheckBox background=new CheckBox {Text="Interactive desktop on this monitor",Checked=true,AutoSize=true};
 readonly CheckBox startup=new CheckBox {Text="Start with Windows",AutoSize=true};
 readonly Button install=new Button {Text="Install",Width=130,Height=36};
 readonly Label status=new Label {AutoSize=true,MaximumSize=new Size(520,0)};
 static readonly string Target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"JarvisDesk","bin");
 static readonly string Exe=Path.Combine(Target,"Syra.exe");
 bool working,finished;
 static string CompatibilityIssue(){
  if(!Environment.Is64BitOperatingSystem)return "This package requires a 64-bit Windows PC.";
  using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")){int build;if(key==null || !Int32.TryParse(Convert.ToString(key.GetValue("CurrentBuildNumber")),out build) || build<10240)return "Use a Windows 10 or Windows 11 PC for this package.";}
  using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32))using(var key=machine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full")){if(key==null || Convert.ToInt32(key.GetValue("Release",0))<528040)return "Install Microsoft .NET Framework 4.8 or newer, then run this installer again.";}
  return null;
 }
 [STAThread] static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new DesktopInstaller());}
 class ScreenItem {public Screen Screen;public override string ToString(){return Screen.DeviceName+(Screen.Primary?" / primary monitor":" / secondary monitor");}}
 DesktopInstaller(){
  Text="Desktop Assistant Setup";ClientSize=new Size(590,420);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);
  var panel=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(26),FlowDirection=FlowDirection.TopDown,WrapContents=false};Controls.Add(panel);
  panel.Controls.Add(new Label {Text="Your interactive desktop",Font=new Font("Segoe UI",21,FontStyle.Bold),AutoSize=true,Margin=new Padding(0,0,0,12)});
  panel.Controls.Add(new Label {Text="Voice commands, chat, agenda and system sensors in one desktop HUD.\nInstalls for your Windows account; existing settings are kept.",AutoSize=true,Margin=new Padding(0,0,0,16)});
  monitors.Width=530;monitors.Dock=DockStyle.None;foreach(var screen in Screen.AllScreens)monitors.Items.Add(new ScreenItem {Screen=screen});monitors.SelectedIndex=0;for(int i=0;i<monitors.Items.Count;i++)if(((ScreenItem)monitors.Items[i]).Screen.Primary)monitors.SelectedIndex=i;panel.Controls.Add(monitors);
  background.Margin=new Padding(0,14,0,8);panel.Controls.Add(background);panel.Controls.Add(startup);
  using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))startup.Checked=key!=null && (key.GetValue("DesktopAssistant") as string)=="\""+Exe+"\"";
  install.Margin=new Padding(0,16,0,8);panel.Controls.Add(install);panel.Controls.Add(status);
  string issue=CompatibilityIssue();if(issue!=null){status.Text=issue;install.Enabled=false;}
  install.Click+=async delegate { if(finished){Close();return;}
   bool pin=background.Checked,run=startup.Checked;string monitor=((ScreenItem)monitors.SelectedItem).Screen.DeviceName;
   install.Enabled=false;background.Enabled=false;startup.Enabled=false;monitors.Enabled=false;working=true;status.Text="Installing…";
   try{await Task.Run(delegate{Install(pin,run,monitor);});status.Text="Installed. Use the tray icon or Ctrl+Alt+D to change modes.";install.Text="Done";install.Enabled=true;finished=true;}
   catch(Exception ex){status.Text=ex.Message;install.Enabled=true;background.Enabled=true;startup.Enabled=true;monitors.Enabled=true;}
   finally{working=false;}
  };
  FormClosing+=delegate(object sender,FormClosingEventArgs e){if(working)e.Cancel=true;};
 }
 static void Install(bool pin,bool run,string monitor){
  string issue=CompatibilityIssue();if(issue!=null)throw new IOException(issue);
  foreach(var process in Process.GetProcessesByName("Syra").Concat(Process.GetProcessesByName("Skynet"))){
   try{
    if(!String.Equals(process.MainModule.FileName,Exe,StringComparison.OrdinalIgnoreCase))throw new IOException("Close the other assistant installation first.");
    if(process.MainWindowHandle==IntPtr.Zero){using(var signal=Process.Start(new ProcessStartInfo(Exe,"--window") {UseShellExecute=false,CreateNoWindow=true}))signal.WaitForExit(3000);for(int i=0;i<15;i++){System.Threading.Thread.Sleep(150);process.Refresh();if(process.MainWindowHandle!=IntPtr.Zero)break;}}
    if(!process.CloseMainWindow() || !process.WaitForExit(6000))throw new IOException("Exit the assistant from its tray menu, then click Install again.");
   }finally{process.Dispose();}
  }
  string folder=Path.Combine(Path.GetTempPath(),"DesktopAssistantSetup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  try{
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)){
    string prefix=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
    foreach(var entry in zip.Entries){string path=Path.GetFullPath(Path.Combine(folder,entry.FullName));if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("Invalid installer path.");if(String.IsNullOrEmpty(entry.Name)){Directory.CreateDirectory(path);continue;}Directory.CreateDirectory(Path.GetDirectoryName(path));using(var input=entry.Open())using(var output=File.Create(path))input.CopyTo(output);}
   }
   string setup=Path.Combine(folder,"Syra","Setup.ps1");
   var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"),"-NoProfile -ExecutionPolicy Bypass -File \""+setup+"\"") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   using(var process=Process.Start(start)){var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();process.WaitForExit();Task.WaitAll(output,error);if(process.ExitCode!=0)throw new IOException("Setup failed. "+error.Result);}
   using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){string own="\""+Exe+"\"";if(run)key.SetValue("DesktopAssistant",own,RegistryValueKind.String);else if((key.GetValue("DesktopAssistant") as string)==own)key.DeleteValue("DesktopAssistant",false);}
   Process.Start(new ProcessStartInfo(Exe,(pin?"--desktop --monitor="+monitor:"--window")) {UseShellExecute=true});
  }finally{try{if(Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))Directory.Delete(folder,true);}catch{}}
 }
}
