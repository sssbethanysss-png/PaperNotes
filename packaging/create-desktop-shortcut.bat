@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "EXE=%~dp0PaperNotes.exe"
if not exist "%EXE%" (
  echo [错误] 当前目录未找到 PaperNotes.exe
  echo 请把本脚本放在与 PaperNotes.exe 同一发布目录后再运行。
  pause
  exit /b 1
)

rem Strip trailing backslash for COM paths
set "WORKDIR=%~dp0"
if "%WORKDIR:~-1%"=="\" set "WORKDIR=%WORKDIR:~0,-1%"

set "VBS=%TEMP%\PaperNotes-desktop-shortcut-%RANDOM%%RANDOM%.vbs"
(
  echo Set shell = CreateObject("WScript.Shell"^)
  echo desktop = shell.SpecialFolders("Desktop"^)
  echo Set link = shell.CreateShortcut(desktop ^& "\纸间 PaperNotes.lnk"^)
  echo link.TargetPath = "%EXE%"
  echo link.WorkingDirectory = "%WORKDIR%"
  echo link.IconLocation = "%EXE%,0"
  echo link.Description = "纸间 PaperNotes"
  echo link.Save
) > "%VBS%"

cscript //nologo "%VBS%"
set "ERR=%ERRORLEVEL%"
del "%VBS%" >nul 2>&1

if not "%ERR%"=="0" (
  echo [错误] 创建桌面快捷方式失败。
  pause
  exit /b %ERR%
)

echo 已创建桌面快捷方式：纸间 PaperNotes
echo 目标：%EXE%
exit /b 0
