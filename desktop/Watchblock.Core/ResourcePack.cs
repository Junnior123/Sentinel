using System.Security.Cryptography;
namespace Watchblock.Core;
public static class ResourcePack {
 public static async Task<Dictionary<string,string>> FolderHashes(string folder,IEnumerable<string> entries,CancellationToken ct) {
  var result=new Dictionary<string,string>(StringComparer.Ordinal);string root=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  foreach(var name in entries.Distinct(StringComparer.Ordinal)) {
   ct.ThrowIfCancellationRequested();if(name.StartsWith('/')||name.Contains(':')||name.Split('/').Contains(".."))throw new InvalidDataException("잘못된 리소스 규칙 경로");
   var path=Path.GetFullPath(Path.Combine(root,name.Replace('/',Path.DirectorySeparatorChar)));if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("리소스 경로 범위 오류");
   if(!File.Exists(path))continue;
   // Never follow a junction/symlink in any component, including the pack root.
   for(string? current=path;current!=null;current=Path.GetDirectoryName(current)){if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("링크 리소스 제외");if(string.Equals(current.TrimEnd(Path.DirectorySeparatorChar),root.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))break;}
   var info=new FileInfo(path);if(info.Length>16*1024*1024||(info.Attributes&FileAttributes.Offline)!=0)throw new IOException("리소스 파일 검사 한도");var modified=info.LastWriteTimeUtc;
   await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,65536,true);
   string hash=Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,ct));info.Refresh();if(info.LastWriteTimeUtc!=modified)throw new IOException("리소스 검사 중 변경");result[name]=hash;
  }
  return result;
 }
}
