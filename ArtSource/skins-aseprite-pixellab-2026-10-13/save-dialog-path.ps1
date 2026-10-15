param([Parameter(Mandatory=$true)][string]$TargetPath)
$allowed='C:\Users\user\Documents\Projects\CocktailCoffeeTea\ArtSource\skins-aseprite-pixellab-2026-10-13\'
if(-not [IO.Path]::GetFullPath($TargetPath).StartsWith($allowed)){throw 'Outside owned source folder'}
Add-Type -TypeDefinition @'
using System;using System.Text;using System.Runtime.InteropServices;
public class ArtSafeSave {
public delegate bool CB(IntPtr h,IntPtr l);
[DllImport("user32.dll")]static extern bool EnumWindows(CB c,IntPtr l);
[DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr p,CB c,IntPtr l);
[DllImport("user32.dll")]static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
[DllImport("user32.dll")]static extern int GetClassName(IntPtr h,StringBuilder b,int n);
[DllImport("user32.dll")]static extern int GetDlgCtrlID(IntPtr h);
[DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr h);
[DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,string s);
[DllImport("user32.dll",EntryPoint="SendMessageW")]static extern IntPtr Msg(IntPtr h,uint m,IntPtr w,IntPtr l);
[DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int w,int h2,uint f);
public static string SetPath(string path){
IntPtr dlg=IntPtr.Zero,edit=IntPtr.Zero;
EnumWindows((h,l)=>{var t=new StringBuilder(256);GetWindowText(h,t,256);if(t.ToString()=="Save As")dlg=h;return true;},IntPtr.Zero);
if(dlg==IntPtr.Zero)throw new Exception("No Save As dialog");
EnumChildWindows(dlg,(h,l)=>{var c=new StringBuilder(128);GetClassName(h,c,128);if(c.ToString()=="Edit"&&GetDlgCtrlID(h)==1001)edit=h;return true;},IntPtr.Zero);
if(edit==IntPtr.Zero)throw new Exception("No filename edit");
SendMessage(edit,12,IntPtr.Zero,path);
var combo=GetParent(edit);
Msg(combo,273,new IntPtr(1001|(768<<16)),edit);
Msg(GetParent(combo),273,new IntPtr(GetDlgCtrlID(combo)|(5<<16)),combo);
SetWindowPos(dlg,new IntPtr(-1),0,0,0,0,3);
return "Filename set in save dialog "+dlg+"; verify via Orca before Save";
}
}
'@
[ArtSafeSave]::SetPath($TargetPath)
