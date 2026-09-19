using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace Watchblock.App;

internal static class Theme {
 public static bool IsLight{get;private set;}
 private static readonly string Settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Sentinel","ui-theme.txt");
 public static void Load(){bool light=false;try{light=File.Exists(Settings)&&new FileInfo(Settings).Length<32&&File.ReadAllText(Settings).Trim()=="light";}catch(IOException){}catch(UnauthorizedAccessException){}Apply(light);}
 public static bool Apply(bool light,bool save=false){
  IsLight=light;
  string[] keys=["Canvas","Panel","Raised","Inset","Line","Ink","Muted","Subtle","Accent","AccentInk","Selected","Danger","Warning"];
  string[] colors=light?["#F4F4F5","#FFFFFF","#ECECEE","#F8F8F9","#D3D3D8","#202023","#56565E","#65656E","#29292E","#FFFFFF","#E4E4E8","#AD3445","#85600E"]:["#141416","#1C1C1F","#27272B","#18181B","#3B3B42","#F1F1F3","#B5B5BE","#9696A0","#E4E4E9","#202024","#34343B","#F29AA6","#E0C18C"];
  for(int i=0;i<keys.Length;i++)Application.Current.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
  Application.Current.Resources[SystemColors.HighlightBrushKey]=Application.Current.Resources["Selected"];
  Application.Current.Resources[SystemColors.HighlightTextBrushKey]=Application.Current.Resources["Ink"];
  Application.Current.Resources[SystemColors.InactiveSelectionHighlightBrushKey]=Application.Current.Resources["Selected"];
  Application.Current.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey]=Application.Current.Resources["Ink"];
  if(save)try{Directory.CreateDirectory(Path.GetDirectoryName(Settings)!);File.WriteAllText(Settings,light?"light":"dark");}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
  return true;
 }
 public static void TitleBar(Window window){try{var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;int dark=IsLight?0:1;DwmSetWindowAttribute(handle,20,ref dark,sizeof(int));}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
 [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
}
