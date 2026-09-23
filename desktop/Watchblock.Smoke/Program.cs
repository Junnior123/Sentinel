using System.IO;using System.Net.Http;using System.Text;using System.Text.Json;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;using System.Windows.Threading;
class Program {
 [STAThread]static void Main(string[] args){var root=Path.GetFullPath(args[0]);var app=new Watchblock.App.App();app.InitializeComponent();SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());var window=new Watchblock.App.MainWindow();window.Show();_ = Dispatcher.CurrentDispatcher.InvokeAsync(async()=>{try{
  if(args.Contains("--themes")){await CaptureThemes(window,root);return;}
  await Task.Delay(350);Capture(window,Path.Combine(root,"artifacts/app-welcome.png"));
  using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});var login=await http.GetAsync("http://localhost:8787/dev/login");string cookie=login.Headers.GetValues("Set-Cookie").First().Split(';')[0];http.DefaultRequestHeaders.Add("Cookie",cookie);http.DefaultRequestHeaders.Add("Origin","http://localhost:8787");
  var create=await http.PostAsync("http://localhost:8787/api/scans",new StringContent("{\"nickname\":\"로컬 통합 검증\",\"reason\":\"공식 배포 표본 정적 검사\",\"scope\":{\"gameFiles\":true,\"allFiles\":true,\"hardwareChanges\":false,\"extraFolders\":false,\"executionTraces\":false,\"bannedModIds\":[]}}",Encoding.UTF8,"application/json"));create.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await create.Content.ReadAsStringAsync());
  window.LoadInvitation(JsonSerializer.Serialize(new{schemaVersion=1,origin="http://localhost:8787",code=doc.RootElement.GetProperty("code").GetString()}));((Button)window.FindName("ConnectButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>((StackPanel)window.FindName("Options")).IsEnabled,20);
  ((ListBox)window.FindName("Roots")).Items.Clear();((ListBox)window.FindName("Roots")).Items.Add(Path.Combine(root,"samples/private"));if(((Button)window.FindName("ScanButton")).IsEnabled)throw new Exception("Scan enabled before consent");((CheckBox)window.FindName("Consent")).IsChecked=true;await Task.Delay(350);Capture(window,Path.Combine(root,"artifacts/app-consent.png"));((Button)window.FindName("ScanButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>((Button)window.FindName("DeleteButton")).IsEnabled,120);
  if(((DataGrid)window.FindName("Files")).Items.Count!=Directory.GetFiles(Path.Combine(root,"samples/private")).Length)throw new Exception("Expected all local sample files: "+((TextBlock)window.FindName("Status")).Text);
  if(((Button)window.FindName("SubmitButton")).IsEnabled)throw new Exception("Auto-submitted report remains submittable");
  var read=await http.GetStringAsync("http://localhost:8787/api/scans/"+doc.RootElement.GetProperty("id").GetString());using var result=JsonDocument.Parse(read);if(result.RootElement.GetProperty("state").GetString()!="submitted")throw new Exception("Submission incomplete");
  await Task.Delay(450);window.UpdateLayout();var bmp=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var output=File.Create(Path.Combine(root,"artifacts/app-scan.png")))encoder.Save(output);
  window.WindowState=WindowState.Normal;window.Width=980;window.Height=680;await Task.Delay(350);Capture(window,Path.Combine(root,"artifacts/app-minimum.png"));
  File.WriteAllText(Path.Combine(root,"artifacts/app-e2e.json"),read);Console.WriteLine("PASS WPF invitation → consent → scan verified fixtures → automatic submit → API report. Screenshots saved.");
 }catch(Exception e){Console.Error.WriteLine(e);Console.Error.WriteLine(((TextBlock)window.FindName("Status")).Text);Environment.ExitCode=1;}finally{window.Close();app.Shutdown();Dispatcher.CurrentDispatcher.InvokeShutdown();}});Dispatcher.Run();}
 static async Task CaptureThemes(Watchblock.App.MainWindow window,string root){
  var setting=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Sentinel","ui-theme.txt");
  var original=File.Exists(setting)?File.ReadAllBytes(setting):null;
  var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
  var field=typeof(Watchblock.App.MainWindow).GetField("working",flags)!;
  var refresh=typeof(Watchblock.App.MainWindow).GetMethod("RefreshFlow",flags)!;
  var button=(Button)window.FindName("ThemeToggle");
  try{
   foreach(var mode in new[]{"dark","light"}){
    var isLight=button.Content.ToString()!.Contains("다크");if(isLight!=(mode=="light"))button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    var color=((SolidColorBrush)window.Foreground).Color;if((mode=="light")!=(color.R<80))throw new Exception("Theme foreground did not update");
    field.SetValue(window,false);refresh.Invoke(window,null);await Task.Delay(400);Capture(window,Path.Combine(root,"artifacts/app-theme-"+mode+".png"));
    field.SetValue(window,true);refresh.Invoke(window,null);await Task.Delay(400);Capture(window,Path.Combine(root,"artifacts/app-progress-"+mode+".png"));
   }
   field.SetValue(window,false);refresh.Invoke(window,null);
   var reopened=new Watchblock.App.MainWindow();if(!((Button)reopened.FindName("ThemeToggle")).Content.ToString()!.Contains("다크"))throw new Exception("Light preference not persisted");reopened.Close();
   Console.WriteLine("PASS native dark/light switch, readable foreground, stationary progress illustration and persistence; UI fixture screenshots saved");
  }finally{field.SetValue(window,false);if(original!=null)File.WriteAllBytes(setting,original);else if(File.Exists(setting))File.Delete(setting);}
 }
 static async Task Wait(Func<bool> predicate,int seconds){var until=DateTime.UtcNow.AddSeconds(seconds);while(!predicate()){if(DateTime.UtcNow>until)throw new TimeoutException("WPF workflow timed out");await Task.Delay(100);}}
 static void Capture(Window window,string path){window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);}
}
