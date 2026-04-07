# 볼링 영상 편집기 - 자체 설치 프로그램 생성
# Inno Setup 없이 자체 압축 해제 + 바로가기 생성 EXE를 만듭니다.

param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$publishDir = "publish"
$outputDir = "installer_output"

if (-not (Test-Path $publishDir)) {
    Write-Host "publish 폴더가 없습니다. 먼저 dotnet publish를 실행하세요."
    exit 1
}

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

# ZIP 생성
$zipPath = "$outputDir\BowlingVideoEditor_v$Version.zip"
Write-Host "ZIP 생성 중: $zipPath"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force
Write-Host "ZIP 생성 완료: $((Get-Item $zipPath).Length / 1MB) MB"

# 자체 설치 BAT 생성 (ZIP + 설치 스크립트)
$installerBat = @"
@echo off
chcp 65001 >nul
echo ============================================
echo   볼링 영상 편집기 v$Version 설치
echo ============================================
echo.

set INSTALL_DIR=%LOCALAPPDATA%\BowlingVideoEditor

echo 설치 경로: %INSTALL_DIR%
echo.
echo 설치를 계속하시겠습니까?
pause

echo 설치 중...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

:: 자기 자신의 ZIP 부분을 추출
powershell -Command "Expand-Archive -Path '%~dp0BowlingVideoEditor_v$Version.zip' -DestinationPath '%INSTALL_DIR%' -Force"

:: 바탕화면 바로가기
powershell -Command "`$ws = New-Object -ComObject WScript.Shell; `$sc = `$ws.CreateShortcut([Environment]::GetFolderPath('Desktop') + '\볼링 영상 편집기.lnk'); `$sc.TargetPath = '%INSTALL_DIR%\BowlingVideoEditor.exe'; `$sc.WorkingDirectory = '%INSTALL_DIR%'; `$sc.IconLocation = '%INSTALL_DIR%\BowlingVideoEditor.exe'; `$sc.Save()"

:: 시작 메뉴 바로가기
set START_MENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs
powershell -Command "`$ws = New-Object -ComObject WScript.Shell; `$sc = `$ws.CreateShortcut('%START_MENU%\볼링 영상 편집기.lnk'); `$sc.TargetPath = '%INSTALL_DIR%\BowlingVideoEditor.exe'; `$sc.WorkingDirectory = '%INSTALL_DIR%'; `$sc.Save()"

echo.
echo ============================================
echo   설치 완료!
echo   바탕화면에 바로가기가 생성되었습니다.
echo ============================================
echo.

set /p RUN=지금 실행하시겠습니까? (Y/N): 
if /i "%RUN%"=="Y" start "" "%INSTALL_DIR%\BowlingVideoEditor.exe"
"@

$installerPath = "$outputDir\install_v$Version.bat"
$installerBat | Out-File -FilePath $installerPath -Encoding UTF8
Write-Host "설치 BAT 생성: $installerPath"

# 제거 BAT
$uninstallerBat = @"
@echo off
chcp 65001 >nul
echo 볼링 영상 편집기를 제거합니다...
set INSTALL_DIR=%LOCALAPPDATA%\BowlingVideoEditor
if exist "%INSTALL_DIR%" rmdir /s /q "%INSTALL_DIR%"
del "%USERPROFILE%\Desktop\볼링 영상 편집기.lnk" 2>nul
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\볼링 영상 편집기.lnk" 2>nul
echo 제거 완료.
pause
"@

$uninstallerBat | Out-File -FilePath "$outputDir\uninstall.bat" -Encoding UTF8

Write-Host ""
Write-Host "=== 설치 파일 생성 완료 ==="
Write-Host "  ZIP: $zipPath"
Write-Host "  설치: $installerPath"
Write-Host "  제거: $outputDir\uninstall.bat"
