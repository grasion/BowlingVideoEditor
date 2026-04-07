@echo off
chcp 65001 >nul
echo ============================================
echo   버전 업데이트 도구
echo ============================================
echo.
echo 현재 버전을 입력하세요 (예: 1.0.1):
set /p NEW_VERSION=

if "%NEW_VERSION%"=="" (
    echo 버전을 입력하지 않았습니다.
    pause
    exit /b 1
)

echo.
echo 버전을 %NEW_VERSION%으로 업데이트합니다...

:: csproj 버전 업데이트
powershell -Command "(Get-Content 'BowlingVideoEditor\BowlingVideoEditor.csproj') -replace '<Version>.*</Version>', '<Version>%NEW_VERSION%</Version>' -replace '<AssemblyVersion>.*</AssemblyVersion>', '<AssemblyVersion>%NEW_VERSION%.0</AssemblyVersion>' -replace '<FileVersion>.*</FileVersion>', '<FileVersion>%NEW_VERSION%.0</FileVersion>' | Set-Content 'BowlingVideoEditor\BowlingVideoEditor.csproj' -Encoding UTF8"

:: installer.iss 버전 업데이트
powershell -Command "(Get-Content 'BowlingVideoEditor\installer.iss') -replace 'AppVersion=.*', 'AppVersion=%NEW_VERSION%' -replace 'OutputBaseFilename=.*', 'OutputBaseFilename=BowlingVideoEditor_Setup_v%NEW_VERSION%' | Set-Content 'BowlingVideoEditor\installer.iss' -Encoding UTF8"

:: build_and_publish.bat 버전 업데이트
powershell -Command "(Get-Content 'build_and_publish.bat') -replace 'set VERSION=.*', 'set VERSION=%NEW_VERSION%' | Set-Content 'build_and_publish.bat' -Encoding UTF8"

echo.
echo 버전 %NEW_VERSION% 업데이트 완료!
echo build_and_publish.bat를 실행하여 빌드 및 배포하세요.
pause
