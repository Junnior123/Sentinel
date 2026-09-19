using System.Net;
using System.Text.Json;
using Watchblock.App;
internal static class ApiClientChecks {
 private sealed class Handler(Func<HttpRequestMessage,int,HttpResponseMessage> respond):HttpMessageHandler {
  public int Count;public List<string> Bodies=[];
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Bodies.Add(request.Content==null?"":await request.Content.ReadAsStringAsync(ct));return respond(request,++Count);}
 }
 public static async Task Run(Action<bool,string> check){
  static HttpResponseMessage Reply(HttpStatusCode status,string body="{}")=>new(status){Content=new StringContent(body)};
  var retry=new Handler((r,n)=>{check(r.Headers.Authorization?.Parameter=="session-token","upload retry retains authorization");var response=Reply(n<3?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK);response.Headers.RetryAfter=new(TimeSpan.Zero);return response;});
  using(var client=new ApiClient("https://service.test",retry)){client.Token="session-token";await client.Send<JsonElement>("PUT","/api/player/id/chunks/0",new{files=new[]{"same payload"}});check(retry.Count==3&&retry.Bodies.Distinct().Count()==1,"transient upload retry preserves exact body");}
  var claim=new Handler((_,_)=>Reply(HttpStatusCode.ServiceUnavailable));
  using(var client=new ApiClient("https://service.test",claim)){try{await client.Send<JsonElement>("POST","/api/claim",new{code="123"});throw new Exception("Claim should fail");}catch(InvalidOperationException){check(claim.Count==1,"one-use claim is never retried automatically");}}
  var limit=new Handler((_,_)=>{var response=Reply(HttpStatusCode.TooManyRequests);response.Headers.RetryAfter=new(TimeSpan.FromSeconds(60));return response;});
  using(var client=new ApiClient("https://service.test",limit)){try{await client.Send<JsonElement>("GET","/api/health");throw new Exception("Limit should fail");}catch(InvalidOperationException){check(limit.Count==1,"long rate-limit wait does not cause immediate retries");}}
  var large=new Handler((_,_)=>Reply(HttpStatusCode.OK,new string('x',1024*1024+1)));
  using(var client=new ApiClient("https://service.test",large)){try{await client.Send<JsonElement>("GET","/api/health");throw new Exception("Oversize should fail");}catch(InvalidOperationException){check(true,"oversized server response blocked");}}
  var redirect=new Handler((_,_)=>Reply(HttpStatusCode.Redirect));
  using(var client=new ApiClient("https://service.test",redirect)){try{await client.Send<JsonElement>("GET","/api/health");throw new Exception("Redirect should fail");}catch(InvalidOperationException){check(redirect.Count==1,"redirect response is not retried");}try{await client.Send<JsonElement>("GET","https://evil.test/api/health");throw new Exception("External URL should fail");}catch(ArgumentException){check(redirect.Count==1,"bearer requests cannot escape configured service");}}
  using(var client=new ApiClient("https://service.test",new Handler((_,_)=>Reply(HttpStatusCode.OK)))){using var ct=new CancellationTokenSource();ct.Cancel();try{await client.Send<JsonElement>("GET","/api/health",ct:ct.Token);throw new Exception("Cancellation should fail");}catch(OperationCanceledException){check(true,"request cancellation preserved");}}
 }
}
