namespace Watchblock.Core;
public static class UploadOrder {
 public static List<ReportChunk> Prioritize(ScanReport report) {
  var findings=report.Chunks.SelectMany(c=>c.Findings).ToList();
  var matched=findings.Where(f=>f.FileId!=null).Select(f=>f.FileId!).ToHashSet();
  var ordered=new List<ReportChunk>();
  foreach(var group in findings.Chunk(100))ordered.Add(new([],[],group.ToList(),[]));
  foreach(var group in report.Chunks.SelectMany(c=>c.Coverage).Chunk(50))ordered.Add(new([],[],[],group.ToList()));
  foreach(var group in report.Chunks.SelectMany(c=>c.Artifacts).Chunk(100))ordered.Add(new([],group.ToList(),[],[]));
  foreach(var group in report.Chunks.SelectMany(c=>c.Files).OrderByDescending(f=>matched.Contains(f.Id)).Chunk(100))ordered.Add(new(group.ToList(),[],[],[]));
  if(ordered.Count==0)ordered.Add(new([],[],[],[]));
  return ordered;
 }
}
