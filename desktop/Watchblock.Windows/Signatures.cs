using System.Runtime.InteropServices;
namespace Watchblock.Windows;
public static class Signatures {
 // WinVerifyTrust verifies the embedded Authenticode signature, including trust; catalog signatures are not covered.
 public static string Verify(string path){IntPtr file=IntPtr.Zero;try{var f=new WINTRUST_FILE_INFO{cbStruct=(uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),pcwszFilePath=path};file=Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());Marshal.StructureToPtr(f,file,false);var data=new WINTRUST_DATA{cbStruct=(uint)Marshal.SizeOf<WINTRUST_DATA>(),dwUIChoice=2,fdwRevocationChecks=0,dwUnionChoice=1,pFile=file,dwStateAction=0,dwProvFlags=0x1000};Guid action=new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");int result=WinVerifyTrust(IntPtr.Zero,ref action,ref data);return result==0?"내장 서명 신뢰 확인 (온라인 폐기 확인 제외)":result==unchecked((int)0x800B0100)?"내장 서명 없음 (카탈로그 미검사)":"서명 신뢰 확인 실패";}catch{return "서명 검사 실패";}finally{if(file!=IntPtr.Zero){Marshal.DestroyStructure<WINTRUST_FILE_INFO>(file);Marshal.FreeHGlobal(file);}}}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct WINTRUST_FILE_INFO{public uint cbStruct;[MarshalAs(UnmanagedType.LPWStr)]public string pcwszFilePath;public IntPtr hFile;public IntPtr pgKnownSubject;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct WINTRUST_DATA{public uint cbStruct;public IntPtr pPolicyCallbackData,pSIPClientData;public uint dwUIChoice,fdwRevocationChecks,dwUnionChoice;public IntPtr pFile;public uint dwStateAction;public IntPtr hWVTStateData,pwszURLReference;public uint dwProvFlags,dwUIContext;}
 [DllImport("wintrust.dll",ExactSpelling=true)]private static extern int WinVerifyTrust(IntPtr hwnd,ref Guid action,ref WINTRUST_DATA data);
}
