using System;
using System.IO;
using JarvisDesk;
public static class TemperatureAlertsTests {
 static readonly DateTime Origin=new DateTime(2026,10,19,12,0,0,DateTimeKind.Utc);
 static readonly TemperatureAlertPreferences Settings=new TemperatureAlertPreferences();
 static double? C(double? f) { return f.HasValue?(double?)((f.Value-32)*5.0/9.0):null; }
 static TemperatureAlertBatch Feed(TemperatureAlertEngine engine,int seconds,double? cpu,double? gpu) { var time=Origin.AddSeconds(seconds); engine.Observe(C(cpu),C(gpu),time,time,Settings); return engine.Due(time,Settings); }
 static void Check(bool condition,string name) { if(!condition) throw new Exception(name); Console.WriteLine("PASS "+name); }
 static void Invalid(Action action,string name) { bool rejected=false; try { action(); } catch(ArgumentException) { rejected=true; } Check(rejected,name); }
 public static int Main(string[] args) {
  try {
   Check(Settings.Enabled && Settings.CpuLimitF==175 && Settings.GpuLimitF==175 && Settings.CooldownMinutes==5,"User-selected defaults are enabled, above 175°F for CPU and GPU, with a five-minute repeat limit");
   var engine=new TemperatureAlertEngine();
   Check(!Feed(engine,0,175,175).Any && !Feed(engine,3,175,175).Any,"Exactly 175°F does not trigger an above-175 alert");
   Check(!Feed(engine,6,176,120).Any,"A single hot sample does not trigger speech");
   var first=Feed(engine,9,176,120); Check(first.Cpu && !first.Gpu && engine.CpuHigh,"Two confirmed high samples trigger the correct component");
   engine.MarkAttempt(first,Origin.AddSeconds(9)); Check(!Feed(engine,12,180,120).Any,"Confirmed high temperatures do not repeat immediately");
   Check(!Feed(engine,306,180,120).Any,"A sensor gap requires fresh confirmation before repeating");
   var repeated=Feed(engine,309,180,120); Check(repeated.Cpu,"Persistent heat can repeat after the configured cooldown");
   engine=new TemperatureAlertEngine(); Feed(engine,0,180,120); var cpu=Feed(engine,3,180,120); engine.MarkAttempt(cpu,Origin.AddSeconds(3)); Feed(engine,6,180,180); var gpu=Feed(engine,9,180,180);
   Check(!gpu.Cpu && gpu.Gpu,"GPU cooldown is independent of an earlier CPU warning");
   engine=new TemperatureAlertEngine(); Feed(engine,0,180,180); var both=Feed(engine,3,180,180);
   Check(both.Cpu && both.Gpu && both.Message.Contains("CPU and GPU"),"Simultaneous warnings combine into one spoken message");
   engine=new TemperatureAlertEngine(); Feed(engine,0,180,120); Feed(engine,3,160,120);
   Check(!Feed(engine,6,180,120).Any && Feed(engine,9,180,120).Cpu,"A brief spike interrupted by a normal reading needs two new hot samples");
   engine=new TemperatureAlertEngine(); Feed(engine,0,180,120); Feed(engine,3,180,120); Feed(engine,6,null,120);
   Check(!engine.CpuAvailable && !engine.CpuHigh && !Feed(engine,9,180,120).Any && Feed(engine,12,180,120).Cpu,"Missing readings never imply recovery or produce an old warning");
   engine=new TemperatureAlertEngine(); Feed(engine,0,180,120); engine.Observe(C(180),C(120),Origin,Origin.AddSeconds(1),Settings);
   Check(!engine.Due(Origin.AddSeconds(1),Settings).Any,"The same sensor timestamp cannot count twice");
   engine=new TemperatureAlertEngine(); engine.Observe(C(180),C(180),Origin,Origin.AddSeconds(30),Settings);
   Check(!engine.CpuAvailable && !engine.Due(Origin.AddSeconds(30),Settings).Any,"Stale readings cannot trigger warnings");
   engine.Observe(C(180),C(180),Origin.AddMinutes(1),Origin,Settings); Check(!engine.CpuAvailable,"Future-dated readings outside the clock tolerance are rejected");
   engine=new TemperatureAlertEngine(); Feed(engine,0,180,120); Feed(engine,3,180,120);
   Check(!engine.Due(Origin.AddSeconds(16),Settings).Any && !engine.CpuAvailable,"Queued alerts expire when fresh readings stop");
   engine=new TemperatureAlertEngine(); engine.Observe(Double.NaN,Double.PositiveInfinity,Origin,Origin,Settings);
   Check(!engine.CpuAvailable && !engine.GpuAvailable,"Invalid and non-finite temperatures never trigger alerts");
   engine=new TemperatureAlertEngine(); Feed(engine,0,180,180); var incident=Feed(engine,3,180,180); engine.MarkAttempt(incident,Origin.AddSeconds(3)); Feed(engine,6,166,166); Feed(engine,9,166,166); Feed(engine,12,180,180);
   Check(!Feed(engine,15,180,180).Any,"Cooling and reheating inside the cooldown cannot cause warning chatter");
   var disabled=new TemperatureAlertPreferences { Enabled=false }; engine.Observe(C(190),C(190),Origin.AddSeconds(18),Origin.AddSeconds(18),disabled);
   Check(!engine.Due(Origin.AddSeconds(18),disabled).Any && !engine.CpuHigh,"Disabling alerts clears the active incident and pending warnings");
   var custom=new TemperatureAlertPreferences { CpuLimitF=200,GpuLimitF=190,CooldownMinutes=1 }; engine=new TemperatureAlertEngine(); engine.Observe(C(195),C(195),Origin,Origin,custom); engine.Observe(C(195),C(195),Origin.AddSeconds(3),Origin.AddSeconds(3),custom);
   Check(!engine.Due(Origin.AddSeconds(3),custom).Cpu && engine.Due(Origin.AddSeconds(3),custom).Gpu,"Independent custom thresholds compare raw Celsius values to Fahrenheit limits");
   Invalid(delegate { new TemperatureAlertPreferences { CpuLimitF=99 }.Validate(); },"Reject an out-of-range CPU limit");
   Invalid(delegate { new TemperatureAlertPreferences { GpuLimitF=251 }.Validate(); },"Reject an out-of-range GPU limit");
   Invalid(delegate { new TemperatureAlertPreferences { CooldownMinutes=0 }.Validate(); },"Reject zero cooldown to avoid a repeated speech loop");
   var path=Path.Combine(args[0],"temperature-alerts.json"); custom.Save(path); var saved=TemperatureAlertPreferences.Load(path);
   Check(saved.CpuLimitF==200 && saved.GpuLimitF==190 && saved.CooldownMinutes==1,"Alert settings persist across reloads");
   var snapshot=new SystemSnapshot(); SystemMonitor.ApplyTemperatures(snapshot,new SensorReading[] {new SensorReading {Name="CPU Package",Parent="/intelcpu/0",Value=80},new SensorReading {Name="CPU Core #1",Parent="/intelcpu/0",Value=95},new SensorReading {Name="GPU Core",Parent="/gpu-nvidia/0",Value=81},new SensorReading {Name="GPU Hot Spot",Parent="/gpu-nvidia/0",Value=100}});
   Check(snapshot.CpuTemperatureCelsius==80 && snapshot.GpuTemperatureCelsius==81,"Alert inputs use the same preferred package/core sensors as the HUD");
   SystemMonitor.ApplyTemperatures(snapshot,new SensorReading[0]);
   Check(!snapshot.CpuTemperatureCelsius.HasValue && !snapshot.GpuTemperatureCelsius.HasValue,"An empty sensor selection clears raw alert inputs");
   return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
 }
}
