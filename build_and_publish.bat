@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ============================================
echo   蹂쇰쭅 ?곸긽 ?몄쭛湲?- 鍮뚮뱶 諛?GitHub 諛고룷
echo ============================================
echo.

:: ===== ?ㅼ젙 =====
set GITHUB_USER=grasion
set GITHUB_REPO=BowlingVideoEditor
set VERSION=1.0.0
set TAG=v%VERSION%

:: ===== 1) 鍮뚮뱶 =====
echo [1/4] 鍮뚮뱶 以?..
cd BowlingVideoEditor
dotnet publish -c Release -o publish
if %errorlevel% neq 0 ( echo 鍮뚮뱶 ?ㅽ뙣! & pause & exit /b 1 )
echo.

:: ===== 2) Inno Setup?쇰줈 Setup.exe ?앹꽦 =====
echo [2/4] Setup.exe ?앹꽦 以?..
set "ISCC="
for %%p in ("%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "C:\Program Files\Inno Setup 6\ISCC.exe") do (
    if exist %%p set "ISCC=%%~p"
)
if defined ISCC (
    "%ISCC%" installer.iss
    if %errorlevel% equ 0 ( echo Setup.exe ?앹꽦 ?꾨즺! ) else ( echo Setup ?앹꽦 ?ㅽ뙣 )
) else (
    echo Inno Setup 誘몄꽕移? winget install JRSoftware.InnoSetup ?쇰줈 ?ㅼ튂?섏꽭??
)
echo.

:: ===== 3) Git ?몄떆 =====
echo [3/4] Git ?몄떆...
cd ..
git add -A
git commit -m "Release %TAG%"
git tag -a %TAG% -m "Release %TAG%" 2>nul
git push origin main
git push origin %TAG%
echo.

:: ===== 4) GitHub Release =====
echo [4/4] GitHub Release...
where gh >nul 2>&1
if %errorlevel% equ 0 (
    set "FILES="
    if exist "BowlingVideoEditor\installer_output\BowlingVideoEditor_Setup_v%VERSION%.exe" (
        set "FILES=!FILES! BowlingVideoEditor\installer_output\BowlingVideoEditor_Setup_v%VERSION%.exe"
    )
    if exist "BowlingVideoEditor\installer_output2\BowlingVideoEditor_Setup_v%VERSION%.exe" (
        set "FILES=!FILES! BowlingVideoEditor\installer_output2\BowlingVideoEditor_Setup_v%VERSION%.exe"
    )
    gh release create %TAG% !FILES! --title "蹂쇰쭅 ?곸긽 ?몄쭛湲?%TAG%" --notes "## 蹂쇰쭅 ?곸긽 ?몄쭛湲?%TAG%
- BowlingVideoEditor_Setup_v%VERSION%.exe ?ㅼ슫濡쒕뱶 ???ㅽ뻾?섏뿬 ?ㅼ튂"
    echo Release: https://github.com/%GITHUB_USER%/%GITHUB_REPO%/releases/tag/%TAG%
) else (
    echo gh CLI 誘몄꽕移? https://cli.github.com/ ?먯꽌 ?ㅼ튂?섏꽭??
    echo ?섎룞 ?낅줈?? BowlingVideoEditor\installer_output\BowlingVideoEditor_Setup_v%VERSION%.exe
)

echo.
echo ============================================
echo   ?꾨즺!
echo ============================================
pause
