using System.IO.Compression;using System.Security.Cryptography;using System.Text;using System.Text.Json;using Watchblock.Core;using Watchblock.Windows;
var root=Path.GetFullPath(args.FirstOrDefault()??".");var dir=Path.Combine(root,"artifacts","fixtures");Directory.CreateDirectory(dir);int assertions=0;
void Check(bool value,string reason){if(!value)throw new Exception(reason);assertions++;Console.WriteLine("PASS "+reason);}
void Jar(string name,Dictionary<string,string> entries){using var fs=File.Create(Path.Combine(dir,name));using var zip=new ZipArchive(fs,ZipArchiveMode.Create);foreach(var (path,text) in entries){using var writer=new StreamWriter(zip.CreateEntry(path).Open());writer.Write(text);}}
for(int i=0;i<100;i++)Jar($"clean-{i}.jar",new(){["fabric.mod.json"]=JsonSerializer.Serialize(new{id="clean"+i,version="1.0",name="Normal fixture"}),["assets/example/lang/en_us.json"]="{}"});
Jar("forge.jar",new(){["META-INF/mods.toml"]="[[mods]]\nmodId=\"clean_forge\"\n"});Jar("neoforge.jar",new(){["META-INF/neoforge.mods.toml"]="[[mods]]\nmodId=\"clean_neoforge\"\n"});
File.WriteAllText(Path.Combine(dir,"repeat.ahk"),"Loop 5 {\n Click\n}");File.WriteAllText(Path.Combine(dir,"accessibility.ahk"),"F1::Click");File.WriteAllBytes(Path.Combine(dir,"broken.jar"),[0x50,0x4b,3,4,0]);Jar("unsafe.zip",new(){["../escape.exe"]="untrusted"});Jar("nested.jar",new(){["libs/inside.jar"]="test"});
var rules=Scanner.LoadRules(Path.Combine(root,"rules/catalog.json"));var scanner=new Scanner(rules);var report=await scanner.ScanAsync([dir],new(true,false,false,["clean_forge"]),null,CancellationToken.None);
var files=report.Chunks.SelectMany(c=>c.Files).ToArray();var findings=report.Chunks.SelectMany(c=>c.Findings).ToArray();
Check(files.Count(f=>f.Path.Contains("clean-"))==100,"100 synthetic benign JARs read");Check(!findings.Any(f=>files.Any(x=>x.Id==f.FileId&&x.Path.Contains("clean-"))),"zero findings on 100 synthetic benign JARs");Check(files.Any(f=>f.Format=="forge-jar")&&files.Any(f=>f.Format=="neoforge-jar"),"three loader metadata formats");Check(findings.Any(f=>f.Category=="policy"),"server policy separate");Check(findings.Any(f=>f.RuleId=="ahk-repeat-click-review"&&f.Category=="review"),"macro classified review only");Check(!findings.Any(f=>files.Any(x=>x.Id==f.FileId&&x.Path.EndsWith("accessibility.ahk"))),"single accessibility click not detected");Check(files.Single(f=>f.Path.EndsWith("broken.jar")).Status=="error","corrupt archive explicit error");Check(files.Single(f=>f.Path.EndsWith("unsafe.zip")).Status=="partial"&&!File.Exists(Path.Combine(root,"escape.exe")),"archive traversal not extracted");Check(report.Summary.Completion=="partial","partial coverage not clean");Check(!JsonSerializer.Serialize(report,Json.Options).Contains(Environment.UserName),"personal path masked");
using(var ct=new CancellationTokenSource()){ct.Cancel();var canceled=await new Scanner(rules).ScanAsync([dir],new(),null,ct.Token);Check(canceled.Summary.Completion=="cancelled","cancellation explicit");}
byte[] pf=new byte[256];BitConverter.GetBytes(30).CopyTo(pf,0);Encoding.ASCII.GetBytes("SCCA").CopyTo(pf,4);Encoding.Unicode.GetBytes("EXAMPLE.EXE").CopyTo(pf,16);BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(pf,128);var parsed=Traces.ParsePrefetch(pf);Check(parsed.Executable=="EXAMPLE.EXE"&&parsed.Times.Length==1,"Prefetch v30 timestamp parsing");try{Traces.ParsePrefetch([1,2,3]);throw new Exception("not rejected");}catch(InvalidDataException){Check(true,"corrupt Prefetch rejected");}
var samples=Path.Combine(root,"samples/private");if(Directory.Exists(samples)){var real=new Scanner(rules);var realReport=await real.ScanAsync([samples],new(true,false,false),null,CancellationToken.None);foreach(var f in realReport.Chunks.SelectMany(c=>c.Files)){Check(realReport.Chunks.SelectMany(c=>c.Findings).Any(x=>x.FileId==f.Id&&x.Category=="known"),"primary-source cheat/autoclicker/macro classified known: "+f.Path);var local=real.LocalPaths.Single(x=>x.Value==f.Id).Key;Check(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(local)))==f.Sha256,"source preserved: "+f.Path);}Check(realReport.Summary.Review==0,"verified samples do not duplicate generic review findings");File.WriteAllText(Path.Combine(root,"artifacts/real-sample-report.json"),JsonSerializer.Serialize(realReport,Json.Options));}
File.WriteAllText(Path.Combine(root,"artifacts/fixture-report.json"),JsonSerializer.Serialize(report,Json.Options));Console.WriteLine($"ASSERTIONS {assertions}");
var cleanDir=Path.Combine(root,"artifacts/real-benign");Directory.CreateDirectory(cleanDir);var frameworkDir=System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();foreach(var source in Directory.EnumerateFiles(frameworkDir,"*.dll").Take(100))File.Copy(source,Path.Combine(cleanDir,Path.GetFileName(source)),true);
var cleanReport=await new Scanner(rules).ScanAsync([cleanDir],new(true,false,false),null,CancellationToken.None);Check(cleanReport.Summary.Files>=100,"at least 100 actual .NET runtime DLLs inspected");Check(cleanReport.Summary.Known==0&&cleanReport.Summary.Review==0,"zero detections on actual .NET runtime DLLs");File.WriteAllText(Path.Combine(root,"artifacts/real-benign-report.json"),JsonSerializer.Serialize(cleanReport,Json.Options));
var trace=Traces.Collect(new(){[Environment.ProcessPath!]=Guid.NewGuid().ToString()},CancellationToken.None);Check(trace.Coverage.Count>=3,"real Windows process/BAM/Prefetch collectors return coverage");Check(!JsonSerializer.Serialize(trace,Json.Options).Contains(Environment.UserName),"Windows trace paths masked");File.WriteAllText(Path.Combine(root,"artifacts/trace-report.json"),JsonSerializer.Serialize(trace,Json.Options));Console.WriteLine($"TOTAL ASSERTIONS {assertions}");
var edgeDir=Path.Combine(root,"artifacts/edge-cases");Directory.CreateDirectory(edgeDir);File.WriteAllBytes(Path.Combine(edgeDir,"invalid.exe"),[77,90,0,0]);
using(var large=File.Create(Path.Combine(edgeDir,"large.jar")))large.SetLength(Scanner.MaxFile+1);
using(var fs=File.Create(Path.Combine(edgeDir,"bomb.zip")))using(var z=new ZipArchive(fs,ZipArchiveMode.Create))using(var es=z.CreateEntry("large.class",CompressionLevel.SmallestSize).Open())es.Write(new byte[17*1024*1024]);
using(var fs=File.Create(Path.Combine(edgeDir,"wrong-metadata.jar")))using(var z=new ZipArchive(fs,ZipArchiveMode.Create))using(var w=new StreamWriter(z.CreateEntry("fabric.mod.json").Open()))w.Write("{\"id\":123}");
if(File.Exists(Path.Combine(samples,"meteor-client-0.5.8.jar"))){File.Copy(Path.Combine(samples,"meteor-client-0.5.8.jar"),Path.Combine(edgeDir,"renamed.bin"),true);File.Copy(Path.Combine(samples,"meteor-client-0.5.8.jar"),Path.Combine(edgeDir,"repacked.jar"),true);using(var z=ZipFile.Open(Path.Combine(edgeDir,"repacked.jar"),ZipArchiveMode.Update)){if(z.GetEntry("fixture-marker.txt")==null){using var w=new StreamWriter(z.CreateEntry("fixture-marker.txt").Open());w.Write("static regression fixture");}}}
if(File.Exists(Path.Combine(samples,"sjrwatson-autoclicker.exe")))File.Copy(Path.Combine(samples,"sjrwatson-autoclicker.exe"),Path.Combine(edgeDir,"renamed-native.bin"),true);
var edges=await new Scanner(rules).ScanAsync([edgeDir,edgeDir],new(true,true,false),null,CancellationToken.None);var ef=edges.Chunks.SelectMany(c=>c.Files).ToArray();var eh=edges.Chunks.SelectMany(c=>c.Findings).ToArray();Check(ef.Select(f=>f.Path).Distinct().Count()==ef.Length,"duplicate roots do not duplicate files");Check(ef.Single(f=>f.Path.EndsWith("large.jar")).Status=="skipped","large file explicitly skipped");Check(ef.Single(f=>f.Path.EndsWith("bomb.zip")).Status=="partial","decompression bomb bounded");Check(ef.Single(f=>f.Path.EndsWith("invalid.exe")).Status=="error","invalid PE rejected");Check(ef.Single(f=>f.Path.EndsWith("wrong-metadata.jar")).Status=="partial","metadata type error does not stop other files");
foreach(var f in ef.Where(f=>f.Path.EndsWith(".bin")))Check(eh.Any(h=>h.FileId==f.Id),"renamed extension detected by file content: "+f.Path);
if(ef.Any(f=>f.Path.EndsWith("repacked.jar")))Check(eh.Any(h=>h.FileId==ef.Single(f=>f.Path.EndsWith("repacked.jar")).Id&&h.RuleId=="meteor-structure-review"&&h.Category=="review"),"repacked JAR structural match requires review");
try{ClassFile.Constants([0,1,2]);throw new Exception("class not rejected");}catch(InvalidDataException){Check(true,"malformed Java class rejected");}
File.WriteAllText(Path.Combine(root,"artifacts/edge-report.json"),JsonSerializer.Serialize(edges,Json.Options));Console.WriteLine($"FINAL ASSERTIONS {assertions}");
var invitation=Invitation.Parse("https://operator.example/#scan=abcdef123456");
Check(invitation.Origin=="https://operator.example"&&invitation.Code=="ABCDEF123456","invitation link resolves operator and code");
Check(Invitation.Parse(JsonSerializer.Serialize(invitation,Json.Options))==invitation,"invitation file roundtrip");
Check(Invitation.Parse("abcdef123456","https://operator.example")==invitation,"operator-configured app accepts code only");
foreach(var invalid in new[]{"http://operator.example/#scan=abcdef123456","https://user:pass@operator.example/#scan=abcdef123456","https://operator.example/path#scan=abcdef123456","https://operator.example/#scan=bad","{\"schemaVersion\":2,\"origin\":\"https://operator.example\",\"code\":\"abcdef123456\"}"}) {
 try{Invitation.Parse(invalid);throw new Exception("Invalid invitation accepted");}catch(ArgumentException){Check(true,"invalid invitation rejected");}
}
Console.WriteLine($"INVITATION REGRESSION TOTAL {assertions}");
var broad=Path.Combine(root,"artifacts/broad-fixtures");Directory.CreateDirectory(broad);
File.WriteAllText(Path.Combine(broad,"personal.txt"),"Loop Click should never be treated as a script in arbitrary documents");
File.WriteAllBytes(Path.Combine(broad,"image.jpg"),[255,216,255,224,1,2,3]);
var broadReport=await new Scanner(rules).ScanAsync([broad],new(AllFiles:true,ExecutionTraces:false),null,CancellationToken.None);
Check(broadReport.Summary.Files==2,"all-file mode inventories arbitrary extensions");
Check(broadReport.Chunks.SelectMany(c=>c.Files).All(f=>f.Sha256!=null&&f.Format=="hash-only"),"unknown formats hashed without text-pattern interpretation");
Check(broadReport.Summary.Known==0&&broadReport.Summary.Review==0,"arbitrary personal text does not trigger macro rule");
var negative=Path.Combine(root,"artifacts/negative");if(Directory.Exists(negative)){
 var nr=await new Scanner(rules).ScanAsync([negative],new(ExecutionTraces:false),null,CancellationToken.None);
 Check(nr.Summary.Known==0&&nr.Summary.Review==0,"official Anti Xray defensive pack is not classified as cheat");
}
var pack=Path.Combine(samples,"spectator-xray-1.2.1.zip");if(File.Exists(pack)){
 var unpacked=Path.Combine(root,"artifacts/unpacked-xray");ZipFile.ExtractToDirectory(pack,unpacked,true);
 var ur=await new Scanner(rules).ScanAsync([unpacked],new(ExecutionTraces:false),null,CancellationToken.None);
 Check(ur.Chunks.SelectMany(c=>c.Findings).Any(f=>f.RuleId=="spectator-xray-content"&&f.Category=="known"),"unpacked Xray pack matched by three internal hashes");
 var packDir=Path.Combine(root,"artifacts/repacked-xray");Directory.CreateDirectory(packDir);File.Copy(pack,Path.Combine(packDir,"renamed.zip"),true);
 using(var zip=ZipFile.Open(Path.Combine(packDir,"renamed.zip"),ZipArchiveMode.Update)){using var w=new StreamWriter(zip.CreateEntry("test-marker").Open());w.Write("fixture");}
 var pr=await new Scanner(rules).ScanAsync([packDir],new(ExecutionTraces:false),null,CancellationToken.None);
 Check(pr.Chunks.SelectMany(c=>c.Findings).Any(f=>f.RuleId=="spectator-xray-content"&&f.Category=="known"),"repacked Xray preserves known internal content match");
 var ordered=UploadOrder.Prioritize(pr);Check(ordered.First().Findings.Count>0,"findings upload before large file inventories");
 Check(ordered.SelectMany(c=>c.Files).Count()==pr.Summary.Files,"prioritized upload does not duplicate files");
}
var previous=new HardwareBaseline(1,"fixture",new(){["hardware"]="aaa"});
Check(HardwareChecks.Compare(previous,previous).Length==0,"unchanged hardware baseline does not create suspicion");
Check(HardwareChecks.Compare(previous,new(1,"fixture",new(){["hardware"]="bbb"})).SequenceEqual(new[]{"hardware"}),"changed hardware value is separated for review");
var hardware=HardwareChecks.Collect(Path.Combine(root,"artifacts/test-hardware-baseline.json"),CancellationToken.None);
Check(hardware.Coverage.Count>0,"actual Windows hardware and boot checks report coverage");
Check(!JsonSerializer.Serialize(hardware,Json.Options).Contains("digests",StringComparison.OrdinalIgnoreCase),"hardware raw hashes are not included in report");
File.WriteAllText(Path.Combine(root,"artifacts/hardware-report.json"),JsonSerializer.Serialize(hardware,Json.Options));
var signals=new List<ScanProgress>();
var timed=await new Scanner(rules).ScanAsync([dir],new(ExecutionTraces:false),new InlineProgress(signals.Add),CancellationToken.None);
Check(signals[0].Total==null&&signals[0].Percent==null,"inventory does not invent progress percentage");
Check(signals.Last().Percent==100&&signals.Last().Count==timed.Summary.Files,"file percentage finishes at actual file count");
Check(signals.Where(p=>p.Percent!=null).Select(p=>p.Percent!.Value).SequenceEqual(signals.Where(p=>p.Percent!=null).Select(p=>p.Percent!.Value).Order()),"file percentage is monotonic");
Check(ScanTiming.Measure(2,10,TimeSpan.FromSeconds(1)).RemainingSeconds==null,"ETA waits for warm-up");
Check(ScanTiming.Measure(5,10,TimeSpan.FromSeconds(10)).RemainingSeconds==10,"ETA based on measured processing rate");
Check(ScanTiming.Measure(0,0,TimeSpan.Zero).Percent==100,"empty inventory does not divide by zero");
var pref=ScanPreferences.Recommended(dir);
Check(pref.Excludes(Path.Combine(dir,"Windows/WinSxS"))&&!pref.Excludes(Path.Combine(dir,"Windows/WinSxS-copy")),"exclusions use exact paths");
Check(pref.Excludes(Path.Combine(dir,"Users/test/AppData/Local/Temp")),"profile temp folders are excluded");
var profileDir=Path.Combine(root,"artifacts/profile-fixture");Directory.CreateDirectory(Path.Combine(profileDir,"Windows/WinSxS"));Directory.CreateDirectory(Path.Combine(profileDir,"Users/another-person/AppData/Local/Temp"));
File.WriteAllText(Path.Combine(profileDir,"Windows/WinSxS/skip.txt"),"fixture");File.WriteAllText(Path.Combine(profileDir,"Users/another-person/AppData/Local/Temp/include.txt"),"fixture");
var profile=await new Scanner(rules).ScanAsync([profileDir],new(AllFiles:true),null,CancellationToken.None,ScanPreferences.Recommended(profileDir));
Check(profile.Summary.Files==0&&profile.Chunks.SelectMany(c=>c.Coverage).Any(c=>c.Reason.Contains("WinSxS")),"recommended profile records exclusions including Temp");
Check(!JsonSerializer.Serialize(profile,Json.Options).Contains("another-person"),"other profile names are masked");
using(var cancelledInventory=new CancellationTokenSource()){var cancelledReport=await new Scanner(rules).ScanAsync([dir],new(),new InlineProgress(p=>{if(p.Total==null)cancelledInventory.Cancel();}),cancelledInventory.Token);Check(cancelledReport.Summary.Completion=="cancelled"&&cancelledReport.Summary.Files==0,"inventory cancellation does not inspect files");}
if(File.Exists(Path.Combine(samples,"meteor-client-0.5.8.jar"))){
var disguised=Path.Combine(root,"artifacts/disguised-fixture");Directory.CreateDirectory(disguised);
File.Copy(Path.Combine(samples,"meteor-client-0.5.8.jar"),Path.Combine(disguised,"sodium.jar"),true);
File.Copy(Path.Combine(samples,"meteor-client-0.5.8.jar"),Path.Combine(disguised,"appleskin.jar"),true);
using(var zip=ZipFile.Open(Path.Combine(disguised,"appleskin.jar"),ZipArchiveMode.Update)){using var writer=new StreamWriter(zip.CreateEntry("benign-name.txt").Open());writer.Write("AppleSkin");}
var disguisedReport=await new Scanner(rules).ScanAsync([disguised],new(),null,CancellationToken.None);
Check(disguisedReport.Summary.Known>=1&&disguisedReport.Summary.Review>=1,"Sodium name and repacked AppleSkin name do not bypass content detection");
Check(disguisedReport.Chunks.SelectMany(c=>c.Findings).Any(f=>f.RuleId=="meteor-crystal-module"),"repacked sample retains verified CrystalAura structure detection");
Check(disguisedReport.Chunks.SelectMany(c=>c.Findings).Any(f=>f.RuleId=="meteor-bow-aim-module"),"repacked sample retains verified bow aim structure detection");
using(var zip=ZipFile.OpenRead(Path.Combine(samples,"meteor-client-0.5.8.jar"))){using var stream=zip.GetEntry("meteordevelopment/meteorclient/systems/modules/combat/CrystalAura.class")!.Open();using var memory=new MemoryStream();stream.CopyTo(memory);var bytes=memory.ToArray();Check(ClassFile.Constants(bytes).Length>0&&ClassFile.Constants(bytes,false)=="","optimized class validation skips unused text decoding");}
}
var sequential=await new Scanner(rules).ScanAsync([dir],new(AllFiles:true),null,CancellationToken.None,new([],1));
var concurrent=await new Scanner(rules).ScanAsync([dir],new(AllFiles:true),null,CancellationToken.None,new([],2));
string[] FileFacts(ScanReport r)=>r.Chunks.SelectMany(c=>c.Files).Select(f=>$"{f.Path}|{f.Size}|{f.Sha256}|{f.Status}|{f.Format}").Order().ToArray();
string[] FindingFacts(ScanReport r){var names=r.Chunks.SelectMany(c=>c.Files).ToDictionary(f=>f.Id,f=>f.Path);return r.Chunks.SelectMany(c=>c.Findings).Select(f=>$"{f.RuleId}|{f.Category}|{(f.FileId==null?"":names[f.FileId])}").Order().ToArray();}
Check(FileFacts(sequential).SequenceEqual(FileFacts(concurrent)),"parallel file scan preserves file hashes, status and inventory");
Check(FindingFacts(sequential).SequenceEqual(FindingFacts(concurrent)),"parallel file scan preserves all findings");
var peDir=Path.Combine(root,"artifacts/pe-pattern-fixture");Directory.CreateDirectory(peDir);File.Copy(Directory.GetFiles(cleanDir,"*.dll").First(),Path.Combine(peDir,"renamed.dat"),true);
var peRule=new Rule("pe-test","PE marker regression","review","https://learn.microsoft.com/",[],[],["MZ","PE\0\0"],[],"Synthetic test",["pe"]);
var peResult=await new Scanner(new(1,"test",[peRule])).ScanAsync([peDir],new(AllFiles:true),null,CancellationToken.None);
Check(peResult.Summary.Review==1,"PE string scan remains active when a rule requires it");
var hiddenDir=Path.Combine(root,"artifacts/hidden-fixture");Directory.CreateDirectory(hiddenDir);var hiddenFile=Path.Combine(hiddenDir,"hidden.ahk");if(File.Exists(hiddenFile))File.SetAttributes(hiddenFile,FileAttributes.Normal);File.WriteAllText(hiddenFile,"F1::Click");File.SetAttributes(hiddenFile,FileAttributes.Hidden|FileAttributes.System);
var hiddenReport=await new Scanner(rules).ScanAsync([hiddenDir],new(AllFiles:true),null,CancellationToken.None);
Check(hiddenReport.Summary.Files==1&&hiddenReport.Chunks.SelectMany(c=>c.Files).Single().Sha256!=null,"optimized enumeration retains hidden and system files");
var excludeProfile=ScanPreferences.Recommended(profileDir);
Check(excludeProfile.Excludes(Path.Combine(profileDir,"Program Files (x86)/Steam/steamapps/common/Game/data.bin")),"Steam library descendants excluded before reading");
Check(excludeProfile.Excludes(Path.Combine(profileDir,"Windows/System32/DriverStore/FileRepository/test.sys")),"driver repository excluded");
Check(!excludeProfile.Excludes(Path.Combine(profileDir,"Users/test/.minecraft/mods/steam.jar"))&&!excludeProfile.Excludes(Path.Combine(profileDir,"Users/test/Downloads/temp/aim.jar")),"arbitrary mod and download names cannot bypass content inspection");
var unfilteredProfile=await new Scanner(rules).ScanAsync([profileDir],new(AllFiles:true),null,CancellationToken.None);
Check(unfilteredProfile.Summary.Files==2,"disabling profile restores excluded file coverage");
byte[] RecycleFixture(string path,long version=2){var text=Encoding.Unicode.GetBytes(path+"\0");var buffer=new byte[version==1?544:28+text.Length];BitConverter.GetBytes(version).CopyTo(buffer,0);BitConverter.GetBytes(123L).CopyTo(buffer,8);BitConverter.GetBytes(DateTimeOffset.UtcNow.ToFileTime()).CopyTo(buffer,16);if(version==2)BitConverter.GetBytes((uint)(text.Length/2)).CopyTo(buffer,24);text.CopyTo(buffer,version==1?24:28);return buffer;}
var originalPath=@"C:\Users\private-person\Downloads\xray.jar";
Check(DeletedFiles.ParseRecycle(RecycleFixture(originalPath)).OriginalPath==originalPath,"Recycle Bin v2 original path parsed without executing retained content");
Check(DeletedFiles.ParseRecycle(RecycleFixture(originalPath,1)).Size==123,"Recycle Bin v1 size and path parsed");
Check(DeletedFiles.ScopeLabel(originalPath,[@"C:\Users"])=="선택폴더1/[사용자]/Downloads/xray.jar","deleted file original path masks other user profiles");
Check(DeletedFiles.ScopeLabel(originalPath,[@"C:\Users\private-person\Downloads-copy"])==null,"deleted file history stays inside selected roots");
bool malformed=false;try{DeletedFiles.ParseRecycle(RecycleFixture(originalPath)[..30]);}catch(InvalidDataException){malformed=true;}Check(malformed,"truncated recycle path rejected");
Check(!DeletedFiles.Relevant("private.docx")&&DeletedFiles.Relevant("renamed.jar"),"deletion collector omits personal document history");
var retained=await new Scanner(rules).ScanRetainedFileAsync(Path.Combine(dir,"repeat.ahk"),"휴지통/선택폴더1/renamed.ahk",new(),CancellationToken.None);
Check(retained.Findings.Count>0&&retained.Files.Single().Sha256!=null,"retained recycle contents use existing content detection even with renamed labels");
byte[] UsnFixture(string name,uint reason=0x200){var text=Encoding.Unicode.GetBytes(name);var bytes=new byte[68+text.Length];BitConverter.GetBytes(1000L).CopyTo(bytes,0);BitConverter.GetBytes((uint)(60+text.Length)).CopyTo(bytes,8);BitConverter.GetBytes((ushort)2).CopyTo(bytes,12);BitConverter.GetBytes(42UL).CopyTo(bytes,16);BitConverter.GetBytes(7UL).CopyTo(bytes,24);BitConverter.GetBytes(800L).CopyTo(bytes,32);BitConverter.GetBytes(DateTimeOffset.UtcNow.ToFileTime()).CopyTo(bytes,40);BitConverter.GetBytes(reason).CopyTo(bytes,48);BitConverter.GetBytes((ushort)text.Length).CopyTo(bytes,64);BitConverter.GetBytes((ushort)60).CopyTo(bytes,66);text.CopyTo(bytes,68);return bytes;}
var journalRecords=DeletionJournal.ParseBuffer(UsnFixture("$R1234.jar"),out var nextUsn,out var unsupportedUsn);
Check(nextUsn==1000&&unsupportedUsn==0&&journalRecords.Single().File==42&&journalRecords[0].Name=="$R1234.jar","USN V2 deletion record parsed with file and parent IDs");
var prior=journalRecords[0] with{Name="xray.jar",Usn=700,Reason=0x1000};
Check(DeletionJournal.OriginalName(journalRecords[0],new Dictionary<ulong,DeletionJournal.Entry>{{42,prior}}).Name=="xray.jar","recycle deletion linked to earlier original name by same file ID");
Check(DeletionJournal.OriginalName(journalRecords[0],new Dictionary<ulong,DeletionJournal.Entry>{{99,prior}}).Name.StartsWith("$R"),"unrelated file ID cannot establish original name");
Check(DeletionJournal.OriginalName(journalRecords[0],new Dictionary<ulong,DeletionJournal.Entry>{{42,prior with{Usn=900}}}).Name.StartsWith("$R"),"future rename cannot explain earlier deletion");
malformed=false;try{DeletionJournal.ParseBuffer(UsnFixture("xray.jar")[..65],out _,out _);}catch(InvalidDataException){malformed=true;}Check(malformed,"truncated journal buffer rejected");
var unsupportedRecord=UsnFixture("xray.jar");BitConverter.GetBytes((ushort)3).CopyTo(unsupportedRecord,12);Check(DeletionJournal.ParseBuffer(unsupportedRecord,out _,out var unsupportedCount).Count==0&&unsupportedCount==1,"unsupported journal versions never silently interpreted as V2");
using(var cancelledHistory=new CancellationTokenSource()){cancelledHistory.Cancel();var cancelled=DeletionJournal.Collect([@"C:\"],cancelledHistory.Token);Check(cancelled.Coverage.Any(c=>c.Status=="cancelled"),"deletion journal cancellation explicit before volume access");}
File.WriteAllText(Path.Combine(root,"artifacts/deletion-fixture.json"),JsonSerializer.Serialize(new ReportChunk([], [new("recycle-bin","선택폴더1/xray.jar",null,null,"none","휴지통 메타데이터, 내용 미확인")],[],[]),Json.Options));
var deletionHint=DeletedFiles.NameReview(new("usn","선택폴더1/xray.jar",null,null,"none","내용 없음"),rules);
Check(deletionHint?.Category=="review"&&deletionHint.FileId==null,"deleted Xray name remains review-only without content or hash");
Check(DeletedFiles.NameReview(new("usn","선택폴더1/normal.jar",null,null,"none","내용 없음"),rules)==null,"ordinary deleted mod name does not trigger a cheat finding");
Console.WriteLine($"FINAL REGRESSION ASSERTIONS {assertions}");
await ApiClientChecks.Run(Check);Console.WriteLine($"PRODUCTION ASSERTIONS {assertions}");
sealed class InlineProgress(Action<ScanProgress> callback):IProgress<ScanProgress>{public void Report(ScanProgress value)=>callback(value);}


