using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace JarvisDesk {
 public class SensorReading {
  public string Name, Parent, Identifier;
  public string SensorType = "Temperature";
  public double Value;
 }
 public class SystemSnapshot {
  public double? CpuUsage, RamUsage, GpuUsage;
  public double? CpuTemperatureCelsius, GpuTemperatureCelsius;
  public string GpuUsageDetail = "GPU utilization unavailable";
  public string Memory = "Unavailable", CpuTemperature = "Unavailable", GpuTemperature = "Unavailable";
  public string Board = "No motherboard temperature sensors available.", Details = "", Status = "";
  public string Cooling = "No fan or pump RPM readings available.";
  public string CoolingDetail = "Only RPM sensors exposed by the hardware monitor can be shown.";
  public List<SensorReading> Fans = new List<SensorReading>();
  public DateTime ReadAt;
 }
 public sealed class SystemMonitor {
  [StructLayout(LayoutKind.Sequential)] struct FileTime { public uint Low, High; public ulong Value { get { return ((ulong)High << 32) | Low; } } }
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] class MemoryStatus {
   public uint Length = (uint)Marshal.SizeOf(typeof(MemoryStatus)), Load;
   public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
  }
  [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
  [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus memory);
  ulong previousIdle, previousTotal; bool sampled;
  public static string Kind(SensorReading sensor) {
   string id = (sensor.Parent + " " + sensor.Identifier).ToLowerInvariant();
   if (id.Contains("/intelcpu/") || id.Contains("/amdcpu/")) return "CPU";
   if (id.Contains("/gpu-nvidia/") || id.Contains("/gpu-amd/") || id.Contains("/gpu-intel/") || id.Contains("/gpu-intel-integrated/") || id.Contains("/nvidiagpu/") || id.Contains("/atigpu/") || id.Contains("/intelgpu/")) return "GPU";
   if (id.Contains("/lpc/") || id.Contains("/motherboard/")) return "BOARD";
   return "OTHER";
  }
  public static bool ValidTemperature(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value) && value > -20 && value < 150; }
  public static string FahrenheitDisplay(string text) {
   return Regex.Replace(text ?? "", @"([-+]?\d+(?:\.\d+)?)\s*°C", delegate(Match match) {
    double celsius = Double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    return (celsius * 9.0 / 5.0 + 32).ToString("0.0", CultureInfo.InvariantCulture) + " °F";
   });
  }
  static string Format(SensorReading s) { return s.Name + ": " + s.Value.ToString("0.0", CultureInfo.InvariantCulture) + " °C"; }
  public static void ApplyTemperatures(SystemSnapshot result, IEnumerable<SensorReading> source) {
   result.CpuTemperatureCelsius=null; result.GpuTemperatureCelsius=null; result.CpuTemperature="Unavailable"; result.GpuTemperature="Unavailable";
   var sensors = source.Where(s => s != null && s.SensorType == "Temperature" && ValidTemperature(s.Value) && (s.Name ?? "").IndexOf("distance", StringComparison.OrdinalIgnoreCase) < 0).OrderBy(s => s.Parent).ThenBy(s => s.Name).ToList();
   var cpu = sensors.Where(s => Kind(s) == "CPU").ToList();
   var packages = cpu.Where(s => s.Name.IndexOf("package", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("tctl", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("tdie", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
   var cores = cpu.Where(s => s.Name.IndexOf("core", StringComparison.OrdinalIgnoreCase) >= 0 && s.Name.IndexOf("distance", StringComparison.OrdinalIgnoreCase) < 0).ToList();
   var preferredCpu = packages.Count > 0 ? packages : cores;
   if (preferredCpu.Count > 0) { result.CpuTemperatureCelsius=preferredCpu.Max(s=>s.Value); result.CpuTemperature = result.CpuTemperatureCelsius.Value.ToString("0.0", CultureInfo.InvariantCulture) + " °C · hottest " + (packages.Count > 0 ? "package / die" : "core"); }
   else { var fallback = cpu.Where(s => s.Name.IndexOf("distance", StringComparison.OrdinalIgnoreCase) < 0).OrderByDescending(s => s.Value).FirstOrDefault(); if (fallback != null) { result.CpuTemperatureCelsius=fallback.Value; result.CpuTemperature = Format(fallback); } }
   var gpu = sensors.Where(s => Kind(s) == "GPU" && s.Name.IndexOf("core", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
   if (gpu.Count > 0) { result.GpuTemperatureCelsius=gpu.Max(s=>s.Value); result.GpuTemperature = result.GpuTemperatureCelsius.Value.ToString("0.0", CultureInfo.InvariantCulture) + " °C · hottest core"; }
   else { var fallback = sensors.Where(s => Kind(s) == "GPU").OrderByDescending(s => s.Value).FirstOrDefault(); if (fallback != null) { result.GpuTemperatureCelsius=fallback.Value; result.GpuTemperature = Format(fallback); } }
   var board = sensors.Where(s => Kind(s) == "BOARD").ToList();
   if (board.Count > 0) result.Board = String.Join(Environment.NewLine, board.Select(s => Format(s) + "  [" + s.Parent + "]"));
   result.Details = String.Join(Environment.NewLine, sensors.Select(s => Kind(s) + " / " + Format(s) + "  [" + s.Parent + "]"));
   result.Status = sensors.Count > 0 ? "Sensor feed connected · " + sensors.Count + " temperature readings" : "Connected, but no valid temperature sensors were exposed.";
  }
  public static void ApplyGpuLoads(SystemSnapshot result, IEnumerable<SensorReading> source) {
   var loads = source.Where(s => s != null && s.SensorType == "Load" && Kind(s) == "GPU" && !Double.IsNaN(s.Value) && !Double.IsInfinity(s.Value) && s.Value >= 0 && s.Value <= 100).ToList();
   var selected = new List<SensorReading>();
   foreach (var device in loads.GroupBy(s => s.Parent)) {
    var core = device.Where(s => String.Equals(s.Name, "GPU Core", StringComparison.OrdinalIgnoreCase) || String.Equals(s.Name, "GPU Total", StringComparison.OrdinalIgnoreCase)).ToList();
    var candidates = core.Count > 0 ? core : device.Where(s => String.Equals(s.Name, "D3D 3D", StringComparison.OrdinalIgnoreCase) || String.Equals(s.Name, "GPU 3D", StringComparison.OrdinalIgnoreCase)).ToList();
    if (candidates.Count > 0) selected.Add(candidates.OrderByDescending(s => s.Value).First());
   }
   var busiest = selected.OrderByDescending(s => s.Value).FirstOrDefault();
   result.GpuUsage = busiest == null ? (double?)null : busiest.Value;
   result.GpuUsageDetail = busiest == null ? "GPU utilization unavailable" : busiest.Name + ": " + busiest.Value.ToString("0.0", CultureInfo.InvariantCulture) + "% [" + busiest.Parent + "] · busiest adapter";
   if (loads.Count > 0) result.Details += Environment.NewLine + Environment.NewLine + "GPU LOAD SENSORS / %" + Environment.NewLine + String.Join(Environment.NewLine, loads.Select(s => s.Name + ": " + s.Value.ToString("0.0", CultureInfo.InvariantCulture) + "% [" + s.Parent + "]"));
  }
  public static void ApplyFans(SystemSnapshot result, IEnumerable<SensorReading> source) {
   result.Cooling = "No fan or pump RPM readings available.";
   result.CoolingDetail = "Only RPM sensors exposed by the hardware monitor can be shown.";
   result.Fans = source.Where(s => s != null && s.SensorType == "Fan" && !Double.IsNaN(s.Value) && !Double.IsInfinity(s.Value) && s.Value >= 0 && s.Value <= 50000).OrderBy(s => s.Parent).ThenBy(s => s.Name).ToList();
   var cooling = result.Fans.Where(s => Kind(s) != "GPU" && s.Value > 0).ToList();
   if(cooling.Count==0 && result.Fans.Any(s=>Kind(s)!="GPU")){result.Cooling="No active fan or pump speeds to display.";result.CoolingDetail="Zero RPM channels are hidden until their speed increases.";}
   if (cooling.Count > 0) {
    result.Cooling = String.Join(Environment.NewLine, cooling.Select(s => s.Name + ": " + s.Value.ToString("0", CultureInfo.InvariantCulture) + " RPM"));
    result.CoolingDetail = String.Join(Environment.NewLine, cooling.Select(s => s.Name + " [" + s.Identifier + "]")) + Environment.NewLine + "Generic fan numbers do not identify AIO wiring. Zero means a reported 0 RPM, not a confirmed disconnected fan.";
   }
   if (result.Fans.Any(s=>s.Value>0)) result.Details += Environment.NewLine + Environment.NewLine + "FAN / PUMP SPEEDS · RPM" + Environment.NewLine + String.Join(Environment.NewLine, result.Fans.Where(s=>s.Value>0).Select(s => s.Name + ": " + s.Value.ToString("0", CultureInfo.InvariantCulture) + " RPM [" + s.Identifier + "]"));
  }
  static bool ParseRpm(string text, out double rpm) {
   text = (text ?? "").Trim(); text = Regex.Replace(text, @"\s*RPM\s*$", "", RegexOptions.IgnoreCase).Trim();
   return Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out rpm) && !Double.IsNaN(rpm) && !Double.IsInfinity(rpm) && rpm >= 0 && rpm <= 50000;
  }
  static string Field(IDictionary<string, object> node, string key) { object value; return node.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : ""; }
  static bool ParseTemperatureValue(string text, bool allowUnitlessCelsius, out double temperature) {
   text = (text ?? "").Trim().Replace(',', '.'); temperature = 0;
   if (allowUnitlessCelsius && Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out temperature)) return true;
   var match = Regex.Match(text, @"^\s*([-+]?\d+(?:\.\d+)?)\s*°?\s*([CF])\s*$", RegexOptions.IgnoreCase);
   if (!match.Success || !Double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out temperature)) return false;
   if (match.Groups[2].Value.Equals("F", StringComparison.OrdinalIgnoreCase)) temperature = (temperature - 32) * 5 / 9;
   return true;
  }
  public static List<SensorReading> ParseWebFeed(string json) {
   return ParseWebSensors(json, false);
  }
  public static List<SensorReading> ParseWebSensors(string json, bool includeLoads) {
   var serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024, RecursionLimit = 128 };
   var readings = new List<SensorReading>(); WalkWebNode(serializer.DeserializeObject(json), readings, includeLoads); return readings;
  }
  static void WalkWebNode(object value, List<SensorReading> readings, bool includeLoads) {
   var node = value as IDictionary<string, object>; if (node == null) return;
   string id = Field(node, "SensorId"); string type = Field(node, "Type");
   if (type.Equals("Temperature", StringComparison.OrdinalIgnoreCase) || id.IndexOf("/temperature/", StringComparison.OrdinalIgnoreCase) >= 0) {
    double temperature; object raw; bool hasRaw = node.TryGetValue("RawValue", out raw) && raw != null;
    string formatted = Field(node, "Value");
    if ((hasRaw && ParseTemperatureValue(Convert.ToString(raw, CultureInfo.InvariantCulture), true, out temperature)) || ParseTemperatureValue(formatted, false, out temperature)) {
     int split = id.IndexOf("/temperature/", StringComparison.OrdinalIgnoreCase);
     readings.Add(new SensorReading { Name = Field(node, "Text"), Identifier = id, Parent = split >= 0 ? id.Substring(0, split) : id, Value = temperature });
    }
   }
   else if (includeLoads && (type.Equals("Load", StringComparison.OrdinalIgnoreCase) || id.IndexOf("/load/", StringComparison.OrdinalIgnoreCase) >= 0)) {
    double load; string raw = Field(node, "RawValue").Trim().TrimEnd('%').Trim().Replace(',', '.');
    if (!Double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out load)) raw = Field(node, "Value").Trim().TrimEnd('%').Trim().Replace(',', '.');
    if (Double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out load) && load >= 0 && load <= 100) {
     int split = id.IndexOf("/load/", StringComparison.OrdinalIgnoreCase);
     readings.Add(new SensorReading { Name = Field(node, "Text"), Identifier = id, Parent = split >= 0 ? id.Substring(0, split) : id, Value = load, SensorType = "Load" });
    }
   }
   else if (includeLoads && (type.Equals("Fan", StringComparison.OrdinalIgnoreCase) || id.IndexOf("/fan/", StringComparison.OrdinalIgnoreCase) >= 0)) {
    double rpm;
    if (ParseRpm(Field(node, "RawValue"), out rpm) || ParseRpm(Field(node, "Value"), out rpm)) {
     int split = id.IndexOf("/fan/", StringComparison.OrdinalIgnoreCase);
     readings.Add(new SensorReading { Name = Field(node, "Text"), Identifier = id, Parent = split >= 0 ? id.Substring(0, split) : id, Value = rpm, SensorType = "Fan" });
    }
   }
   object children; if (node.TryGetValue("Children", out children)) {
    var list = children as object[]; if (list != null) foreach (object child in list) WalkWebNode(child, readings, includeLoads);
   }
  }
  static List<SensorReading> ReadWebFeed() {
   var request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:8085/data.json");
   request.Proxy = null; request.AllowAutoRedirect = false; request.Timeout = 1800; request.ReadWriteTimeout = 1800;
   request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
   using (var response = (HttpWebResponse)request.GetResponse()) {
    if (response.StatusCode != HttpStatusCode.OK) throw new IOException("Local feed returned HTTP " + (int)response.StatusCode);
    using (var stream = response.GetResponseStream()) using (var reader = new StreamReader(stream)) {
     var buffer = new char[4096]; var text = new System.Text.StringBuilder(); int count;
     while ((count = reader.Read(buffer, 0, buffer.Length)) > 0) { if (text.Length + count > 4 * 1024 * 1024) throw new IOException("Sensor feed is too large."); text.Append(buffer, 0, count); }
     return ParseWebSensors(text.ToString(), true);
    }
   }
  }
  public SystemSnapshot Read() {
   var result = new SystemSnapshot();
   try {
    FileTime idle, kernel, user;
    if (GetSystemTimes(out idle, out kernel, out user)) {
     ulong total = kernel.Value + user.Value;
     if (sampled && total > previousTotal && idle.Value >= previousIdle) result.CpuUsage = Math.Max(0, Math.Min(100, 100.0 * (1.0 - (double)(idle.Value - previousIdle) / (total - previousTotal))));
     previousIdle = idle.Value; previousTotal = total; sampled = true;
    }
    var memory = new MemoryStatus();
    if (GlobalMemoryStatusEx(memory) && memory.TotalPhysical > 0) {
     result.RamUsage = 100.0 * (memory.TotalPhysical - memory.AvailablePhysical) / memory.TotalPhysical;
     result.Memory = ((memory.TotalPhysical - memory.AvailablePhysical) / 1073741824.0).ToString("0.0") + " / " + (memory.TotalPhysical / 1073741824.0).ToString("0.0") + " GB";
    }
   } catch { }
   string webError = "", wmiError = "";
   try {
    var web = ReadWebFeed();
    ApplyTemperatures(result, web); ApplyGpuLoads(result, web); ApplyFans(result, web);
    if (web.Any(s => s.SensorType == "Temperature" && ValidTemperature(s.Value)) || result.GpuUsage.HasValue || result.Fans.Count > 0) { result.Status += " · local web feed"; result.ReadAt = DateTime.UtcNow; return result; }
    webError = "Local web feed connected but exposed no valid temperatures.";
   } catch (Exception ex) { webError = "Local web feed: " + ex.Message; }
   try {
    var readings = new List<SensorReading>();
    var options = new EnumerationOptions { Timeout = TimeSpan.FromSeconds(3), ReturnImmediately = false };
    using (var search = new ManagementObjectSearcher(new ManagementScope(@"\\.\root\LibreHardwareMonitor"), new ObjectQuery("SELECT Name, Parent, Identifier, Value, SensorType FROM Sensor WHERE SensorType = 'Temperature' OR SensorType = 'Load' OR SensorType = 'Fan'"), options))
    using (var found = search.Get()) foreach (ManagementObject item in found) using (item) {
     if (item["Value"] != null) readings.Add(new SensorReading { Name = Convert.ToString(item["Name"]), Parent = Convert.ToString(item["Parent"]), Identifier = Convert.ToString(item["Identifier"]), Value = Convert.ToDouble(item["Value"], CultureInfo.InvariantCulture), SensorType = Convert.ToString(item["SensorType"]) });
    }
    ApplyTemperatures(result, readings); ApplyGpuLoads(result, readings); ApplyFans(result, readings);
    if (readings.Any(s => s.SensorType == "Temperature" && ValidTemperature(s.Value)) || result.GpuUsage.HasValue || result.Fans.Count > 0) { result.Status += " · WMI feed"; result.ReadAt = DateTime.UtcNow; return result; }
    wmiError = "WMI connected but exposed no valid temperatures.";
   } catch (UnauthorizedAccessException) { wmiError = "WMI access denied."; }
   catch (Exception ex) { wmiError = "WMI: " + ex.Message; }
   result.Status = "No temperature feed. In Libre Hardware Monitor, enable Options > Remote Web Server > Run on port 8085. See SENSOR SETUP.";
   result.Details = webError + Environment.NewLine + wmiError;
   result.ReadAt = DateTime.UtcNow; return result;
  }
 }
}
