using System.IO;
using System.Windows;
using Watchblock.Core;
namespace Watchblock.App;
public partial class MainWindow {
 private static string SystemRoot => Path.GetPathRoot(Environment.SystemDirectory)??@"C:\";
 private void MergeDeletedFiles(Watchblock.Windows.TraceOutput traces){
  if(report==null)return;var files=(traces.Files??[]).Take(Math.Max(0,Scanner.MaxFiles-report.Summary.Files)).ToList();var ids=files.Select(f=>f.Id).ToHashSet();var findings=(traces.Findings??[]).Where(f=>f.FileId==null||ids.Contains(f.FileId)).ToList();
  foreach(var group in files.Chunk(100))report.Chunks.Add(new(group.ToList(),[],[],[]));foreach(var group in findings.Chunk(100))report.Chunks.Add(new([],[],group.ToList(),[]));
  var removed=(traces.Files??[]).Where(f=>!ids.Contains(f.Id)).Select(f=>f.Id).ToHashSet();
  if(removed.Count>0)foreach(var chunk in report.Chunks)for(int i=0;i<chunk.Artifacts.Count;i++){var artifact=chunk.Artifacts[i];if(artifact.FileId!=null&&removed.Contains(artifact.FileId))chunk.Artifacts[i]=artifact with{FileId=null,Association="none",Note=artifact.Note+" 파일 수 제한으로 내용 결과 제외."};}
  if(files.Count<(traces.Files?.Count??0))report.Chunks.Add(new([],[],[],[new("recycle-bin","partial","전체 파일 수 한도로 일부 휴지통 파일 결과 제외",files.Count)]));
  report=report with{Summary=report.Summary with{Files=report.Summary.Files+files.Count,Known=report.Summary.Known+findings.Count(f=>f.Category=="known"),Review=report.Summary.Review+findings.Count(f=>f.Category=="review"),Policy=report.Summary.Policy+findings.Count(f=>f.Category=="policy")}};
 }
 private void Recommended_Click(object sender,RoutedEventArgs e) {
  try {
   if(claim?.Session.Scope.AllFiles!=true)return;
   if(!Directory.Exists(SystemRoot))throw new IOException("시스템 드라이브를 찾지 못했습니다.");
   Roots.Items.Clear();Roots.Items.Add(SystemRoot);SkipSystemStores.IsChecked=true;Consent.IsChecked=false;RefreshFlow();
   Status.Text="사용자 폴더·AppData·다운로드 검사. 게임 데이터·드라이버·임시 저장소는 선택한 제외 설정을 적용합니다.";
  }catch(Exception ex){Status.Text=ex.Message;}
 }
 private void Exclusions_Changed(object sender,RoutedEventArgs e){if(Consent!=null){Consent.IsChecked=false;RefreshFlow();}}
 private void ShowScanProgress(ScanProgress p) {
  if(p.Stage=="파일 검사"||p.Stage=="파일 목록 준비")inspected=p.Total.HasValue?p.Count:0;ProgressTitle.Text=p.Stage;
  ScanActivity.IsIndeterminate=!p.Percent.HasValue;
  var previous=ScanActivity.Value;ScanActivity.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty,null);ScanActivity.Value=p.Percent??0;
  if(p.Percent.HasValue&&p.Percent>=previous&&SystemParameters.ClientAreaAnimation)ScanActivity.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty,new System.Windows.Media.Animation.DoubleAnimation(previous,p.Percent.Value,TimeSpan.FromMilliseconds(160)));
  ProgressPercent.Text=p.Percent.HasValue?$"{p.Percent.Value:F1}%":"";
  ProgressCount.Text=p.Total.HasValue?$"{p.Count:N0} / {p.Total:N0}개 파일 처리":$"{p.Count:N0}개 파일 발견";
  ProgressEta.Text=p.Total.HasValue?p.RemainingSeconds is double seconds
   ?seconds<=0?"다음 단계 준비 중":$"예상 남은 시간: 약 {FormatRemaining(seconds)}"
   :"예상 시간 계산 중…":"파일 목록 준비 중";
  Status.Text=$"{p.Stage} · {p.Count:N0}개";
 }
 private static string FormatRemaining(double seconds){var minutes=Math.Ceiling(seconds/60);return seconds<60?"1분 미만":minutes<60?$"{minutes:N0}분":$"{Math.Floor(minutes/60):N0}시간 {minutes%60:N0}분";}
 private void ShowFollowupProgress(string title){ProgressTitle.Text=title;ProgressPercent.Text="";ProgressEta.Text="파일 분석 이후 진행하는 별도 검사입니다.";ScanActivity.IsIndeterminate=true;}
 private Task VerifySignaturesAsync(ScanReport current,Dictionary<string,string> paths,CancellationToken ct) {
  var targets=current.Chunks.SelectMany(chunk=>Enumerable.Range(0,chunk.Files.Count).Where(i=>chunk.Files[i].Format=="pe").Select(i=>(Chunk:chunk,Index:i))).ToArray();
  var updates=new Progress<ScanProgress>(ShowScanProgress);var timer=System.Diagnostics.Stopwatch.StartNew();var pulse=System.Diagnostics.Stopwatch.StartNew();var gate=new object();int completed=0;
  return Task.Run(()=>Parallel.ForEach(targets,new ParallelOptions{MaxDegreeOfParallelism=2,CancellationToken=ct},item=>{
   var file=item.Chunk.Files[item.Index];var path=paths.GetValueOrDefault(file.Id);
   if(path!=null)item.Chunk.Files[item.Index]=file with{Signature=Watchblock.Windows.Signatures.Verify(path)};
   lock(gate){completed++;if(pulse.ElapsedMilliseconds>=200||completed==targets.Length){((IProgress<ScanProgress>)updates).Report(ScanTiming.Measure(completed,targets.Length,timer.Elapsed) with{Stage="파일 서명 확인"});pulse.Restart();}}
  }),ct);
 }
}
