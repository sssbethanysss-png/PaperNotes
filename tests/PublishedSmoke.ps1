param(
    [string]$ExePath = '',
    [string]$DataDir = ''
)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class SmokeNative {
 public delegate bool Callback(IntPtr hwnd, IntPtr lParam);
 [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback, IntPtr param);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int length);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wp, IntPtr lp);
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string klass, string title);
 // Notes.WindowTitle for ja / zh / en — exact match, scoped to target process.
 static readonly string[] IndexTitles = {
   "PaperNotes \u00b7 \u4ed8\u7b8b\u4e00\u89a7",
   "\u7eb8\u95f4 \u00b7 \u4fbf\u7b7e\u7d22\u5f15",
   "PaperNotes \u00b7 Notes"
 };
 public static bool IsIndexTitle(string text) {
   for (int i = 0; i < IndexTitles.Length; i++)
     if (string.Equals(text, IndexTitles[i], StringComparison.Ordinal)) return true;
   return false;
 }
 public static IntPtr Index(int process) {
   IntPtr found=IntPtr.Zero;
   EnumWindows((h,p)=> {
     uint id; GetWindowThreadProcessId(h,out id);
     if(id==(uint)process) {
       var text=new StringBuilder(256); GetWindowText(h,text,256);
       if(IsIndexTitle(text.ToString())) found=h;
     }
     return true;
   }, IntPtr.Zero);
   return found;
 }
 public static IntPtr Hotkey(int process) {
   IntPtr found=IntPtr.Zero;
   EnumWindows((h,p)=> { uint id; GetWindowThreadProcessId(h,out id); if(id==(uint)process) { var text=new StringBuilder(256); GetWindowText(h,text,256); if(text.ToString()=="PaperNotes.Hotkeys") found=h; } return true; }, IntPtr.Zero);
   if(found!=IntPtr.Zero) return found;
   IntPtr next=IntPtr.Zero;
   while((next=FindWindowEx(new IntPtr(-3), next, null, "PaperNotes.Hotkeys"))!=IntPtr.Zero) { uint id; GetWindowThreadProcessId(next,out id); if(id==(uint)process) return next; }
   return IntPtr.Zero;
 }
}
'@
$root = (Get-Location).Path
if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $ExePath = Join-Path $root 'outputs\PaperNotes\PaperNotes.exe'
}
if (!(Test-Path -LiteralPath $ExePath)) { throw "Published executable not found: $ExePath" }
if ([string]::IsNullOrWhiteSpace($DataDir)) {
    $DataDir = Join-Path $root ('work\published-' + [Guid]::NewGuid().ToString('N'))
}
$exe = $ExePath
$data = $DataDir
$first = Start-Process -FilePath $exe -ArgumentList '--data-dir', ('"' + $data + '"') -PassThru -WindowStyle Hidden
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(12)
    $handle = [IntPtr]::Zero
    while ([DateTime]::UtcNow -lt $deadline -and $handle -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 150; $handle = [SmokeNative]::Index($first.Id) }
    if ($handle -eq [IntPtr]::Zero) { throw 'Published app did not create index' }
    Write-Output 'PASS published executable creates native index'
    [SmokeNative]::SendMessage($handle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 200
    if ($first.HasExited -or [SmokeNative]::IsWindowVisible($handle)) { throw 'Closing index did not keep hidden process alive' }
    Write-Output 'PASS published index close keeps tray process alive'
    $second = Start-Process -FilePath $exe -ArgumentList '--data-dir', ('"' + $data + '"') -PassThru -WindowStyle Hidden
    if (!$second.WaitForExit(6000)) { $second.Kill(); throw 'Second process did not exit' }
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    while (![SmokeNative]::IsWindowVisible($handle) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100; $handle = [SmokeNative]::Index($first.Id) }
    if (![SmokeNative]::IsWindowVisible($handle)) { throw 'Second launch did not restore index' }
    Write-Output 'PASS second executable instance exits and restores existing index'
    $hotkey = [SmokeNative]::Hotkey($first.Id)
    [uint32]$owner = 0
    [SmokeNative]::GetWindowThreadProcessId($hotkey, [ref]$owner) | Out-Null
    if ($owner -ne $first.Id) { throw 'Hotkey window owner mismatch' }
    [SmokeNative]::SendMessage($hotkey, 0x0312, [IntPtr]1, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 700
    $notes = @(Get-ChildItem -LiteralPath (Join-Path $data 'notes') -Filter '*.json')
    if ($notes.Count -ne 1) { throw 'New note command failed' }
    Write-Output 'PASS published native hotkey dispatch creates durable note'
} finally {
    if (!$first.HasExited) { $first.Kill(); $first.WaitForExit() }
}
