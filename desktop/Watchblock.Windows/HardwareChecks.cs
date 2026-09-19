using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using Watchblock.Core;
namespace Watchblock.Windows;

public record HardwareBaseline(int Version,string Captured,Dictionary<string,string> Digests);
public record HardwareResult(List<Finding> Findings,List<ExecutionArtifact> Artifacts,List<Coverage> Coverage);
public static class HardwareChecks {
 public const string Source="https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-getsystemfirmwaretable";
 public static string[] Compare(HardwareBaseline previous,HardwareBaseline current)=>current.Digests.Where(p=>previous.Digests.TryGetValue(p.Key,out var old)&&old!=p.Value).Select(p=>p.Key).ToArray();
 public static HardwareResult Collect(string baselinePath,CancellationToken ct) {
  var result=new HardwareResult([],[],[]);var digests=new Dictionary<string,string>();
  ct.ThrowIfCancellationRequested();
  try {
   const uint rsmb=0x52534D42;uint length=GetSystemFirmwareTable(rsmb,0,null,0);
   if(length<8||length>1024*1024)throw new InvalidDataException("SMBIOS 크기 또는 접근 오류");
   var buffer=new byte[length];if(GetSystemFirmwareTable(rsmb,0,buffer,length)!=length)throw new IOException("SMBIOS 읽기 실패");
   digests["SMBIOS 펌웨어"]=Convert.ToHexStringLower(SHA256.HashData(buffer));Array.Clear(buffer);
  }catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){result.Coverage.Add(new("hardware","partial","펌웨어 정보를 읽지 못했습니다.",0));}
  try {
   using var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
   if(key?.GetValue("MachineGuid") is string value&&!string.IsNullOrEmpty(value))digests["Windows 설치 식별값"]=Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
   else result.Coverage.Add(new("hardware","partial","Windows 설치 식별값 없음",0));
  }catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){result.Coverage.Add(new("hardware","denied","Windows 식별값 읽기 권한 없음",0));}
  ct.ThrowIfCancellationRequested();
  try {
   using var adapters=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}");
   int inspected=0;
   if(adapters==null)result.Coverage.Add(new("network-override","unsupported","네트워크 장치 설정 없음",0));
   else {
    foreach(var name in adapters.GetSubKeyNames().Where(n=>System.Text.RegularExpressions.Regex.IsMatch(n,"^[0-9]{4}$")).Take(512)) {
     ct.ThrowIfCancellationRequested();using var key=adapters.OpenSubKey(name);inspected++;
     if(key?.GetValue("NetworkAddress") is string address&&address.Trim().Length>0) {
      const string note="네트워크 드라이버의 NetworkAddress 수동 재정의 설정이 있습니다. 관리·가상화 용도로도 사용되며 실제 적용·치트·스푸핑 도구 사용은 미확인입니다. 주소 원문은 수집하지 않습니다.";
      result.Findings.Add(new("network-address-override",null,"review","네트워크 주소 재정의 설정",[note],"https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ndis/nf-ndis-ndisreadnetworkaddress"));
      result.Artifacts.Add(new("hardware","네트워크 장치 설정 "+name,DateTimeOffset.UtcNow.ToString("O"),null,"none",note));
     }
    }
    result.Coverage.Add(new("network-override","partial","드라이버의 현재 주소 재정의 설정만 확인. 과거 값·실제 장치 적용 여부·커널 변경 미검사.",inspected));
   }
  }catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){result.Coverage.Add(new("network-override","denied","네트워크 장치 설정 읽기 실패",0));}
  try {
   var current=new HardwareBaseline(1,DateTimeOffset.UtcNow.ToString("O"),digests);
   if(File.Exists(baselinePath)) {
    if(new FileInfo(baselinePath).Length>16384)throw new InvalidDataException("기준 파일 크기 오류");
    var previous=JsonSerializer.Deserialize<HardwareBaseline>(File.ReadAllText(baselinePath),Json.Options);
    if(previous?.Version!=1||previous.Digests==null||previous.Digests.Count>8||!DateTimeOffset.TryParse(previous.Captured,out _)||previous.Digests.Any(p=>p.Key.Length>80||p.Value==null||!System.Text.RegularExpressions.Regex.IsMatch(p.Value,"^[a-f0-9]{64}$")))throw new InvalidDataException("기준 파일 형식 오류");
    foreach(var changed in Compare(previous,current)) {
     string note=$"{changed}: 이 앱의 {previous.Captured} 기준과 현재 값이 다릅니다. 부품·BIOS·Windows 변경도 원인이 될 수 있으며 스푸핑 확정이 아닙니다.";
     result.Findings.Add(new("hardware-baseline-change",null,"review","하드웨어·시스템 식별정보 변경",[note,"원본 식별값·해시는 서버에 전송하지 않습니다. 로컬 기준 파일은 변조될 수 있습니다."],Source));
     result.Artifacts.Add(new("hardware",changed,current.Captured,null,"none",note));
    }
    result.Coverage.Add(new("hardware","partial","로컬 기준과 비교. 기록 이전 변경·스푸핑 시도·커널 변조 여부는 확인하지 못합니다.",digests.Count));
   }else if(digests.Count>0) {
    Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
    using var stream=new FileStream(baselinePath,FileMode.CreateNew,FileAccess.Write,FileShare.None);
    JsonSerializer.Serialize(stream,current,Json.Options);
    result.Coverage.Add(new("hardware","partial","첫 검사: 로컬 해시 기준 생성. 과거 비교 기준이 없어 이전 변경은 알 수 없습니다.",digests.Count));
   }
  }catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException){result.Coverage.Add(new("hardware","partial","로컬 기준을 읽거나 저장하지 못했습니다. 변경 여부 미확인.",0));}
  try {
   var info=new IntegrityInfo{Length=8};int status=NtQuerySystemInformation(103,ref info,8,out _);
   if(status!=0)result.Coverage.Add(new("boot-integrity","unsupported","코드 무결성 상태를 조회하지 못했습니다.",0));
   else {
    if((info.Options&2)!=0) {
     const string note="Windows 테스트 서명 모드가 활성화되어 있습니다. 드라이버 개발에도 사용되는 설정으로 스푸핑 사용의 증거는 아닙니다.";
     result.Findings.Add(new("windows-test-signing",null,"review","테스트 서명 드라이버 허용 상태",[note],"https://learn.microsoft.com/en-us/windows-hardware/drivers/install/the-testsigning-boot-configuration-option"));
     result.Artifacts.Add(new("boot","코드 무결성 정책",DateTimeOffset.UtcNow.ToString("O"),null,"none",note));
    }
    result.Coverage.Add(new("boot-integrity","partial","현재 코드 무결성 정책만 조회. 드라이버 메모리·과거 부팅 설정·스푸핑 도구 실행은 별도 미검사.",1));
   }
  }catch(Exception e)when(e is DllNotFoundException or EntryPointNotFoundException){result.Coverage.Add(new("boot-integrity","unsupported","이 Windows의 무결성 조회 API 미지원",0));}
  return result;
 }
 [StructLayout(LayoutKind.Sequential)]private struct IntegrityInfo{public uint Length;public uint Options;}
 [DllImport("kernel32.dll",SetLastError=true)]private static extern uint GetSystemFirmwareTable(uint provider,uint id,byte[]? buffer,uint length);
 [DllImport("ntdll.dll")]private static extern int NtQuerySystemInformation(int type,ref IntegrityInfo info,uint length,out uint returned);
}
