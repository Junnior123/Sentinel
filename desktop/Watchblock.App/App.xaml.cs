using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using Watchblock.Core;
using Watchblock.Windows;
namespace Watchblock.App;
public partial class App:Application {
 private void ButtonEnter(object sender,System.Windows.Input.MouseEventArgs e)=>MoveButton(sender,-2);
 private void ButtonLeave(object sender,System.Windows.Input.MouseEventArgs e)=>MoveButton(sender,0);
 private static void MoveButton(object sender,double offset){
  if(sender is not System.Windows.Controls.Button button||!SystemParameters.ClientAreaAnimation)return;
  if(button.RenderTransform is not System.Windows.Media.TranslateTransform)button.RenderTransform=new System.Windows.Media.TranslateTransform();
  ((System.Windows.Media.TranslateTransform)button.RenderTransform).BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
   new System.Windows.Media.Animation.DoubleAnimation(offset,TimeSpan.FromMilliseconds(140)){EasingFunction=new System.Windows.Media.Animation.CubicEase{EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut}});
 }
 private Mutex? installationGuard;
 protected override async void OnStartup(StartupEventArgs e){base.OnStartup(e);if(e.Args.Length==2&&e.Args[0]=="--trace-pipe"&&Guid.TryParse(e.Args[1],out _)){
  ShutdownMode=ShutdownMode.OnExplicitShutdown;try{using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(90));await using var pipe=new NamedPipeClientStream(".","watchblock-"+e.Args[1],PipeDirection.InOut,PipeOptions.Asynchronous);await pipe.ConnectAsync(10000,ct.Token);using var reader=new StreamReader(pipe,leaveOpen:true);using var writer=new StreamWriter(pipe,leaveOpen:true){AutoFlush=true};var line=await reader.ReadLineAsync(ct.Token);if(line is null||line.Length>8*1024*1024)throw new InvalidDataException();var request=JsonSerializer.Deserialize<TraceRequest>(line,Json.Options)??throw new InvalidDataException();if(request.Paths.Count>50000||request.Roots.Length>10||request.Roots.Any(p=>!Path.IsPathFullyQualified(p)))throw new InvalidDataException();var result=await Task.Run(()=>Traces.Collect(request.Paths,ct.Token,request.Roots,request.DeletedHistory,request.Scope));await writer.WriteLineAsync(JsonSerializer.Serialize(result,Json.Options));}catch{Environment.ExitCode=1;}finally{Shutdown();}return;
 }try{installationGuard=new Mutex(false,"Sentinel.Desktop.Running");}catch(UnauthorizedAccessException){MessageBox.Show("설치 상태를 확인할 수 없습니다. 앱을 다시 실행해 주세요.","Sentinel");Shutdown(1);return;}Exit+=(_,_)=>installationGuard?.Dispose();new MainWindow().Show();}
}

