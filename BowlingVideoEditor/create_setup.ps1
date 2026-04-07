# 볼링 영상 편집기 - Setup ZIP 생성
param([string]$Version = "1.0.0")

$ErrorActionPreference = "Stop"
$publishDir = "publish"
$outputDir = "installer_output"

if (-not (Test-Path $publishDir)) {
    Write-Host "ERROR: publish 폴더가 없습니다." -ForegroundColor Red; exit 1
}

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

# setup_script.bat을 publish에 복사
Copy-Item "setup_script.bat" "$publishDir\setup_script.bat" -Force

# Setup ZIP (설치 스크립트 포함)
$setupZip = "$outputDir\BowlingVideoEditor_Setup_v$Version.zip"
if (Test-Path $setupZip) { Remove-Item $setupZip -Force }
Compress-Archive -Path "$publishDir\*" -DestinationPath $setupZip -Force

# publish에서 setup_script.bat 제거
Remove-Item "$publishDir\setup_script.bat" -Force -ErrorAction SilentlyContinue

# Portable ZIP (설치 스크립트 없이)
$portableZip = "$outputDir\BowlingVideoEditor_Portable_v$Version.zip"
if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
Compress-Archive -Path "$publishDir\*" -DestinationPath $portableZip -Force

# 이전 파일 정리
Remove-Item "$outputDir\install_v*.bat" -Force -ErrorAction SilentlyContinue
Remove-Item "$outputDir\uninstall.bat" -Force -ErrorAction SilentlyContinue
Remove-Item "$outputDir\BowlingVideoEditor_v*.zip" -Force -ErrorAction SilentlyContinue

$setupSize = [math]::Round((Get-Item $setupZip).Length / 1MB, 1)
$portableSize = [math]::Round((Get-Item $portableZip).Length / 1MB, 1)

Write-Host ""
Write-Host "=== 생성 완료 ===" -ForegroundColor Green
Write-Host "  Setup:    $setupZip ($setupSize MB)" -ForegroundColor Cyan
Write-Host "  Portable: $portableZip ($portableSize MB)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Setup 사용법:" -ForegroundColor Yellow
Write-Host "  1. BowlingVideoEditor_Setup_v$Version.zip 압축 해제"
Write-Host "  2. setup_script.bat 실행 (더블클릭)"
Write-Host "  -> 바탕화면 바로가기 + 시작메뉴 + 프로그램 추가/제거 등록"
