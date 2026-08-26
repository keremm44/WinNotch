$proc = Get-Process WinNotch -ErrorAction SilentlyContinue
if (-not $proc) { Write-Host "NOT RUNNING"; exit 1 }
Write-Host "PID: $($proc.Id)"

Add-Type @"
using System;using System.Runtime.InteropServices;using System.Text;
public class WinCheck {
 [DllImport("user32.dll")]public static extern bool EnumWindows(EnumWinDelegate d,IntPtr p);
 [DllImport("user32.dll")]public static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll")]public static extern int GetWindowTextLength(IntPtr h);
 [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out WinRect r);
 [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")]public static extern int GetWindowLong(IntPtr h,int i);
 public delegate bool EnumWinDelegate(IntPtr h,IntPtr p);
}
[StructLayout(LayoutKind.Sequential)]public struct WinRect{public int L,T,R,B;}
"@

$pids = @($proc.Id)
[WinCheck]::EnumWindows({
 param($h,$p)
 $wpid=[uint32]0
 [WinCheck]::GetWindowThreadProcessId($h,[ref]$wpid)|Out-Null
 if($pids -contains[int]$wpid){
  $len=[WinCheck]::GetWindowTextLength($h)
  $t=""
  if($len -gt 0){$sb=New-Object System.Text.StringBuilder($len+1);[WinCheck]::GetWindowText($h,$sb,$sb.Capacity)|Out-Null;$t=$sb.ToString()}
  $v=[WinCheck]::IsWindowVisible($h)
  $r=New-Object WinRect;[WinCheck]::GetWindowRect($h,[ref]$r)|Out-Null
  $w=$r.R-$r.L;$hh=$r.B-$r.T
  $ex=[WinCheck]::GetWindowLong($h,-20)
  Write-Host "HWND=$h Visible=$v Title='$t' Pos=X$($r.L),Y$($r.T) Size=${w}x${hh} ExStyle=0x$($ex.ToString('X8'))"
 }
 return $true
},[IntPtr]::Zero)|Out-Null
