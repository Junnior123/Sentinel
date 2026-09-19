using System.Text.Json;
using System.Text.RegularExpressions;
namespace Watchblock.Core;

public sealed record Invitation(int SchemaVersion, string Origin, string Code) {
 public static Invitation Parse(string input, string configuredOrigin = "") {
  input = input.Trim();
  if (input.Length > 4096) throw new ArgumentException("검사 초대가 너무 깁니다.");
  string origin = configuredOrigin.Trim(), code = input;
  if (input.StartsWith('{')) {
   var value = JsonSerializer.Deserialize<Invitation>(input, Json.Options) ?? throw new ArgumentException("초대 파일이 비어 있습니다.");
   if (value.SchemaVersion != 1) throw new ArgumentException("지원하지 않는 초대 파일입니다.");
   origin = value.Origin; code = value.Code;
  } else if (Uri.TryCreate(input, UriKind.Absolute, out var link) && link.Fragment.StartsWith("#scan=", StringComparison.Ordinal)) {
   if (link.AbsolutePath != "/" || link.Query != "" || link.UserInfo != "") throw new ArgumentException("올바른 검사 초대 링크가 아닙니다.");
   origin = link.GetLeftPart(UriPartial.Authority); code = link.Fragment[6..];
  }
  if (string.IsNullOrWhiteSpace(origin)) throw new ArgumentException("운영자에게 검사 초대 링크를 받아 붙여넣어 주세요. 코드만 있다면 아래 연결 설정에서 서버를 선택하세요.");
  if (!Uri.TryCreate(origin, UriKind.Absolute, out var server) || server.UserInfo != "" || server.AbsolutePath != "/" || server.Query != "" || server.Fragment != "" || !(server.Scheme == "https" || server.Scheme == "http" && server.IsLoopback)) throw new ArgumentException("검사 서버는 HTTPS 주소여야 합니다.");
  if (code is null || !Regex.IsMatch(code, "^[A-Fa-f0-9]{12}$", RegexOptions.CultureInvariant)) throw new ArgumentException("검사 코드 12자리 또는 운영자가 전달한 초대 링크를 확인해 주세요.");
  return new(1, server.GetLeftPart(UriPartial.Authority), code.ToUpperInvariant());
 }
}
