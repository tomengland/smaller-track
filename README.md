# Smaller Track

Smaller Track is a Windows utility that widens the palm-rejection zones on the ASUS ROG Zephyrus G16 (GU606AW). It sets the left edge to 10% and the right edge to 20% of the width reported by the ASUS ASUF1209 touchpad. Some touches and clicks near the edges may still register.

## Use Smaller Track

1. Download `SmallerTrack.exe` from the [latest GitHub release](https://github.com/tomengland/smaller-track/releases/latest).
2. Open it, select **Apply**, and approve the Windows administrator prompt.
3. Restart Windows to load the settings.

To undo the change, open the app, select **Restore original settings**, approve the prompt, and restart Windows. The app saves the original edge settings the first time you apply the preset and keeps that backup if you apply the preset again.

The preset is sized for the GU606AW touchpad. Results may vary on other devices. The app writes the `SuperCurtainLeft` and `SuperCurtainRight` values under `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\PrecisionTouchPad`.

## Build from source

From PowerShell on 64-bit Windows, run the .NET Framework C# compiler from the repository directory:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" `
  /nologo /target:winexe /out:SmallerTrack.exe `
  /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
  SmallerTrack.cs
```
