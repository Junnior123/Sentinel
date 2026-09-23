namespace Watchblock.Core;
public static class UploadOrder {
 public static List<ReportChunk> Prioritize(ScanReport report,bool evidenceOnly=false) {
  var findings=report.Chunks.SelectMany(c=>c.Findings).ToList();
  if(evidenceOnly){
   var files=report.Chunks.SelectMany(c=>c.Files).ToDictionary(f=>f.Id);
   findings=findings.Select(f=>f.FileId!=null&&files.TryGetValue(f.FileId,out var file)?f with{Evidence=[..f.Evidence.Take(18),Bound("파일: "+file.Path),Bound("SHA-256: "+(file.Sha256??file.HashReason??"미수집"))]}:f).ToList();
  }
  var matched=findings.Where(f=>f.FileId!=null).Select(f=>f.FileId!).ToHashSet();
  var ordered=new List<ReportChunk>();
  foreach(var group in findings.Chunk(100))ordered.Add(new([],[],group.ToList(),[]));
  foreach(var group in report.Chunks.SelectMany(c=>c.Coverage).Chunk(50))ordered.Add(new([],[],[],group.ToList()));
  foreach(var group in report.Chunks.SelectMany(c=>c.Artifacts).Chunk(100))ordered.Add(new([],group.ToList(),[],[]));
  if(!evidenceOnly)foreach(var group in report.Chunks.SelectMany(c=>c.Files).OrderByDescending(f=>matched.Contains(f.Id)).Chunk(100))ordered.Add(new(group.ToList(),[],[],[]));
  else {
   var incomplete=report.Chunks.SelectMany(c=>c.Files).Where(f=>f.Status!="complete").GroupBy(f=>(f.Status,f.HashReason));
   var gaps=incomplete.Select(g=>new Coverage("file-analysis","partial",$"{g.Key.Status}: {g.Key.HashReason??"파일 분석 미완료"}",g.Count())).ToList();
   gaps.Add(new("submission","complete","탐지 근거·관련 파일 경로와 해시·실행 흔적·검사 누락 전송. 전체 파일 목록은 앱에서 로컬 저장 가능.",report.Summary.Files));
   foreach(var group in gaps.Chunk(50))ordered.Add(new([],[],[],group.ToList()));
  }
  if(ordered.Count==0)ordered.Add(new([],[],[],[]));
  return ordered.SelectMany(Fit).ToList();
 }
 private static string Bound(string value)=>value[..Math.Min(1024,value.Length)];
 private static IEnumerable<ReportChunk> Fit(ReportChunk chunk){
  if(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(chunk,Json.Options).Length<=240*1024){yield return chunk;yield break;}
  // Prioritize creates homogeneous chunks; split long evidence instead of dropping a whole batch.
  int count=Math.Max(Math.Max(chunk.Files.Count,chunk.Findings.Count),Math.Max(chunk.Artifacts.Count,chunk.Coverage.Count));
  if(count<=1)throw new InvalidDataException("보고서 항목이 전송 한도를 초과했습니다. 로컬 보고서를 저장해 주세요.");
  int mid=count/2;
  var first=new ReportChunk(chunk.Files.Take(mid).ToList(),chunk.Artifacts.Take(mid).ToList(),chunk.Findings.Take(mid).ToList(),chunk.Coverage.Take(mid).ToList());
  var second=new ReportChunk(chunk.Files.Skip(mid).ToList(),chunk.Artifacts.Skip(mid).ToList(),chunk.Findings.Skip(mid).ToList(),chunk.Coverage.Skip(mid).ToList());
  foreach(var part in Fit(first))yield return part;
  foreach(var part in Fit(second))yield return part;
 }
}
