using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Watchblock.Core;
namespace Watchblock.App;

public partial class MainWindow {
 private int visibleStep;
 private void Theme_Click(object sender,RoutedEventArgs e){var saved=Theme.Apply(!Theme.IsLight,true);ThemeToggle.Content=Theme.IsLight?"☾  다크 모드":"☼  화이트 모드";Theme.TitleBar(this);RefreshFlow();if(!saved)Status.Text="테마를 적용했습니다. 다음 실행을 위한 설정 저장은 실패했습니다.";}
 private void InitializeFlow() {
  ExclusionList.Text=string.Join("\n",ScanPreferences.Recommended(SystemRoot).ExcludedPaths.Distinct(StringComparer.OrdinalIgnoreCase))+"\nUsers/*/AppData/Local/Temp (각 사용자 임시 폴더)";
  try {
   var path=Path.Combine(AppContext.BaseDirectory,"Sentinel.service.json");if(!File.Exists(path))path=Path.Combine(AppContext.BaseDirectory,"Watchblock.service.json");
   if(File.Exists(path)) {
    if(new FileInfo(path).Length>4096)throw new InvalidDataException("서버 설정 파일이 너무 큽니다.");
    using var doc=System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
    Server.Text=Invitation.Parse("000000000000",doc.RootElement.GetProperty("origin").GetString()??"").Origin;
    InvitationPreview.Text="연결할 서버: "+Server.Text;
    WelcomeSubtitle.Text="운영자에게 받은 12자리 검사 코드를 입력하세요.";
    CodeLabel.Text="검사 코드";
   }
  }catch(Exception ex){Status.Text="서버 설정을 읽지 못했습니다. 초대 링크를 사용하세요. "+ex.Message;}
  RefreshFlow();
 }
 private void RefreshFlow() {
  if(WelcomePane==null)return;
  int step=working?3:report!=null?4:claim!=null?2:1;
  if(step==2)ScopeDescription.Text=$"{Roots.Items.Count}개 위치 · "+(claim!.Session.Scope.AllFiles?"모든 확장자 · 숨김 파일 포함":"모드·실행 파일·리소스팩");
  WelcomePane.Visibility=step==1?Visibility.Visible:Visibility.Collapsed;
  ConsentPane.Visibility=step==2?Visibility.Visible:Visibility.Collapsed;
  Setup.Visibility=step<=2?Visibility.Visible:Visibility.Collapsed;
  ProgressPane.Visibility=step==3?Visibility.Visible:Visibility.Collapsed;
  ResultPane.Visibility=step==4?Visibility.Visible:Visibility.Collapsed;
  ScanActivity.IsIndeterminate=working;
  ScanActivity.Visibility=working?Visibility.Visible:Visibility.Collapsed;
  ScanButton.Visibility=step==2&&!terminal?Visibility.Visible:Visibility.Collapsed;
  ScanButton.IsEnabled=step==2&&!terminal&&Consent.IsChecked==true&&Roots.Items.Count>0;
  SubmitButton.Visibility=step==4&&!terminal?Visibility.Visible:Visibility.Collapsed;
  CancelButton.Visibility=working&&cts!=null?Visibility.Visible:Visibility.Collapsed;
  ExportButton.Visibility=step==4?Visibility.Visible:Visibility.Collapsed;
  DeclineButton.Visibility=claim!=null&&!working&&!terminal?Visibility.Visible:Visibility.Collapsed;
  DeleteButton.Visibility=DeleteButton.IsEnabled?Visibility.Visible:Visibility.Collapsed;
  int activeStep=working&&claim==null?1:step;var steps=new[]{Step1,Step2,Step3,Step4};
  var frames=new[]{StepFrame1,StepFrame2,StepFrame3,StepFrame4};
  for(int i=0;i<frames.Length;i++){
   var target=((SolidColorBrush)FindResource(i+1==activeStep?"Selected":"Panel")).Color;
   var brush=frames[i].Background as SolidColorBrush;var previous=brush?.Color??((SolidColorBrush)FindResource("Panel")).Color;
   brush=new SolidColorBrush(target);frames[i].Background=brush;
   if(step!=visibleStep&&SystemParameters.ClientAreaAnimation)brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(previous,target,TimeSpan.FromMilliseconds(200)));
  }
  for(int i=0;i<steps.Length;i++){steps[i].SetResourceReference(TextBlock.ForegroundProperty,i+1==activeStep?"Ink":i+1<activeStep?"Muted":"Subtle");steps[i].FontWeight=i+1==activeStep?FontWeights.Bold:FontWeights.Normal;}
  if(step!=visibleStep&&SystemParameters.ClientAreaAnimation){FlowContent.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(260)));var move=new TranslateTransform();FlowContent.RenderTransform=move;move.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(10,0,TimeSpan.FromMilliseconds(260)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});}
  visibleStep=step;
 }
 private void Scope_Changed(object sender,RoutedEventArgs e)=>RefreshFlow();
 private void AddDrives_Click(object sender,RoutedEventArgs e) {
  try {
   if(claim?.Session.Scope.AllFiles!=true)throw new InvalidOperationException("운영자가 전체 파일 검사를 선택한 새 초대가 필요합니다.");
   Roots.Items.Clear();
   foreach(var drive in DriveInfo.GetDrives().Where(d=>d.DriveType==DriveType.Fixed)) {
    try{if(drive.IsReady&&Roots.Items.Count<10)Roots.Items.Add(drive.RootDirectory.FullName);}catch(IOException){}
   }
   Consent.IsChecked=false;Status.Text="선택한 드라이브의 모든 확장자 파일 목록·해시가 검사 대상입니다. 범위를 확인하고 동의해 주세요.";RefreshFlow();
  }catch(Exception ex){Status.Text=ex.Message;}
 }
 private void Invitation_Changed(object sender,TextChangedEventArgs e) {
  if(InvitationPreview==null||Server==null||Code==null)return;
  try {var invite=Invitation.Parse(Code.Text,Server.Text);InvitationPreview.Text="연결할 서버: "+invite.Origin;}
  catch {InvitationPreview.Text=string.IsNullOrWhiteSpace(Server.Text)?"초대 링크에 검사 서버가 포함되어 있습니다.":"설정된 서버: "+Server.Text;}
 }
 public void LoadInvitation(string text) {
  if(working||claim!=null)throw new InvalidOperationException("진행 중인 검사가 있습니다. 새 검사는 앱을 다시 열어 연결하세요.");
  var invite=Invitation.Parse(text,Server.Text);
  Code.Text=invite.Origin+"/#scan="+invite.Code;
  Status.Text="연결할 서버를 확인하고 계속해 주세요.";
 }
 private void ImportInvitation_Click(object sender,RoutedEventArgs e) {
  try {var picker=new OpenFileDialog{Filter="Watchblock 검사 초대|*.watchblock.json|JSON 파일|*.json",Title="운영자에게 받은 검사 초대"};if(picker.ShowDialog(this)!=true)return;if(new FileInfo(picker.FileName).Length>4096)throw new InvalidDataException("초대 파일이 너무 큽니다.");LoadInvitation(File.ReadAllText(picker.FileName));}
  catch(Exception ex){Status.Text=ex.Message;}
 }
}
