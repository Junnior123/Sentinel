using System.Net.Http;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Watchblock.Core;
namespace Watchblock.App;
public sealed class ApiClient:IDisposable {
 private readonly HttpClient client;public string Origin{get;}public string? Token{get;set;}
 public ApiClient(string origin):this(origin,new HttpClientHandler{AllowAutoRedirect=false}){}
 internal ApiClient(string origin,HttpMessageHandler handler){if(!Uri.TryCreate(origin.Trim(),UriKind.Absolute,out var uri)||uri.UserInfo!=""||uri.AbsolutePath!="/"||uri.Query!=""||uri.Fragment!=""||!(uri.Scheme=="https"||uri.Scheme=="http"&&uri.IsLoopback))throw new ArgumentException("HTTPS 서비스 주소를 입력해 주세요. 로컬 개발만 HTTP를 허용합니다.");Origin=uri.GetLeftPart(UriPartial.Authority);client=new(handler){BaseAddress=new Uri(Origin),Timeout=Timeout.InfiniteTimeSpan};}
 public async Task<T> Send<T>(string method,string path,object? value=null,CancellationToken ct=default){
  if(!path.StartsWith("/api/",StringComparison.Ordinal)||path.Contains('\\')||!Uri.TryCreate(client.BaseAddress,path,out var destination)||destination.GetLeftPart(UriPartial.Authority)!=Origin)throw new ArgumentException("잘못된 API 경로입니다.");
  // Only operations with server-side replay protection may be retried automatically.
  var replayable=method=="GET"||method=="PUT"&&path.Contains("/chunks/",StringComparison.Ordinal)||method=="POST"&&path.EndsWith("/complete",StringComparison.Ordinal);
  var serialized=value==null?null:JsonSerializer.Serialize(value,Json.Options);
  for(var attempt=0;;attempt++){
   ct.ThrowIfCancellationRequested();
   using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(30));
   try{
    using var request=new HttpRequestMessage(new HttpMethod(method),destination);
    if(Token!=null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token);
    if(serialized!=null)request.Content=new StringContent(serialized,Encoding.UTF8,"application/json");
    using var result=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
    var status=(int)result.StatusCode;
    if(replayable&&attempt<2&&status is 429 or 502 or 503 or 504){
     var wait=result.Headers.RetryAfter?.Delta??(result.Headers.RetryAfter?.Date-DateTimeOffset.UtcNow)??TimeSpan.FromSeconds(1<<attempt);
     if(wait<=TimeSpan.FromSeconds(5)){await Task.Delay(wait<TimeSpan.Zero?TimeSpan.Zero:wait,ct);continue;}
    }
    if(result.Content.Headers.ContentLength>1024*1024)throw new InvalidOperationException("서버 응답이 허용 크기를 초과했습니다.");
    await using var stream=await result.Content.ReadAsStreamAsync(timeout.Token);using var buffer=new MemoryStream();var block=new byte[8192];
    int read;while((read=await stream.ReadAsync(block,timeout.Token))>0){if(buffer.Length+read>1024*1024)throw new InvalidOperationException("서버 응답이 허용 크기를 초과했습니다.");buffer.Write(block,0,read);}
    var text=Encoding.UTF8.GetString(buffer.ToArray());
    if(!result.IsSuccessStatusCode){
     var message=status==429?"서버 요청 한도에 도달했습니다. 잠시 기다린 후 다시 제출해 주세요.":status>=500?"서버에 일시적으로 연결할 수 없습니다. 보고서를 보관하고 다시 제출해 주세요.":$"서비스 요청 실패 ({status})";
     try{using var doc=JsonDocument.Parse(text);if(doc.RootElement.TryGetProperty("error",out var error)&&error.ValueKind==JsonValueKind.String)message=error.GetString()??message;}catch(JsonException){}
     throw new InvalidOperationException(message);
    }
    try{return JsonSerializer.Deserialize<T>(text,Json.Options)??throw new InvalidOperationException("서버 응답이 비어 있습니다.");}catch(JsonException ex){throw new InvalidOperationException("서버 응답 형식이 올바르지 않습니다. 서비스 주소를 확인해 주세요.",ex);}
   }catch(Exception ex)when(ex is HttpRequestException or IOException||ex is OperationCanceledException&&!ct.IsCancellationRequested){
    if(replayable&&attempt<2){await Task.Delay(TimeSpan.FromSeconds(1<<attempt),ct);continue;}
    throw new InvalidOperationException(path=="/api/claim"?"연결 응답을 받지 못했습니다. 인터넷을 확인해 주세요. 코드가 이미 사용되었다면 운영자에게 새 코드를 요청하세요.":"연결이 끊어지거나 응답 시간이 초과되었습니다. 인터넷을 확인한 뒤 다시 시도해 주세요.",ex);
   }
  }
 }
 public void Dispose()=>client.Dispose();
}
