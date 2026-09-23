# Sentinel

Minecraft Java 서버를 위한 Windows 검사 앱입니다. 플레이어가 선택한 범위에서 치트 파일과 실행 흔적을 확인하고, 운영자는 웹에서 보고서를 검토할 수 있습니다.

[웹 열기](https://sentinel.sentinel-ac.workers.dev) · [다운로드](https://github.com/Junnior123/Sentinel/releases)

## 사용하기

1. 운영자가 웹에서 검사를 만들고 초대 링크를 전달합니다.
2. 플레이어는 초대 화면에서 앱을 내려받아 설치합니다.
3. 앱에 초대를 붙여넣고 검사 범위를 확인합니다.
4. 검사와 자동 제출에 동의하고 시작하면, 완료 후 보고서가 전송됩니다.

Windows x64용이며, .NET을 따로 설치할 필요는 없습니다.

## 검사와 보고서

파일 이름뿐 아니라 해시와 내부 구조를 검사합니다. 모드·리소스팩·실행 파일·매크로의 탐지 근거와 관련 실행·삭제 흔적을 보고서에 표시합니다. 확인하지 못한 항목은 별도로 남깁니다.

파일 발견만으로 게임에서 사용했다고 단정하지 않습니다. 결과는 운영자가 검토하며 자동으로 차단하지 않습니다. 현재 검증 범위는 [검증 기록](VALIDATION.md)에서 확인할 수 있습니다.

탐지 근거와 관련 파일 정보, 실행 흔적, 검사 누락을 전송합니다. 전체 파일 목록은 앱에서만 확인·저장할 수 있으며, 파일 원본이나 개인 문서 내용은 전송하지 않습니다. 보고서는 기본 7일 동안 보관됩니다.

## 개발

Node.js 24, pnpm 11, .NET 10 SDK가 필요합니다. Windows 앱은 Windows에서 빌드합니다.

```powershell
pnpm install --frozen-lockfile
pnpm build
pnpm local
```

로컬 웹은 `http://localhost:8787/dev/login`에서 확인할 수 있습니다. 로컬 개발 서버를 외부에 공개하지 마세요.

```powershell
dotnet run --project desktop/Watchblock.App -p:RestoreConfigFile="$PWD/NuGet.Config"
```

빌드·배포와 규칙 추가 방법은 [기여 안내](CONTRIBUTING.md)에 정리했습니다.

## 라이선스

[MIT](LICENSE). 외부 구성요소의 라이선스는 [별도 고지](THIRD_PARTY_NOTICES.md)를 따릅니다.
