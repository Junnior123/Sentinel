using System.Diagnostics;using System.Text.Json;using Watchblock.Core;
try {
 var root=Path.GetFullPath(args[0]);var name=args.Length>1?args[1]:"current";
 if(name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ArgumentException("Invalid output label");
 var rules=Scanner.LoadRules(Path.Combine(root,"rules/catalog.json"));
 if(name=="profile"){
  var fixture=Path.Combine(root,"artifacts/profile-benchmark");var payload=new byte[65536];new Random(17).NextBytes(payload);
  foreach(var folder in new[]{"Program Files (x86)/Steam/steamapps/common/Example","Windows/System32/DriverStore/Example","Users/Example/AppData/Local/Temp","Program Files/Epic Games/Example","Users/Example/.minecraft/mods"}){
   var target=Path.Combine(fixture,folder);Directory.CreateDirectory(target);int count=folder.EndsWith("mods")?25:250;for(int n=0;n<count;n++)File.WriteAllBytes(Path.Combine(target,$"fixture-{n}.bin"),payload);
  }
  var comparison=new List<object>();
  foreach(bool enabled in new[]{false,true})for(int run=0;run<4;run++){
   var watch=Stopwatch.StartNew();var measured=await new Scanner(rules).ScanAsync([fixture],new(AllFiles:true,ExecutionTraces:false),null,CancellationToken.None,enabled?ScanPreferences.Recommended(fixture):null);watch.Stop();
   if(run>0)comparison.Add(new{profile=enabled,milliseconds=watch.Elapsed.TotalMilliseconds,files=measured.Summary.Files,excluded=measured.Chunks.SelectMany(c=>c.Coverage).Count(c=>c.Reason.StartsWith("권장 검사 제외"))});
  }
  var profileResult=JsonSerializer.Serialize(new{dataset="Synthetic 1025 x 64KiB files; 1000 in excluded installation/temp paths, 25 retained. Warm cache; not real full-drive performance.",runs=comparison},new JsonSerializerOptions{WriteIndented=true});File.WriteAllText(Path.Combine(root,"artifacts/benchmark-profile.json"),profileResult);Console.WriteLine(profileResult);return;
 }
 var roots=new[]{Path.Combine(root,"artifacts/real-benign"),Path.Combine(root,"samples/private")};
 if(roots.Any(p=>!Directory.Exists(p)))throw new IOException("Run regression fixture preparation before benchmarking.");
 var runs=new List<object>();
 for(int i=0;i<4;i++){
  var before=GC.GetTotalAllocatedBytes(true);var timer=Stopwatch.StartNew();
  var report=await new Scanner(rules).ScanAsync(roots,new(AllFiles:true,ExecutionTraces:false),null,CancellationToken.None);timer.Stop();
  var allocated=GC.GetTotalAllocatedBytes(true)-before;
  if(i>0)runs.Add(new{milliseconds=timer.Elapsed.TotalMilliseconds,allocatedBytes=allocated,files=report.Summary.Files,known=report.Summary.Known,review=report.Summary.Review});
 }
 var result=JsonSerializer.Serialize(new{dataset="100 .NET DLL + 11 verified samples, warm filesystem cache, file phase only",runs},new JsonSerializerOptions{WriteIndented=true});
 File.WriteAllText(Path.Combine(root,"artifacts/benchmark-"+name+".json"),result);Console.WriteLine(result);
}catch(Exception ex){Console.Error.WriteLine(ex.Message);Environment.ExitCode=1;}
