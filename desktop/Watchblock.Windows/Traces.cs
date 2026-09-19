using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using Watchblock.Core;
namespace Watchblock.Windows;
public record TraceOutput(List<ExecutionArtifact> Artifacts,List<Coverage> Coverage,List<FileRecord>? Files=null,List<Finding>? Findings=null);
public record TraceRequest(Dictionary<string,string> Paths,string[] Roots,bool DeletedHistory,ScanScope Scope);
public static class Traces {
 public static TraceOutput Collect(Dictionary<string,string> paths,CancellationToken ct,string[]? roots=null,bool deletedHistory=false,ScanScope? scope=null){
  var pathsByName=paths.GroupBy(p=>Path.GetFileName(p.Key),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.OrdinalIgnoreCase);var artifacts=new List<ExecutionArtifact>();var coverage=new List<Coverage>();
  string Mask(string p)=>paths.TryGetValue(p,out var id)?"검사파일/"+id:Path.GetFileName(p);
  foreach(var process in Process.GetProcesses()){using(process){try{ct.ThrowIfCancellationRequested();var p=process.MainModule?.FileName;if(p!=null&&paths.TryGetValue(p,out var id))artifacts.Add(new("process",Mask(p),process.StartTime.ToUniversalTime().ToString("O"),id,"path-only","현재 실행 경로 일치. 게임 내 사용을 증명하지 않음."));}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){/* Unrelated protected processes are deliberately not inspected. */}}}
  coverage.Add(new("process","partial","등록된 파일 경로와 연결 가능한 프로세스만 검사. 보호 프로세스·게임 내 로드 여부 미검사.",artifacts.Count));
  try{
   using var current=WindowsIdentity.GetCurrent();string? sid=current.User?.Value;int read=0;
   using var bam=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\bam\State\UserSettings\"+sid);
   if(bam==null)coverage.Add(new("bam","unsupported","현재 사용자 BAM 기록 없음",0));else{foreach(var n in bam.GetValueNames()){ct.ThrowIfCancellationRequested();if(bam.GetValue(n) is not byte[] b||b.Length<8)continue;read++;string p=DevicePath(n);if(paths.TryGetValue(p,out var id)){string? time=null;try{time=DateTime.FromFileTimeUtc(BitConverter.ToInt64(b)).ToString("O");}catch(ArgumentOutOfRangeException){}artifacts.Add(new("bam",Mask(p),time,id,"path-only","과거 경로 기록. 현재 파일과 과거 실행 파일의 내용 동일성 미확인."));}}coverage.Add(new("bam","complete","현재 사용자 기록 중 검사 파일과 경로 일치 항목 수집",read));}
  }catch(UnauthorizedAccessException){coverage.Add(new("bam","denied","관리자 권한이 필요합니다.",0));}catch(System.Security.SecurityException){coverage.Add(new("bam","denied","레지스트리 접근 거부",0));}catch(IOException){coverage.Add(new("bam","failed","BAM 읽기 실패",0));}
  try{int read=0,invalid=0;foreach(var file in Directory.EnumerateFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Prefetch"),"*.pf")){
   ct.ThrowIfCancellationRequested();try{if(new FileInfo(file).Length>16*1024*1024){invalid++;continue;}var pf=ParsePrefetch(File.ReadAllBytes(file));read++;
    foreach(var (path,id) in pathsByName.GetValueOrDefault(pf.Executable)??[]){foreach(var time in pf.Times)artifacts.Add(new("prefetch",Mask(path),time,id,"basename-only","Prefetch 실행 파일 이름 일치. 동일 경로·동일 바이너리 여부는 확인되지 않음."));}
   }catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or OverflowException){invalid++;}}
   coverage.Add(new("prefetch",invalid>0?"partial":"complete",invalid>0?$"읽지 못한 Prefetch {invalid}개":"지원 형식 기록 읽기 완료. 기록 없음은 실행하지 않았다는 증거가 아님.",read));
  }catch(UnauthorizedAccessException){coverage.Add(new("prefetch","denied","관리자 권한이 필요합니다.",0));}catch(IOException){coverage.Add(new("prefetch","failed","Prefetch 경로 접근 실패",0));}
  if(artifacts.Count>20000)coverage.Add(new("traces","partial","실행 흔적 수 한도 20,000개 초과",20000));
  var output=artifacts.Take(20000).ToList();
  if(deletedHistory&&roots is {Length:>0}){var history=DeletedFiles.Collect(roots,Scanner.LoadRules(Path.Combine(AppContext.BaseDirectory,"rules/catalog.json")),scope??new(),ct);output.AddRange(history.Artifacts);coverage.AddRange(history.Coverage);return new(output,coverage,history.Files,history.Findings);}
  else coverage.Add(new("recycle-bin","unsupported","휴지통·삭제 기록 검사 선택 안 함",0));
  return new(output,coverage);
 }
 public record PrefetchRecord(string Executable,string[] Times);
 public static PrefetchRecord ParsePrefetch(byte[] b){
  if(b.Length>=8&&Encoding.ASCII.GetString(b,0,3)=="MAM"){
   int size=BitConverter.ToInt32(b,4);if(size<84||size>16*1024*1024)throw new InvalidDataException("Prefetch size");
   ushort format=(ushort)(b[3]&15);if(format!=4)throw new InvalidDataException("Prefetch compression");int offset=(b[3]&0x80)!=0?12:8;if(b.Length<=offset)throw new InvalidDataException();
   if(RtlGetCompressionWorkSpaceSize(format,out uint workspace,out _)!=0)throw new InvalidDataException();var input=b.AsSpan(offset).ToArray();var output=new byte[size];var work=new byte[workspace];
   if(RtlDecompressBufferEx(format,output,(uint)size,input,(uint)input.Length,out uint final,work)!=0||final!=size)throw new InvalidDataException("Prefetch decompress");b=output;
  }
  if(b.Length<156||Encoding.ASCII.GetString(b,4,4)!="SCCA")throw new InvalidDataException("Prefetch signature");int version=BitConverter.ToInt32(b);if(version is not (17 or 23 or 26 or 30 or 31))throw new InvalidDataException("Prefetch version");
  string exe=Encoding.Unicode.GetString(b,16,60).TrimEnd('\0');int offsetTime=version==17?120:128;int count=version>=26?8:1;if(offsetTime+count*8>b.Length)throw new InvalidDataException();var times=new List<string>();for(int i=0;i<count;i++){long t=BitConverter.ToInt64(b,offsetTime+i*8);if(t>0)times.Add(DateTime.FromFileTimeUtc(t).ToString("O"));}return new(exe,times.ToArray());
 }
 private static string DevicePath(string path){if(!path.StartsWith(@"\Device\",StringComparison.OrdinalIgnoreCase))return path;foreach(var drive in DriveInfo.GetDrives()){var name=drive.Name[..2];var text=new StringBuilder(1024);if(QueryDosDevice(name,text,text.Capacity)>0&&path.StartsWith(text.ToString()+"\\",StringComparison.OrdinalIgnoreCase))return name+path[text.Length..];}return path;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern uint QueryDosDevice(string device,StringBuilder target,int size);
 [DllImport("ntdll.dll")]private static extern int RtlGetCompressionWorkSpaceSize(ushort format,out uint workspace,out uint fragment);
 [DllImport("ntdll.dll")]private static extern int RtlDecompressBufferEx(ushort format,byte[] output,uint outputSize,byte[] input,uint inputSize,out uint final,byte[] workspace);
}
