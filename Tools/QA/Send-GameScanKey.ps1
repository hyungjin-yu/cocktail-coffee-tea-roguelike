param([Parameter(Mandatory=$true)][int]$GamePid, [Parameter(Mandatory=$true)][string]$Key, [int]$HoldMs=150)
$ErrorActionPreference='Stop'
if($HoldMs -lt 100 -or $HoldMs -gt 500) {throw 'Hold must be 100..500ms'}
$game=Get-Process -Id $GamePid
if($game.ProcessName -ne 'CocktailCoffeeTea' -or $game.MainWindowHandle -eq 0) {throw 'Target must be the visible isolated test game'}
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class CctQaScanInput {
 [StructLayout(LayoutKind.Sequential)] public struct Mouse { public int x,y; public uint data,flags,time; public UIntPtr extra; }
 [StructLayout(LayoutKind.Sequential)] public struct KeyInput { public ushort vk,scan; public uint flags,time; public UIntPtr extra; }
 [StructLayout(LayoutKind.Explicit)] public struct Union { [FieldOffset(0)] public Mouse mouse; [FieldOffset(0)] public KeyInput key; }
 [StructLayout(LayoutKind.Sequential)] public struct Input { public uint type; public Union data; }
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code,uint type);
 [DllImport("user32.dll",SetLastError=true)] public static extern uint SendInput(uint count,Input[] inputs,int size);
 public static uint Send(ushort scan,bool extended,bool up) {
  var i=new Input {type=1}; i.data.key.scan=scan; i.data.key.flags=8u|(extended?1u:0u)|(up?2u:0u);
  return SendInput(1,new[]{i},Marshal.SizeOf(typeof(Input)));
 }
}
'@
$codes=@{Return=13;Enter=13;Escape=27;Tab=9;Space=32;Left=37;Up=38;Right=39;Down=40;W=87;A=65;S=83;D=68;K=75;R=82;One=49;Two=50;Three=51}
if(-not $codes.ContainsKey($Key)) {throw "Unsupported diagnostic key $Key"}
$vk=[uint32]$codes[$Key]
$scan=[uint16][CctQaScanInput]::MapVirtualKey($vk,0)
$extended=$vk -ge 37 -and $vk -le 40
# 2026-10-09: 다른 앱(Orca 등)이 포그라운드 잠금을 쥐고 있으면 SetForegroundWindow가 거부된다. Windows 문서화된 우회(직전 키 입력)로
# Alt를 한 번 눌렀다 떼어 전환을 허용받는다. 아래 포그라운드 PID 검사는 그대로라 게임이 실제 앞에 오지 않으면 입력하지 않는다.
[CctQaScanInput]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [CctQaScanInput]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
[void][CctQaScanInput]::ShowWindow($game.MainWindowHandle, 9)
[void][CctQaScanInput]::SetForegroundWindow($game.MainWindowHandle)
Start-Sleep -Milliseconds 100
[uint32]$foregroundPid=0
$foreground=[CctQaScanInput]::GetForegroundWindow()
[void][CctQaScanInput]::GetWindowThreadProcessId($foreground,[ref]$foregroundPid)
if($foregroundPid -ne $GamePid) {throw "Foreground target mismatch: expected=$GamePid actual=$foregroundPid; no input sent"}
$down=0; $up=0
try {
 $down=[CctQaScanInput]::Send($scan,$extended,$false)
 if($down -ne 1) {throw 'Native key-down was not delivered'}
 Start-Sleep -Milliseconds $HoldMs
} finally { $up=[CctQaScanInput]::Send($scan,$extended,$true) }
if($up -ne 1) {throw 'Native key-up failed'}
[ordered]@{utc=(Get-Date).ToUniversalTime().ToString('o');pid=$GamePid;window=$game.MainWindowHandle.ToInt64();foregroundPid=$foregroundPid;key=$Key;scan=$scan;extended=$extended;holdMs=$HoldMs;downSent=$down;upSent=$up;transport='Win32 SendInput KEYEVENTF_SCANCODE; OS input only';effect='Requires separate actual screen verification'} | ConvertTo-Json -Compress
