# Sentinel 0.5.1

[운영자 웹](https://sentinel.sentinel-ac.workers.dev) · [설치 파일 다운로드](https://github.com/Junnior123/Sentinel/releases/tag/v0.5.1)

Windows 설치본: `Sentinel-0.5.1-Setup-x64.exe`를 실행하면 한국어 설치 안내, 시작 메뉴 바로가기와 제거 기능을 사용할 수 있습니다. 별도 .NET 설치는 필요하지 않습니다. 설치 없는 ZIP도 함께 제공합니다.

이전 작업명은 Watchblock입니다. 앱·웹 이름은 Sentinel로 변경했으며 기존 초대와 내부 소스 경로는 호환성을 위해 유지합니다.

실제 외부 운영: **[지금 할 일만 따라하기](docs/DEPLOY-EASY.md)** · [전체 배포 안내](docs/DEPLOY.md) · [무료 호스팅 13곳 비교](docs/FREE-HOSTING.md) · [0.5.1 변경 사항](docs/RELEASE-0.5.1.md).

앱 사용법은 [시작 안내](docs/GETTING_STARTED.md)를 읽어 주세요.

0.4.0의 UI·빠른 검사·휴지통 삭제 기록·상세 공개 보고서 사용법: [업데이트 안내](docs/RELEASE-0.4.0.md).

## 프로젝트 개요

Minecraft Java 플레이어가 동의하고 실행하는 **Windows 검사 앱 + 운영자 웹**입니다. ModBridge와 별개의 프로젝트입니다.

현재 0.5.1 웹과 DB를 Cloudflare에 배포했습니다. GitHub 로그인과 검사 코드 발급을 확인했으며, 공개 설치 파일은 Releases에서 배포합니다. 타 PC 제출 시험은 남아 있습니다. Windows 실행 ZIP에는 .NET 런타임이 포함되며 별도 설치가 필요 없습니다. Windows 11 실기기 및 실제 호스팅 검증은 [남은 확인 항목](VALIDATION.md)을 참고하세요.

파일 보유, 실행 흔적, 게임에서의 사용은 서로 다른 사실입니다. 자동 밴·AI 유죄 판정·커널 드라이버·상시 감시 기능은 없습니다.

## 실행

배포 ZIP을 별도 폴더에 풀고 `Sentinel.exe`를 실행합니다. 운영자가 전달한 **초대 링크를 붙여넣거나 초대 파일을 엽니다**. 서버 주소를 따로 입력할 필요가 없습니다. 서버 전용 설정이 포함된 앱은 12자리 코드만 입력합니다. 연결 → 범위 확인·동의 → 검사 → 결과 제출의 네 단계로 진행합니다. 프로그램은 제출 전에도 연결·진행·취소 상태를 운영자에게 알립니다. 파일 보고서는 제출 버튼을 눌러야 전송됩니다.

앱에서 서버 보고서를 삭제할 수 있습니다. 앱을 닫으면 세션 토큰은 사라집니다. 이후 삭제가 필요하면 보고서의 검사 ID로 운영자에게 요청하세요. 삭제 후 Cloudflare 관리 백업에 남는 기간은 [D1 Time Travel](https://developers.cloudflare.com/d1/reference/time-travel/) 정책을 따릅니다.

## 개발

필요 도구: .NET 10 SDK, Node.js 24, pnpm. Windows 앱은 Windows에서 빌드합니다.

```powershell
pnpm install --frozen-lockfile
pnpm build
dotnet build desktop/Watchblock.App -p:RestoreConfigFile="$PWD/NuGet.Config"
dotnet run --project desktop/Watchblock.App
```

배포 ZIP 생성: `dotnet publish desktop/Watchblock.App -c Release -r win-x64 --self-contained true -p:RestoreConfigFile="$PWD/NuGet.Config" -o artifacts/sentinel-app` 실행 후 `./scripts/package.ps1`. SDK를 별도 폴더에 설치했다면 `-DotnetRoot <SDK 폴더>`를 지정합니다. GitHub Actions도 실행 ZIP·소스 ZIP을 생성합니다.

로컬 통합 확인은 `pnpm local` 실행 후 `http://localhost:8787/dev/login`에 접속합니다. 이 서버는 로컬 전용 계정을 자동 설정하며 인터넷에 노출하면 안 됩니다. 프로덕션 Worker에는 개발 로그인 코드가 들어가지 않습니다. `.tools/local.db`에 로컬 기록이 저장됩니다.

Cloudflare 배포는 [배포 안내](docs/DEPLOY.md)를 따릅니다. 운영자별 별도 Cloudflare 계정 설치를 지원합니다. Ocean 계정·유료 API·AI 모델이 필요하지 않습니다.

## 0.2 확장 검사

전체 확장자 파일·드라이브, 엑스레이 모드/리소스팩, 프리캠·미니맵 금지 정책, 하드웨어 식별정보 변경·네트워크 주소 재정의·테스트 서명 상태 검사를 추가했습니다. **범위·제한과 직접 해야 할 배포 작업은 [0.2 사용 안내](docs/USAGE-0.2.md)**에 정리했습니다.

## 실제 지원 범위

- SHA-256, JAR/ZIP 파일 목록, Java 클래스 상수 풀, Fabric·Forge·NeoForge 메타데이터.
- EXE/DLL 형식과 내장 Authenticode 신뢰 확인. 온라인 인증서 폐기 확인·카탈로그 서명 검사는 제외합니다.
- 검증된 공식 표본의 정확한 해시와 복수 내부 특징. 이 서비스는 검증된 오토클리커·매크로 배포본도 `치트 프로그램`으로 분류합니다. 단순 문자열 유사성은 `검토 필요`이며, 파일 발견과 게임 내 사용은 구분합니다.
- 선택 파일과 연결 가능한 현재 프로세스, 현재 사용자 BAM, 지원 형식 Prefetch. 경로·파일명 연결 수준을 기록하고 과거 실행 파일의 동일성을 주장하지 않습니다.
- 내장 JAR, 검사 한도 초과, 권한 부족, 손상 파일은 `부분 완료` 또는 오류로 표시합니다. 누락을 정상으로 간주하지 않습니다.

탐지 카탈로그는 작은 초기 표본 집합입니다. 모든 치트·모든 런처·메모리에만 존재하는 치트를 탐지하지 못합니다. 탐지율 수치를 제품 성능으로 일반화하지 않습니다. [검증 기록](VALIDATION.md)을 확인하세요.

## 데이터

보고서에는 선택 범위의 대상 파일 목록·상대 경로·해시·정적 검사 상태·탐지 근거·관련 실행 흔적이 포함됩니다. 파일 원본, 개인 문서 내용, 인증 토큰, 화면 캡처, 브라우저 기록은 전송하지 않습니다. 추가 폴더 선택은 그 폴더에 있는 대상 형식 파일의 목록 공유에 동의하는 의미입니다.

서버는 기본 7일 보관하며 운영자는 자신이 생성한 검사만 봅니다. 검사당 10MB를 초과하면 부분 제출입니다. 보고서와 임시 전송 페이지는 같은 삭제 정책을 적용합니다. 플랫폼 운영 로그에는 요청 IP가 남을 수 있습니다. 자체 DB에는 원 IP 대신 짧은 기간 유지하는 해시 기반 요청 제한 키를 저장합니다.

## English overview

Watchblock is a consent-based Windows inspection tool for Minecraft Java communities, with a self-hostable operator dashboard on Cloudflare Workers and D1. It distinguishes file identification, execution artifacts, incomplete coverage, and manual review. It does not automatically ban players or attest that an untrusted endpoint is clean. The initial rule catalog is intentionally small and includes documented primary-source fixtures. Source code is MIT; third-party components retain their own licenses.
