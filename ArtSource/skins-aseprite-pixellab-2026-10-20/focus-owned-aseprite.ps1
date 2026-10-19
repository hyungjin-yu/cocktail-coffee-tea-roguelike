param([int]$ProcessId=44304,[long]$WindowHandle=393872)
if($ProcessId -notin @(6376,44304)){throw 'Not a worker-owned Aseprite PID'}
$p=Get-Process -Id $ProcessId -ErrorAction Stop
if($p.Path -ne 'C:\Program Files\Aseprite\Aseprite.exe'){throw 'Unexpected executable'}
Add-Type @'
using System;using System.Runtime.InteropServices;public class SkinArtFocus{
[DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
[DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
[DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
[DllImport("user32.dll")]static extern bool AttachThreadInput(uint a,uint b,bool x);
[DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int c);
[DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")]static extern bool BringWindowToTop(IntPtr h);
public static void Focus(long hwnd,uint expected){var h=new IntPtr(hwnd);uint p;
GetWindowThreadProcessId(h,out p);if(p!=expected)throw new Exception("PID/HWND mismatch");
var f=GetWindowThreadProcessId(GetForegroundWindow(),out p);var c=GetCurrentThreadId();
try{AttachThreadInput(c,f,true);ShowWindow(h,9);BringWindowToTop(h);
Console.WriteLine("SetForeground="+SetForegroundWindow(h)+" immediate="+GetForegroundWindow());}
finally{AttachThreadInput(c,f,false);}}
}
'@
[SkinArtFocus]::Focus($WindowHandle,$ProcessId)
