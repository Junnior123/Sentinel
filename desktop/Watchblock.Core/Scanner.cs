using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Watchblock.Core;
public sealed class Scanner(RulePack rules) {
 public const int MaxFiles=250000;
 public const long MaxFile=256L*1024*1024;
 private readonly bool needsJavaStrings=rules.Rules.Any(r=>r.AllStrings.Length>0&&(r.Formats==null||r.Formats.Length==0||r.Formats.Any(f=>f!="text"&&f!="pe")));
 private readonly bool needsPeStrings=rules.Rules.Any(r=>r.AllStrings.Length>0&&(r.Formats==null||r.Formats.Length==0||r.Formats.Contains("pe")));
 private readonly HashSet<string> requestedHashes=rules.Rules.Where(r=>r.EntrySha256!=null).SelectMany(r=>r.EntrySha256!.Keys).ToHashSet(StringComparer.Ordinal);
 private readonly HashSet<string> visitedFiles=new(StringComparer.OrdinalIgnoreCase);
 private readonly List<FileRecord> files=[];private readonly List<Finding> findings=[];private readonly List<Coverage> coverage=[];
 public readonly Dictionary<string,string> LocalPaths=new(StringComparer.OrdinalIgnoreCase);
 private static readonly EnumerationOptions Enumeration=new(){AttributesToSkip=0,IgnoreInaccessible=false,RecurseSubdirectories=false,ReturnSpecialDirectories=false};
 private static readonly HashSet<string> Extensions=new([".jar",".zip",".exe",".dll",".ahk",".lua",".json",".cfg",".xml",".mcmeta",".sys"],StringComparer.OrdinalIgnoreCase);
 public async Task<ReportChunk> ScanRetainedFileAsync(string path,string label,ScanScope scope,CancellationToken ct){
  if(files.Count>0||label.Length>1024)throw new InvalidOperationException("새 파일 검사기가 필요합니다.");
  if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("링크 파일은 검사하지 않습니다.");
  await InspectAsync(path,label,scope,ct);return new(files,[],findings,[]);
 }
 public static RulePack LoadRules(string path){var pack=JsonSerializer.Deserialize<RulePack>(File.ReadAllText(path),Json.Options)??throw new InvalidDataException("규칙 파일이 비어 있습니다.");if(pack.SchemaVersion!=1||pack.Rules.Length>10000||pack.Rules.Any(r=>string.IsNullOrEmpty(r.Validation)||!Uri.TryCreate(r.Source,UriKind.Absolute,out var u)||u.Scheme!="https"||r.Category is not ("known" or "review")||r.Sha256.Length==0&&r.AllEntries.Length+r.AllStrings.Length<2&&(r.EntrySha256?.Count??0)<2||r.Sha256.Any(h=>h.Length!=64)))throw new InvalidDataException("규칙 형식 또는 검증 출처가 잘못되었습니다.");return pack;}
 public async Task<ScanReport> ScanAsync(string[] roots,ScanScope scope,IProgress<ScanProgress>? progress,CancellationToken ct,ScanPreferences? preferences=null){
  if(!scope.GameFiles)throw new InvalidOperationException("이 버전은 파일 검사를 포함한 검사만 지원합니다.");
  if(visitedFiles.Count>0||files.Count>0)throw new InvalidOperationException("새 검사에는 새 검사기를 사용하세요.");
  var started=DateTimeOffset.UtcNow.ToString("O");var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  var pending=new List<(string Path,string Label)>();bool cancelled=false,limited=false;
  var pulse=System.Diagnostics.Stopwatch.StartNew();
  void Note(string status,string reason){var i=coverage.FindIndex(c=>c.Collector=="files"&&c.Status==status&&c.Reason==reason);if(i<0)coverage.Add(new("files",status,reason,1));else coverage[i]=coverage[i] with{Inspected=coverage[i].Inspected+1};}
  progress?.Report(new("파일 목록 준비",0));
  try{
   for(int index=0;index<roots.Length&&!limited;index++){
    ct.ThrowIfCancellationRequested();var root=Path.GetFullPath(roots[index]);var queue=new Stack<string>();queue.Push(root);
    while(queue.TryPop(out var directory)&&!limited){
     ct.ThrowIfCancellationRequested();if(!seen.Add(directory))continue;
     if(seen.Count>100000){Note("partial","폴더 수 한도 100,000개 도달. 남은 위치 미검사");limited=true;break;}
     if(preferences?.Excludes(directory)==true){Note("partial","권장 검사 제외: "+Path.GetFileName(directory));continue;}
     try{
      if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0){Note("partial","링크 폴더 제외");continue;}
      var childDirectories=new List<string>();
      foreach(var entryInfo in new DirectoryInfo(directory).EnumerateFileSystemInfos("*",Enumeration)){
       var entry=entryInfo.FullName;
       ct.ThrowIfCancellationRequested();
       if(pulse.ElapsedMilliseconds>=200){progress?.Report(new("파일 목록 준비",pending.Count));pulse.Restart();}
       try{
        if(preferences?.Excludes(entry)==true){Note("partial","권장 검사 제외: "+Path.GetFileName(entry));continue;}
        var a=entryInfo.Attributes;if((a&FileAttributes.ReparsePoint)!=0){Note("partial","링크 항목 제외");continue;}
        if((a&FileAttributes.Directory)!=0){childDirectories.Add(entry);continue;}
        if(!scope.AllFiles&&!Extensions.Contains(Path.GetExtension(entry))){using var probe=File.OpenRead(entry);int a0=probe.ReadByte(),a1=probe.ReadByte();if(!((a0==0x50&&a1==0x4b)||(a0==0x4d&&a1==0x5a)))continue;}
        if(visitedFiles.Contains(entry))continue;
        if(pending.Count>=MaxFiles){Note("partial","파일 수 한도 250,000개 도달. 남은 위치 미검사");limited=true;break;}
        visitedFiles.Add(entry);
        var relative=Path.GetRelativePath(root,entry).Replace('\\','/');
        // Mask profile directory segments even when inspecting another user's profile.
        var absolute=Path.GetFullPath(entry).Replace('\\','/').Split('/');
        for(int p=0;p<absolute.Length-1;p++)if(absolute[p].Equals("Users",StringComparison.OrdinalIgnoreCase))relative=relative.Replace(absolute[p+1]+"/","[사용자]/",StringComparison.OrdinalIgnoreCase);
        relative=relative.Replace(Environment.UserName,"[사용자]",StringComparison.OrdinalIgnoreCase);
        pending.Add((entry,$"선택폴더{index+1}/"+relative));
       }catch(UnauthorizedAccessException){Note("denied","항목 읽기 권한 없음");}catch(IOException){Note("partial","목록 수집 중 항목 읽기 실패");}catch(System.Security.SecurityException){Note("denied","항목 보안 권한 없음");}
      }
      foreach(var child in childDirectories.OrderBy(DirectoryPriority))queue.Push(child);
     }catch(UnauthorizedAccessException){Note("denied","폴더 읽기 권한 없음");}catch(IOException){Note("partial","폴더를 읽을 수 없음");}catch(System.Security.SecurityException){Note("denied","폴더 보안 권한 없음");}
    }
   }
   coverage.Add(new("files","complete","목록 수집 시점에 발견한 파일을 검사. 이후 추가된 파일은 다음 검사 대상.",pending.Count));
   var timer=System.Diagnostics.Stopwatch.StartNew();pulse.Restart();progress?.Report(ScanTiming.Measure(0,pending.Count,timer.Elapsed));
   int next=-1;var merge=new object();
   // Each worker owns its parser state. Only completed file records are merged.
   await Task.WhenAll(Enumerable.Range(0,Math.Clamp(preferences?.Workers??2,1,2)).Select(_=>Task.Run(async()=>{
    var worker=new Scanner(rules);
    while(true){
     ct.ThrowIfCancellationRequested();int position=Interlocked.Increment(ref next);if(position>=pending.Count)break;
     var item=pending[position];await worker.InspectAsync(item.Path,item.Label,scope,ct);
     lock(merge){
      files.AddRange(worker.files);findings.AddRange(worker.findings);foreach(var pair in worker.LocalPaths)LocalPaths[pair.Key]=pair.Value;
      if(pulse.ElapsedMilliseconds>=200||files.Count==pending.Count){progress?.Report(ScanTiming.Measure(files.Count,pending.Count,timer.Elapsed));pulse.Restart();}
     }
     worker.files.Clear();worker.findings.Clear();worker.LocalPaths.Clear();
    }
   },ct)));
  }catch(OperationCanceledException){cancelled=true;coverage.Add(new("files","cancelled",$"사용자 취소. 발견한 {pending.Count:N0}개 중 {files.Count:N0}개 처리",files.Count));progress?.Report(new("취소됨",files.Count));}
  if(scope.AllFiles)coverage.Add(new("all-files","partial","모든 확장자의 목록·해시 검사. 지원하지 않는 형식은 내용 분석 제외. 파일당 256MB, 전체 250,000개 한도.",files.Count));
  var completion=cancelled?"cancelled":coverage.Any(c=>c.Status!="complete")||files.Any(f=>f.Status!="complete")?"partial":"complete";
  files.Sort((a,b)=>StringComparer.OrdinalIgnoreCase.Compare(a.Path,b.Path));
  var chunks=new List<ReportChunk>();foreach(var group in files.Chunk(100))chunks.Add(new(group.ToList(),[],[],[]));foreach(var group in findings.Chunk(100))chunks.Add(new([],[],group.ToList(),[]));foreach(var group in coverage.Chunk(50))chunks.Add(new([],[],[],group.ToList()));if(chunks.Count==0)chunks.Add(new([],[],[],[]));
  return new(new(1,rules.Version,completion,"complete",started,DateTimeOffset.UtcNow.ToString("O"),files.Count,0,findings.Count(f=>f.Category=="known"),findings.Count(f=>f.Category=="review"),findings.Count(f=>f.Category=="policy")),chunks);
 }
 private static int DirectoryPriority(string path)=>Path.GetFileName(path).ToLowerInvariant() switch {
  "users"=>100,"downloads"=>95,"desktop"=>90,"appdata"=>85,"temp"=>80,"mods"=>80,"programdata"=>60,"windows"=>-100,_=>0
 };
 private async Task InspectAsync(string path,string label,ScanScope scope,CancellationToken ct){
  string id=Guid.NewGuid().ToString();long size=0;try{
   var info=new FileInfo(path);size=info.Length;if((info.Attributes&FileAttributes.Offline)!=0||((int)info.Attributes&0x400000)!=0){files.Add(new(id,label,size,null,"클라우드 전용 파일은 다운로드하지 않음","unknown","skipped",null,[]));return;}var modified=info.LastWriteTimeUtc;if(size>MaxFile){files.Add(new(id,label,size,null,"256MB 크기 제한","unknown","skipped",null,[]));return;}
   await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,65536,FileOptions.Asynchronous|FileOptions.SequentialScan);var h=Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,ct));ct.ThrowIfCancellationRequested();stream.Position=0;
   byte[] magic=new byte[4];_ = await stream.ReadAsync(magic,ct);stream.Position=0;var entries=new HashSet<string>(StringComparer.Ordinal);var entryHashes=new Dictionary<string,string>(StringComparer.Ordinal);var strings=new StringBuilder();var mods=new HashSet<string>();string format="data",status="complete",reason="";
   if(magic[0]==0x50&&magic[1]==0x4b){format="zip";try{using var zip=new ZipArchive(stream,ZipArchiveMode.Read,true);long expanded=0;int count=0;foreach(var entry in zip.Entries){ct.ThrowIfCancellationRequested();if(++count>10000||entry.Length>16*1024*1024||(expanded+=entry.Length)>64*1024*1024||entry.Length>Math.Max(entry.CompressedLength,1)*500){status="partial";reason="압축 검사 한도";break;}var name=entry.FullName.Replace('\\','/');if(name.StartsWith('/')||name.Split('/').Contains("..")||name.Contains(':')){status="partial";reason="안전하지 않은 압축 경로";continue;}entries.Add(name);if(name=="pack.mcmeta")format="resource-pack";if(requestedHashes.Contains(name)){using var fingerprint=entry.Open();entryHashes[name]=Convert.ToHexStringLower(await SHA256.HashDataAsync(fingerprint,ct));}if(name.EndsWith(".jar")||name.EndsWith(".zip")){status="partial";reason="내장 압축파일은 별도 검사 필요";}
     if(name.EndsWith(".class")||name is "fabric.mod.json" or "META-INF/mods.toml" or "META-INF/neoforge.mods.toml" or "mcmod.info"){
      using var es=entry.Open();using var ms=new MemoryStream();await es.CopyToAsync(ms,ct);var text=name.EndsWith(".class")?ClassFile.Constants(ms.ToArray(),needsJavaStrings):Encoding.UTF8.GetString(ms.ToArray());if(strings.Length+text.Length<16*1024*1024)strings.Append(text);else{status="partial";reason="클래스 검사 한도";}
      if(name=="fabric.mod.json"){using var doc=JsonDocument.Parse(ms.ToArray());if(doc.RootElement.TryGetProperty("id",out var v)&&v.GetString() is string mod)mods.Add(mod);format="fabric-jar";}
      if(name.EndsWith("mods.toml")){format=name.Contains("neoforge")?"neoforge-jar":"forge-jar";foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text,"(?m)^\\s*modId\\s*=\\s*\"([a-zA-Z0-9_.-]+)\"",System.Text.RegularExpressions.RegexOptions.None,TimeSpan.FromSeconds(1)))mods.Add(match.Groups[1].Value);}
     }
    }}catch(InvalidDataException){status="error";reason="압축파일 또는 클래스 구조 오류";}catch(JsonException){status="partial";reason="모드 메타데이터 오류";}catch(InvalidOperationException){status="partial";reason="모드 메타데이터 형식 오류";}}
   else if(magic[0]=='M'&&magic[1]=='Z'){format="pe";if(size<64){files.Add(new(id,label,size,h,"잘못된 PE 헤더",format,"error","검사 불가",[]));return;}stream.Position=60;byte[] peOffset=new byte[4];await stream.ReadExactlyAsync(peOffset,ct);long offset=BitConverter.ToUInt32(peOffset);if(offset>size-4){files.Add(new(id,label,size,h,"잘못된 PE 헤더",format,"error","검사 불가",[]));return;}stream.Position=offset;byte[] signature=new byte[4];await stream.ReadExactlyAsync(signature,ct);if(!signature.SequenceEqual(new byte[]{80,69,0,0})){files.Add(new(id,label,size,h,"PE 서명 불일치",format,"error","검사 불가",[]));return;}if(needsPeStrings&&size<=32*1024*1024){stream.Position=0;using var ms=new MemoryStream();await stream.CopyToAsync(ms,ct);strings.Append(Encoding.Latin1.GetString(ms.ToArray()));}else if(needsPeStrings){status="partial";reason="PE 패턴 검사 한도";}}
   else if(!Extensions.Contains(Path.GetExtension(path))){format="hash-only";reason="해시 검사만 지원하는 형식";}
   else if(size<=2*1024*1024){stream.Position=0;using var reader=new StreamReader(stream,Encoding.UTF8,true,4096,true);strings.Append(await reader.ReadToEndAsync(ct));format="text";}
   else{status="partial";reason="텍스트 패턴 검사 크기 한도";}
   if(Path.GetFileName(path).Equals("pack.mcmeta",StringComparison.OrdinalIgnoreCase)){format="resource-pack-folder";try{entryHashes=await ResourcePack.FolderHashes(Path.GetDirectoryName(path)!,requestedHashes,ct);}catch(IOException){status="partial";reason="리소스팩 내부 파일 검사 불가";}catch(UnauthorizedAccessException){status="partial";reason="리소스팩 내부 파일 접근 거부";}}info.Refresh();if(info.Length!=size||info.LastWriteTimeUtc!=modified){files.Add(new(id,label,size,null,"검사 중 파일 변경",format,"changed",null,[]));return;}
   var content=strings.ToString();foreach(var rule in rules.Rules){ct.ThrowIfCancellationRequested();if(rule.ExcludeSha256.Contains(h)||(rule.Formats is {Length:>0}&&!rule.Formats.Contains(format)))continue;bool exact=rule.Sha256.Contains(h);bool pattern=rule.AllEntries.Length+rule.AllStrings.Length>=2&&rule.AllEntries.All(entries.Contains)&&rule.AllStrings.All(s=>content.Contains(s,StringComparison.Ordinal));bool bundle=rule.EntrySha256 is {Count:>=2}&&rule.EntrySha256.All(pair=>entryHashes.TryGetValue(pair.Key,out var actual)&&actual==pair.Value);if(exact||pattern||bundle)findings.Add(new(rule.Id,id,(exact||bundle)?rule.Category:"review",rule.Title,exact?[$"SHA-256 일치: {h}"]:bundle?["검증된 내부 파일 SHA-256 조합 일치",..rule.EntrySha256!.Keys]:["복수 내부 특징 일치",..rule.AllEntries.Take(8),..rule.AllStrings.Take(8)],rule.Source));}
   foreach(var mod in mods.Intersect(scope.BannedModIds??[]))findings.Add(new("server-policy",id,"policy",$"서버 규정 대상: {mod}",["메타데이터 mod ID 일치"],"https://github.com/"));
   files.Add(new(id,label,size,h,string.IsNullOrEmpty(reason)?null:reason,format,status,format=="pe"?"검증 대기":null,mods.Take(100).ToArray()));LocalPaths[path]=id;
  }catch(OperationCanceledException){throw;}catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){files.Add(new(id,label,size,null,e is UnauthorizedAccessException?"읽기 권한 없음":"파일 읽기 실패","unknown","error",null,[]));}
 }
}

