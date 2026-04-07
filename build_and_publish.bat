@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ============================================
echo   볼링 영상 편집기 - 빌드 및 GitHub 배포
echo ============================================
echo.

:: ===== 설정 =====
set GITHUB_USER=grasion
set GITHUB_REPO=BowlingVideoEditor
set VERSION=1.0.0
set TAG=v%VERSION%

:: ===== 1) 빌드 =====
echo [1/4] 빌드 중...
cd BowlingVideoEditor
dotnet publish -c Release -o publish
if %errorlevel% neq 0 ( echo 빌드 실패! & pause & exit /b 1 )
cd ..
echo.

:: ===== 2) Inno Setup으로 Setup.exe 생성 =====
echo [2/4] Setup.exe 생성 중...
set "ISCC="
for %%p in ("%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "C:\Program Files\Inno Setup 6\ISCC.exe") do (
    if exist %%p set "ISCC=%%~p"
)
if defined ISCC (
    "%ISCC%" BowlingVideoEditor\installer.iss
    if %errorlevel% equ 0 ( echo Setup.exe 생성 완료! ) else ( echo Setup 생성 실패 )
) else (
    echo Inno Setup 미설치. winget install JRSoftware.InnoSetup 으로 설치하세요.
)
echo.

:: ===== 3) Git 소스코드만 푸시 =====
echo [3/4] Git 소스코드 푸시...
git add .gitignore
git add BowlingVideoEditor\*.csproj
git add BowlingVideoEditor\App.xaml
git add BowlingVideoEditor\App.xaml.cs
git add BowlingVideoEditor\Models\*.cs
git add BowlingVideoEditor\Services\*.cs
git add BowlingVideoEditor\Views\*.xaml
git add BowlingVideoEditor\Views\*.xaml.cs
git add BowlingVideoEditor\Resources\*
git add BowlingVideoEditor\installer.iss
git add BowlingVideoEditor\.gitignore
git add BowlingVideoEditor\LICENSE
git add BowlingVideoEditor\README.md
git add build_and_publish.bat
git commit -m "Release %TAG%"
git tag -a %TAG% -m "Release %TAG%" 2>nul
git push origin main
git push origin %TAG% 2>nul
echo.

:: ===== 4) GitHub Release (설치파일만 첨부) =====
echo [4/4] GitHub Release 생성...
where gh >nul 2>&1
if %errorlevel% equ 0 (
    set "SETUP_FILE="
    if exist "BowlingVideoEditor\installer_output\BowlingVideoEditor_Setup_v%VERSION%.exe" (
        set "SETUP_FILE=BowlingVideoEditor\installer_output\BowlingVideoEditor_Setup_v%VERSION%.exe"
    )
    if exist "BowlingVideoEditor\installer_output2\BowlingVideoEditor_Setup_v%VERSION%.exe" (
        set "SETUP_FILE=BowlingVideoEditor\installer_output2\BowlingVideoEditor_Setup_v%VERSION%.exe"
    )

    if defined SETUP_FILE (
        gh release delete %TAG% --yes 2>nul
        gh release create %TAG% "!SETUP_FILE!" --title "볼링 영상 편집기 %TAG%" --notes "## 볼링 영상 편집기 %TAG%
- BowlingVideoEditor_Setup_v%VERSION%.exe 다운로드 후 실행하여 설치
- FFmpeg 자동 설치 포함"
        echo.
        echo Release: https://github.com/%GITHUB_USER%/%GITHUB_REPO%/releases/tag/%TAG%
    ) else (
        echo Setup.exe 파일을 찾을 수 없습니다.
    )
) else (
    echo gh CLI 미설치. https://cli.github.com/ 에서 설치하세요.
)

echo.
echo ============================================
echo   완료!
echo ============================================
pause
