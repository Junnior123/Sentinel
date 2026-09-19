using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using Watchblock.Core;
namespace Watchblock.Windows;

public static class DeletedFiles {
 public record RecycleRecord(string OriginalPath,long Size,DateTimeOffset DeletedAt);
 public static RecycleRecord ParseRecycle(ReadOnlySpan<byte> data){
  if(data.Length<26)throw new InvalidDataException("휴지통 메타데이터 길이");
  long version=BitConverter.ToInt64(data),size=BitConverter.ToInt64(data[8..]);if(size<0)throw new InvalidDataException("파일 크기");
  var date=DateTimeOffset.FromFileTime(BitConverter.ToInt64(data[16..]));int offset,length;
  if(version==1){offset=24;length=520;if(data.Length<offset+length)throw new InvalidDataException("v1 경로 길이");}
  else if(version==2){if(data.Length<28)throw new InvalidDataException();offset=28;uint chars=BitConverter.ToUInt32(data[24..]);if(chars<1||chars>32768||chars*2>data.Length-offset)throw new InvalidDataException("v2 경로 길이");length=(int)chars*2;}
  else throw new InvalidDataException("미지원 휴지통 버전");
  var path=new UnicodeEncoding(false,false,true).GetString(data.Slice(offset,length)).TrimEnd('\0');
  if(path.Contains('\0')||!Path.IsPathFullyQualified(path)||path.StartsWith(@"\\"))throw new InvalidDataException("휴지통 경로");
  return new(Path.GetFullPath(path),size,date);
 }
 public static string? ScopeLabel(string path,string[] roots){
  var full=Path.GetFullPath(path);for(int i=0;i<roots.Length;i++){
   var root=Path.GetFullPath(roots[i]).TrimEnd('\\');if(!full.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase))continue;
   var relative=Path.GetRelativePath(root,full).Replace('\\','/');var parts=full.Replace('\\','/').Split('/');
   for(int n=0;n<parts.Length-1;n++)if(parts[n].Equals("Users",StringComparison.OrdinalIgnoreCase))relative=relative.Replace(parts[n+1]+"/","[사용자]/",StringComparison.OrdinalIgnoreCase);
   relative=relative.Replace(Environment.UserName,"[사용자]",StringComparison.OrdinalIgnoreCase);
   return $"선택폴더{i+1}/"+(relative.Length>900?"[긴 경로 생략]":relative);
  }return null;
 }
 public static bool Relevant(string path)=>Path.GetExtension(path).ToLowerInvariant() is ".jar" or ".zip" or ".exe" or ".dll" or ".ahk" or ".lua" or ".sys";
 public static Finding? NameReview(ExecutionArtifact artifact,RulePack rules){
  if(artifact.Source!="usn"||artifact.FileId!=null)return null;
  var name=Path.GetFileName(artifact.Path);var rule=rules.Rules.FirstOrDefault(r=>r.DeletedNameHints?.Any(h=>h.Length>=4&&name.Contains(h,StringComparison.OrdinalIgnoreCase))==true);
  return rule==null?null:new("deleted-name-hint",null,"review","삭제된 파일 이름 검토",[artifact.Path,artifact.Time??"시각 미확인","규칙의 이름 단서와 일치. 삭제된 내용·해시가 없어 치트 파일인지 확정할 수 없음.",artifact.Note],rule.Source);
 }
 public static ReportChunk Collect(string[] roots,RulePack rules,ScanScope scope,CancellationToken ct){
  var result=new ReportChunk([],[],[],[]);int read=0,invalid=0,denied=0,retained=0;bool limited=false;
  using var identity=WindowsIdentity.GetCurrent();var sid=identity.User?.Value;
  if(sid==null){result.Coverage.Add(new("recycle-bin","denied","현재 사용자 휴지통 식별 실패",0));return result;}
  var timer=Stopwatch.StartNew();
  try{foreach(var volume in roots.Select(Path.GetPathRoot).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)){
   ct.ThrowIfCancellationRequested();var bin=Path.Combine(volume,"$Recycle.Bin",sid);
   try{
    if(!Directory.Exists(bin))continue;
    if((File.GetAttributes(Path.Combine(volume,"$Recycle.Bin"))&FileAttributes.ReparsePoint)!=0||(File.GetAttributes(bin)&FileAttributes.ReparsePoint)!=0){invalid++;continue;}
    foreach(var item in new DirectoryInfo(bin).EnumerateFiles("$I*")){
     ct.ThrowIfCancellationRequested();if(read>=500||timer.Elapsed.TotalSeconds>10){limited=true;break;}read++;
     try{
      if(item.Length>65564||(item.Attributes&FileAttributes.ReparsePoint)!=0){invalid++;continue;}
      var record=ParseRecycle(File.ReadAllBytes(item.FullName));var label=ScopeLabel(record.OriginalPath,roots);if(label==null||!Relevant(record.OriginalPath))continue;
      var content=Path.Combine(bin,"$R"+item.Name[2..]);string? fileId=null;string note="휴지통 메타데이터. 파일 내용 없음·원래 파일의 치트 여부 미확인.";
      if(File.Exists(content)){
       if((File.GetAttributes(content)&FileAttributes.ReparsePoint)!=0){invalid++;note="휴지통 내용이 링크여서 미검사.";}
       else{
        var inspected=new Scanner(rules).ScanRetainedFileAsync(content,"휴지통/"+label,scope,ct).GetAwaiter().GetResult();
        result.Files.AddRange(inspected.Files);result.Findings.AddRange(inspected.Findings);fileId=inspected.Files.FirstOrDefault()?.Id;retained++;
        note="휴지통에 남은 내용 검사. 표시 시각은 휴지통에 넣은 시각이며 비운 시각이 아님. 게임 사용 증거가 아님.";
       }
      }
      result.Artifacts.Add(new("recycle-bin",label,record.DeletedAt.ToUniversalTime().ToString("O"),fileId,fileId==null?"none":"path-only",note));
     }catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){invalid++;}
    }
   }catch(UnauthorizedAccessException){denied++;}catch(IOException){invalid++;}
  }}catch(OperationCanceledException){result.Coverage.Add(new("recycle-bin","cancelled","휴지통 검사 취소",read));return result;}
  result.Coverage.Add(new("recycle-bin","partial",$"현재 사용자 휴지통의 실행 파일·모드·압축·스크립트만 확인. 잔존 내용 {retained}개 검사. 비운 항목은 NTFS 기록에서 별도 확인. 미해석 {invalid}건, 접근 거부 {denied}곳.{(limited?" 500건/10초 한도 도달.":"")}",read));
  var journal=DeletionJournal.Collect(roots,ct);result.Artifacts.AddRange(journal.Artifacts);result.Coverage.AddRange(journal.Coverage);
  foreach(var artifact in journal.Artifacts){var review=NameReview(artifact,rules);if(review!=null)result.Findings.Add(review);}
  return result;
 }
}
