namespace Watchblock.Core;

// Local choices only. Every exclusion is also recorded in report coverage.
public sealed record ScanPreferences(string[] ExcludedPaths,int Workers=2,string? ProfileRoot=null) {
 private readonly HashSet<string> normalized=ExcludedPaths.Select(p=>Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar)).ToHashSet(StringComparer.OrdinalIgnoreCase);
 public static ScanPreferences Recommended(string systemRoot) => new([
  Path.Combine(systemRoot,"$Recycle.Bin"),
  Path.Combine(systemRoot,"Windows","WinSxS"),
  Path.Combine(systemRoot,"Windows","servicing"),
  Path.Combine(systemRoot,"Windows","SoftwareDistribution","Download"),
  Path.Combine(systemRoot,"Windows","Temp"),Path.Combine(systemRoot,"Windows","System32","DriverStore"),
  Path.Combine(systemRoot,"Windows","System32","drivers"),
  Path.Combine(systemRoot,"Program Files (x86)","Steam","steamapps"),
  Path.Combine(systemRoot,"Program Files","Steam","steamapps"),Path.Combine(systemRoot,"SteamLibrary","steamapps"),
  Path.Combine(systemRoot,"Program Files","Epic Games"),Path.Combine(systemRoot,"Riot Games"),
  Path.Combine(systemRoot,"NVIDIA"),Path.Combine(systemRoot,"AMD"),
  Path.GetTempPath(),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Temp"),
  Path.Combine(systemRoot,"pagefile.sys"),Path.Combine(systemRoot,"swapfile.sys"),Path.Combine(systemRoot,"hiberfil.sys")],ProfileRoot:systemRoot);
 // Whole, explicitly listed paths only; a mod merely named steam/temp/driver is never exempt.
 public bool Excludes(string path) {
  var full=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
  if(ProfileRoot!=null){
   var relative=Path.GetRelativePath(Path.GetFullPath(ProfileRoot),full).Split(Path.DirectorySeparatorChar);
   if(relative.Length>=5&&relative[0].Equals("Users",StringComparison.OrdinalIgnoreCase)&&relative[2].Equals("AppData",StringComparison.OrdinalIgnoreCase)&&relative[3].Equals("Local",StringComparison.OrdinalIgnoreCase)&&relative[4].Equals("Temp",StringComparison.OrdinalIgnoreCase))return true;
  }
  return normalized.Any(p=>full.Equals(p,StringComparison.OrdinalIgnoreCase)||full.StartsWith(p+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));
 }
}

public static class ScanTiming {
 public static ScanProgress Measure(int completed,int total,TimeSpan elapsed) {
  if(total<0||completed<0||completed>total)throw new ArgumentOutOfRangeException(nameof(completed));
  double? remaining=completed==total?0:completed>=3&&elapsed.TotalSeconds>=2
   ?Math.Max(0,elapsed.TotalSeconds/completed*(total-completed)):null;
  return new("파일 검사",completed,total,total==0?100:100d*completed/total,remaining);
 }
}
