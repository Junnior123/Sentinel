namespace Sentinel.Core;

public static class LegacySettings {
 public static string ResolveService(string directory) {
  var current=Path.Combine(directory,"Sentinel.service.json");
  return File.Exists(current)?current:Path.Combine(directory,"Watchblock.service.json");
 }
 public static string HardwareBaseline(string localData) {
  var current=Path.Combine(localData,"Sentinel","hardware-baseline.json");
  var legacy=Path.Combine(localData,"Watchblock","hardware-baseline.json");
  if(File.Exists(current)||!File.Exists(legacy))return current;
  try {
   if(new FileInfo(legacy).Length>65536)return legacy;
   Directory.CreateDirectory(Path.GetDirectoryName(current)!);
   // Copy through a temporary file so an interrupted upgrade cannot leave a partial baseline.
   var temporary=current+"."+Guid.NewGuid().ToString("N")+".tmp";
   try {File.Copy(legacy,temporary,false);File.Move(temporary,current,false);}
   finally {if(File.Exists(temporary))File.Delete(temporary);}
   return current;
  }catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException){return File.Exists(current)?current:legacy;}
 }
}
