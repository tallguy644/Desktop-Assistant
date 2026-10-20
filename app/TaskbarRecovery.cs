namespace JarvisDesk {
 static class TaskbarRecovery {
  static int Main(string[] args){return args.Length>0 && args[0]=="--taskbar-guard"?DesktopTaskbar.RunGuard(args):2;}
 }
}
