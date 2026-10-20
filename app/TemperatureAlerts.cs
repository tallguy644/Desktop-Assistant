using System;
using System.IO;

namespace JarvisDesk {
 public sealed class TemperatureAlertPreferences {
  public bool Enabled=true;
  public int CpuLimitF=175, GpuLimitF=175, CooldownMinutes=5;
  public void Validate() {
   if(CpuLimitF<100 || CpuLimitF>250 || GpuLimitF<100 || GpuLimitF>250 || CooldownMinutes<1 || CooldownMinutes>60) throw new ArgumentException("Use limits from 100 to 250 °F and a repeat interval from 1 to 60 minutes.");
  }
  public static TemperatureAlertPreferences Load(string path) {
   var value=File.Exists(path)?ChatJson.Serializer().Deserialize<TemperatureAlertPreferences>(File.ReadAllText(path)):new TemperatureAlertPreferences();
   if(value==null) throw new ArgumentException("Temperature alert settings are empty."); value.Validate(); return value;
  }
  public void Save(string path) { Validate(); string temporary=path+".tmp"; File.WriteAllText(temporary,ChatJson.Encode(this)); if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path); }
 }
 public sealed class TemperatureAlertBatch {
  public bool Cpu, Gpu;
  public bool Any { get { return Cpu || Gpu; } }
  public string Message { get { return Cpu && Gpu?"Warning. Your CPU and GPU temperatures are above your alert limits. Please check " + AssistantIdentity.CurrentName + ".":"Warning. Your "+(Cpu?"CPU":"GPU")+" temperature is above your alert limit. Please check " + AssistantIdentity.CurrentName + "."; } }
 }
 public sealed class TemperatureAlertEngine {
  sealed class Channel {
   public int HighSamples, CoolSamples;
   public bool Active;
   public double? CurrentF;
   public DateTime LastAttempt=DateTime.MinValue;
   public void Read(double? celsius,int limit) {
    CurrentF=celsius.HasValue && SystemMonitor.ValidTemperature(celsius.Value)?(double?)(celsius.Value*9.0/5.0+32):null;
    if(!CurrentF.HasValue) { HighSamples=0; CoolSamples=0; return; }
    if(CurrentF.Value>limit) { CoolSamples=0; HighSamples=Math.Min(2,HighSamples+1); if(HighSamples>=2) Active=true; }
    else { HighSamples=0; if(CurrentF.Value<=limit-9) { CoolSamples=Math.Min(2,CoolSamples+1); if(CoolSamples>=2) Active=false; } else CoolSamples=0; }
   }
   public bool Due(DateTime now,int cooldown) { return Active && HighSamples>=2 && CurrentF.HasValue && (LastAttempt==DateTime.MinValue || now-LastAttempt>=TimeSpan.FromMinutes(cooldown)); }
   public void Missing() { CurrentF=null; HighSamples=0; CoolSamples=0; }
  }
  Channel cpu=new Channel(), gpu=new Channel();
  DateTime lastSample=DateTime.MinValue;
  public bool CpuHigh { get { return cpu.Active && cpu.HighSamples>=2 && cpu.CurrentF.HasValue; } }
  public bool GpuHigh { get { return gpu.Active && gpu.HighSamples>=2 && gpu.CurrentF.HasValue; } }
  public bool CpuAvailable { get { return cpu.CurrentF.HasValue; } }
  public bool GpuAvailable { get { return gpu.CurrentF.HasValue; } }
  public void Reset() { cpu=new Channel(); gpu=new Channel(); lastSample=DateTime.MinValue; }
  public void Expire(DateTime now) { if(lastSample==DateTime.MinValue || now-lastSample>TimeSpan.FromSeconds(12) || lastSample>now.AddSeconds(5)) { cpu.Missing(); gpu.Missing(); } }
  public void Observe(double? cpuC,double? gpuC,DateTime readAt,DateTime now,TemperatureAlertPreferences settings) {
   if(!settings.Enabled) { Reset(); return; }
   if(readAt==default(DateTime) || now-readAt>TimeSpan.FromSeconds(12) || readAt>now.AddSeconds(5) || readAt<lastSample) { cpu.Missing(); gpu.Missing(); return; }
   if(readAt==lastSample) { Expire(now); return; }
   if(lastSample!=DateTime.MinValue && readAt-lastSample>TimeSpan.FromSeconds(12)) { cpu.Missing(); gpu.Missing(); }
   lastSample=readAt; cpu.Read(cpuC,settings.CpuLimitF); gpu.Read(gpuC,settings.GpuLimitF);
  }
  public TemperatureAlertBatch Due(DateTime now,TemperatureAlertPreferences settings) {
   Expire(now); return new TemperatureAlertBatch { Cpu=settings.Enabled && cpu.Due(now,settings.CooldownMinutes),Gpu=settings.Enabled && gpu.Due(now,settings.CooldownMinutes) };
  }
  public void MarkAttempt(TemperatureAlertBatch batch,DateTime now) { if(batch.Cpu) cpu.LastAttempt=now; if(batch.Gpu) gpu.LastAttempt=now; }
 }
}
