using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Watchblock.Core;
namespace Watchblock.Windows;

// Bounded, read-only NTFS journal lookup. Never creates, deletes or resets a journal.
public static class DeletionJournal {
 public record Entry(ulong File,ulong Parent,long Usn,DateTimeOffset Time,uint Reason,string Name);
 public static Entry OriginalName(Entry deleted,IReadOnlyDictionary<ulong,Entry> aliases)=>
  deleted.Name.StartsWith("$R",StringComparison.OrdinalIgnoreCase)&&aliases.TryGetValue(deleted.File,out var previous)&&previous.Usn<deleted.Usn&&previous.Time<=deleted.Time?previous:deleted;
 public static List<Entry> ParseBuffer(ReadOnlySpan<byte> buffer,out long next,out int unsupported){
  if(buffer.Length<8)throw new InvalidDataException("USN header");next=BitConverter.ToInt64(buffer);unsupported=0;var entries=new List<Entry>();
  for(int offset=8;offset<buffer.Length;){
   if(buffer.Length-offset<8)throw new InvalidDataException("USN record header");var record=buffer[offset..];uint length=BitConverter.ToUInt32(record);
   if(length<8||length>record.Length)throw new InvalidDataException("USN record length");
   if(BitConverter.ToUInt16(record[4..])!=2){unsupported++;offset+=(int)length;continue;}
   if(length<60)throw new InvalidDataException("USN V2 length");int nameLength=BitConverter.ToUInt16(record[56..]),nameOffset=BitConverter.ToUInt16(record[58..]);
   if(nameOffset<60||nameLength%2!=0||nameOffset+nameLength>length)throw new InvalidDataException("USN name bounds");
   string name=new UnicodeEncoding(false,false,true).GetString(record.Slice(nameOffset,nameLength));
   if(name.Length==0||name.IndexOfAny(['\\','/',':','\0'])>=0)throw new InvalidDataException("USN file name");
   if((BitConverter.ToUInt32(record[52..])&0x10)==0)entries.Add(new(BitConverter.ToUInt64(record[8..]),BitConverter.ToUInt64(record[16..]),BitConverter.ToInt64(record[24..]),DateTimeOffset.FromFileTime(BitConverter.ToInt64(record[32..])),BitConverter.ToUInt32(record[40..]),name));
   offset+=(int)length;
  }return entries;
 }
 public static TraceOutput Collect(string[] roots,CancellationToken ct){
  var output=new TraceOutput([],[]);var timer=Stopwatch.StartNew();
  try{foreach(var volume in roots.Select(Path.GetPathRoot).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)){
   ct.ThrowIfCancellationRequested();if(volume.Length!=3||!char.IsAsciiLetter(volume[0]))continue;
   using var handle=CreateFile(@"\\.\"+volume[..2],0x80000000,7,IntPtr.Zero,3,0,IntPtr.Zero);
   if(handle.IsInvalid){output.Coverage.Add(new("usn","denied","NTFS 삭제 기록은 관리자 권한이 필요하거나 볼륨에 접근할 수 없습니다.",0));continue;}
   var query=new byte[80];if(!DeviceIoControl(handle,0x900f4,[],0,query,query.Length,out int returned,IntPtr.Zero)||returned<56){output.Coverage.Add(new("usn","unsupported","NTFS 저널이 없거나 조회할 수 없습니다. 삭제 기록 미검사",0));continue;}
   long end=BitConverter.ToInt64(query,16),start=Math.Max(Math.Max(BitConverter.ToInt64(query,8),BitConverter.ToInt64(query,24)),end-32L*1024*1024);ulong journal=BitConverter.ToUInt64(query);
   var aliases=new Dictionary<ulong,Entry>();var parents=new Dictionary<ulong,string?>();var buffer=new byte[65536];int read=0,unresolved=0,unsupported=0;bool limited=false;
   string? Resolve(ulong id){if(parents.TryGetValue(id,out var path))return path;if(parents.Count>=2048)return null;var descriptor=new FileIdDescriptor{Size=24,Type=0,Id=id};using var directory=OpenFileById(handle,ref descriptor,0x80,7,IntPtr.Zero,0x02000000);if(directory.IsInvalid)return parents[id]=null;var text=new StringBuilder(32768);uint n=GetFinalPathNameByHandle(directory,text,text.Capacity,0);if(n==0||n>=text.Capacity)return parents[id]=null;path=text.ToString();return parents[id]=path.StartsWith(@"\\?\")?path[4..]:path;}
   while(start<end){
    ct.ThrowIfCancellationRequested();if(timer.Elapsed.TotalSeconds>8||output.Artifacts.Count>=500||read>=20000){limited=true;break;}
    var input=new byte[40];BitConverter.GetBytes(start).CopyTo(input,0);BitConverter.GetBytes(0x1200u).CopyTo(input,8);BitConverter.GetBytes(journal).CopyTo(input,32);
    if(!DeviceIoControl(handle,0x900bb,input,input.Length,buffer,buffer.Length,out returned,IntPtr.Zero)){output.Coverage.Add(new("usn","partial","저널 읽기 중 변경·접근 실패. 남은 기록 미검사",read));break;}
    var entries=ParseBuffer(buffer.AsSpan(0,returned),out var next,out var skipped);unsupported+=skipped;if(next<=start)break;start=next;
    foreach(var entry in entries){
     ct.ThrowIfCancellationRequested();if(entry.Usn>=end)continue;read++;if(entry.Time<DateTimeOffset.UtcNow.AddDays(-7)||entry.Time>DateTimeOffset.UtcNow.AddMinutes(5))continue;
     if(!DeletedFiles.Relevant(entry.Name))continue;
     if((entry.Reason&0x1000)!=0&&!entry.Name.StartsWith("$R",StringComparison.OrdinalIgnoreCase)&&aliases.Count<10000)aliases[entry.File]=entry;
     if((entry.Reason&0x200)==0)continue;
     if(output.Artifacts.Count>=500){limited=true;break;}
     bool recycled=entry.Name.StartsWith("$R",StringComparison.OrdinalIgnoreCase);var original=OriginalName(entry,aliases);
     var parent=Resolve(original.Parent);if(parent==null){unresolved++;continue;}
     var label=DeletedFiles.ScopeLabel(Path.Combine(parent,original.Name),roots);if(label==null)continue;
     string note=recycled&&original!=entry?"휴지통 내용 삭제 기록. 이전 이름변경 기록과 동일 NTFS 파일 ID로 연결.":recycled?"휴지통 내부 파일 삭제 기록. 원래 파일명은 확인되지 않음.":"파일 삭제 기록. 휴지통 비우기·직접 삭제·프로그램 삭제 여부와 삭제 주체는 확정할 수 없음.";
     output.Artifacts.Add(new("usn",label,entry.Time.ToUniversalTime().ToString("O"),null,"none",note+" 부모 폴더는 현재 경로로 해석. 삭제된 내용·해시 미복구, 치트 여부 미확인."));
    }
   }
   output.Coverage.Add(new("usn","partial",$"잔존 저널 마지막 32MiB 중 최근 7일 삭제/이름변경 기록. 경로 미해석 {unresolved}건·미지원 {unsupported}건. 이름만으로 치트 확정 안 함. 기록 없음은 삭제하지 않았다는 증거가 아님.{(limited?" 시간·수량 한도 도달.":"")}",read));
  }}catch(OperationCanceledException){output.Coverage.Add(new("usn","cancelled","삭제 기록 검사 취소",0));}
  catch(Exception ex)when(ex is IOException or ArgumentException or UnauthorizedAccessException){output.Coverage.Add(new("usn","failed","삭제 저널 읽기 또는 해석 실패",0));}
  return output;
 }
 [StructLayout(LayoutKind.Explicit,Size=24)]private struct FileIdDescriptor{[FieldOffset(0)]public uint Size;[FieldOffset(4)]public uint Type;[FieldOffset(8)]public ulong Id;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool DeviceIoControl(SafeFileHandle handle,uint code,byte[] input,int inputSize,byte[] output,int outputSize,out int returned,IntPtr overlapped);
 [DllImport("kernel32.dll",SetLastError=true)]private static extern SafeFileHandle OpenFileById(SafeFileHandle volume,ref FileIdDescriptor id,uint access,uint share,IntPtr security,uint flags);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle,StringBuilder path,int length,uint flags);
}
