@echo off
chcp 65001 >nul
title 볼링 영상 편집기 설치

echo ============================================
echo   볼링 영상 편집기 v1.0.0 설치
echo ============================================
echo.

set "INSTALL_DIR=%LOCALAPPDATA%\BowlingVideoEditor"

echo 설치 경로: %INSTALL_DIR%
echo.

:: 설치 디렉토리 생성
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

:: 파일 복사
echo 파일 설치 중...
xcopy /s /y /q "%~dp0*.*" "%INSTALL_DIR%\" >nul 2>&1
del "%INSTALL_DIR%\setup_script.bat" >nul 2>&1

:: 바탕화면 바로가기
echo 바로가기 생성 중...
powershell -Command "$ws = New-Object -ComObject WScript.Shell; $sc = $ws.CreateShortcut([Environment]::GetFolderPath('Desktop') + '\볼링 영상 편집기.lnk'); $sc.TargetPath = '%INSTALL_DIR%\BowlingVideoEditor.exe'; $sc.WorkingDirectory = '%INSTALL_DIR%'; $sc.IconLocation = '%INSTALL_DIR%\BowlingVideoEditor.exe'; $sc.Save()"

:: 시작 메뉴 바로가기
set "START_MENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs\볼링 영상 편집기"
if not exist "%START_MENU%" mkdir "%START_MENU%"
powershell -Command "$ws = New-Object -ComObject WScript.Shell; $sc = $ws.CreateShortcut('%START_MENU%\볼링 영상 편집기.lnk'); $sc.TargetPath = '%INSTALL_DIR%\BowlingVideoEditor.exe'; $sc.WorkingDirectory = '%INSTALL_DIR%'; $sc.Save()"
powershell -Command "$ws = New-Object -ComObject WScript.Shell; $sc = $ws.CreateShortcut('%START_MENU%\볼링 영상 편집기 제거.lnk'); $sc.TargetPath = '%INSTALL_DIR%\uninstall.bat'; $sc.WorkingDirectory = '%INSTALL_DIR%'; $sc.Save()"

:: 제거 프로그램 생성
echo @echo off > "%INSTALL_DIR%\uninstall.bat"
echo chcp 65001 ^>nul >> "%INSTALL_DIR%\uninstall.bat"
echo echo 볼링 영상 편집기를 제거합니다... >> "%INSTALL_DIR%\uninstall.bat"
echo rmdir /s /q "%INSTALL_DIR%" >> "%INSTALL_DIR%\uninstall.bat"
echo del "%USERPROFILE%\Desktop\볼링 영상 편집기.lnk" 2^>nul >> "%INSTALL_DIR%\uninstall.bat"
echo rmdir /s /q "%START_MENU%" 2^>nul >> "%INSTALL_DIR%\uninstall.bat"
echo echo 제거 완료. >> "%INSTALL_DIR%\uninstall.bat"
echo pause >> "%INSTALL_DIR%\uninstall.bat"

:: 프로그램 추가/제거에 등록
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\BowlingVideoEditor" /v "DisplayName" /t REG_SZ /d "볼링 영상 편집기" /f >nul 2>&1
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\BowlingVideoEditor" /v "UninstallString" /t REG_SZ /d "\"%INSTALL_DIR%\uninstall.bat\"" /f >nul 2>&1
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\BowlingVideoEditor" /v "DisplayIcon" /t REG_SZ /d "%INSTALL_DIR%\BowlingVideoEditor.exe" /f >nul 2>&1
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\BowlingVideoEditor" /v "DisplayVersion" /t REG_SZ /d "1.0.0" /f >nul 2>&1
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\BowlingVideoEditor" /v "Publisher" /t REG_SZ /d "BowlingVideoEditor" /f >nul 2>&1
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\BowlingVideoEditor" /v "NoModify" /t REG_DWORD /d 1 /f >nul 2>&1
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\BowlingVideoEditor" /v "NoRepair" /t REG_DWORD /d 1 /f >nul 2>&1

echo.
echo ============================================
echo   설치 완료!
echo ============================================
echo.
echo   바탕화면과 시작 메뉴에 바로가기가 생성되었습니다.
echo   프로그램 추가/제거에서 제거할 수 있습니다.
echo.

set /p RUN="지금 실행하시겠습니까? (Y/N): "
if /i "%RUN%"=="Y" start "" "%INSTALL_DIR%\BowlingVideoEditor.exe"
