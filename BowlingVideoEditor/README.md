# 🎳 볼링 영상 편집기 (Bowling Video Editor)

볼링 영상을 편집하고 점수판을 오버레이할 수 있는 프로그램입니다.

## 기능

- 영상 열기 및 재생 (mp4, avi, mkv, mov 등)
- 구간 자르기 (시작/끝 시간 지정)
- 여러 클립 합치기
- 볼링 점수판 오버레이 (영상 위에 점수 표시)
- GitHub 기반 자동 업데이트

## 요구사항

- Windows 10 이상
- .NET 8.0 Runtime
- FFmpeg (시스템 PATH에 등록 필요)

## 빌드

```bash
dotnet restore
dotnet build
dotnet publish -c Release -r win-x64 --self-contained
```

## FFmpeg 설치

1. https://ffmpeg.org/download.html 에서 다운로드
2. 압축 해제 후 `bin` 폴더를 시스템 PATH에 추가

## 자동 업데이트 설정

1. GitHub에 리포지토리 생성
2. `UpdateService.cs`에서 `YOUR_GITHUB_USERNAME`을 본인 GitHub 사용자명으로 변경
3. Release 생성 시 빌드된 zip 파일을 첨부하면 자동 업데이트 가능

## 라이선스

MIT License - 자유롭게 사용, 수정, 배포 가능합니다.

### 사용된 오픈소스 라이브러리

| 라이브러리 | 라이선스 |
|-----------|---------|
| FFMpegCore | MIT |
| LibVLCSharp | LGPL-2.1 |
| VideoLAN.LibVLC | LGPL-2.1 |
| Octokit | MIT |
