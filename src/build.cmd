@echo off
setlocal
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "WPF=%FW%\WPF"

"%FW%\csc.exe" /nologo /target:winexe /platform:anycpu /optimize+ /out:"%~dp0..\DSH_Awakening.exe" /win32manifest:"%~dp0app.manifest" /win32icon:"%~dp0..\dsh.ico" /r:"%WPF%\PresentationFramework.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\WindowsBase.dll" /r:"%FW%\System.Xaml.dll" /r:"%FW%\System.Web.Extensions.dll" "%~dp0Native.cs" "%~dp0Util.cs" "%~dp0Config.cs" "%~dp0DshTarget.cs" "%~dp0DshExeResolver.cs" "%~dp0PlayerWindow.cs" "%~dp0Program.cs"

if errorlevel 1 (
  echo [BUILD FAILED]
  exit /b 1
)
echo [BUILD OK] %~dp0..\DSH_Awakening.exe
