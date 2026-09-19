# Sentinel — 지금 할 일만 따라하기

2026-09-19 기준입니다. **웹과 DB는 이미 배포했습니다. Cloudflare 가입·DB 생성·명령어 배포를 다시 할 필요 없습니다.**

사이트: **https://sentinel.sentinel-ac.workers.dev**

**GitHub 로그인 연결까지 완료했습니다.** Junnior123으로 운영자 화면에 로그인하고 실제 검사 코드가 발급되는 것까지 확인했습니다. Cloudflare의 두 GitHub 비밀값도 암호화 저장돼 있습니다. 아래 1~3번 설정을 다시 할 필요 없습니다.

지금은 **5번 첫 검사**로 진행하세요. 웹의 ‘검사 만들기’에서 새 초대를 만들고 앱에 붙여넣으면 됩니다. 코드 유효시간은 15분입니다. 앱 설치는 **4번**을 참고하세요.

## 1. GitHub에 로그인 연결 만들기

1. **Junnior123** 계정으로 GitHub에 로그인합니다.
2. 본인 계정에서는 [이미 등록한 Sentinel 설정](https://github.com/settings/applications/3867858)을 엽니다. 아래 입력값을 확인할 수 있습니다. 다른 운영자가 처음 설치할 때만 [새 로그인 연결](https://github.com/settings/applications/new)을 만듭니다.
3. 아래 세 칸을 그대로 복사해 넣습니다.

| 화면에 보이는 이름 | 붙여넣을 값 |
| --- | --- |
| Application name | `Sentinel 운영자 웹` |
| Homepage URL | `https://sentinel.sentinel-ac.workers.dev` |
| Redirect URI (기존 명칭: Authorization callback URL) | `https://sentinel.sentinel-ac.workers.dev/auth/callback` |

4. Description은 비워도 됩니다. Device Flow는 선택하지 않습니다. **Register application**을 누릅니다.
5. 열린 화면의 **Client ID**를 확인합니다. 이어서 **Generate a new client secret**을 누릅니다. 본인 확인을 요청하면 진행합니다.
6. 이 GitHub 창은 닫지 않고 그대로 둡니다. 다음 단계에서 두 값을 각각 복사합니다.

Client secret은 채팅·스크린샷·소스 파일에 넣지 마세요. Cloudflare의 비밀값 입력칸에만 붙여넣습니다. [GitHub 공식 안내](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/creating-an-oauth-app)

## 2. 두 값을 Cloudflare에 붙여넣기

1. 새 브라우저 탭에서 [Cloudflare](https://dash.cloudflare.com/)에 로그인합니다.
2. **Workers & Pages → sentinel → Settings** 순서로 엽니다.
3. **Runtime variables and secrets → Add variable**를 누릅니다.
4. Key와 Value를 아래처럼 입력하고 각 항목의 **Secret**을 체크합니다. 이전 화면에서는 Type을 Secret으로 고릅니다.

| Variable name — 직접 입력 | Value — GitHub에서 복사 |
| --- | --- |
| `GITHUB_CLIENT_ID` | GitHub 화면의 Client ID |
| `GITHUB_CLIENT_SECRET` | 방금 만든 Client secret |

5. 입력창의 **Add**로 두 번째 줄을 추가합니다. 두 줄 모두 Secret을 체크합니다.
6. **Add 2 variables and deploy**를 눌러 반영합니다. `PUBLIC_ORIGIN` 등 이미 있는 다른 설정은 수정하지 않습니다.

[Cloudflare 공식 Secret 설정 안내](https://developers.cloudflare.com/workers/configuration/secrets/)

## 3. 로그인되는지 확인하기

1. [Sentinel 웹](https://sentinel.sentinel-ac.workers.dev)을 새로고침합니다.
2. **GitHub로 로그인**을 누르고 본인이 만든 Sentinel 연결을 승인합니다.
3. **검사 관리** 화면이 나오면 성공입니다. Junnior123은 설치자 권한으로 연결됩니다.

로그인 준비 중 문구가 남으면 Cloudflare에 두 항목이 정확한 이름으로 저장됐는지, Deploy를 눌렀는지 확인합니다. callback 오류는 1번 표의 마지막 주소와 GitHub 설정이 일치하는지 확인합니다.

[서비스 상태 확인](https://sentinel.sentinel-ac.workers.dev/api/health)에서 `ready: true`는 설정값과 DB 준비 여부입니다. 실제 로그인 성공까지 확인해야 연결 검증이 끝납니다.

## 4. 앱 내려받고 설치하기

1. [Sentinel 0.5.1 다운로드](https://github.com/Junnior123/Sentinel/releases/tag/v0.5.1)를 엽니다.
2. **Assets**에서 `Sentinel-0.5.1-Setup-x64.exe`를 내려받습니다. 소스 코드를 받을 필요 없습니다.
3. 기존 Sentinel을 종료하고 Setup을 실행합니다. 설치가 끝나면 시작 메뉴에서 Sentinel을 엽니다. 별도 .NET 설치는 필요 없습니다.
4. 웹에서 만든 초대 링크를 앱에 붙여넣습니다.

설치본은 아직 코드 서명되지 않았습니다. 배포 페이지의 SHA-256 파일로 다운로드 무결성을 확인할 수 있습니다. 설치 없이 실행하려면 `Sentinel-0.5.1-win-x64.zip`을 풀어 사용합니다. 공개 소스는 [GitHub 저장소](https://github.com/Junnior123/Sentinel)에 있습니다.

## 5. 첫 검사는 작게 해보기

1. 앱을 종료한 뒤 새 Setup을 실행해 설치합니다. 별도 .NET 설치는 필요 없습니다.
2. 웹에서 **검사 만들기**로 테스트용 검사를 만들고 초대를 복사합니다.
3. 앱에 초대를 붙여넣습니다. 운영자와 서비스 주소를 확인합니다.
4. 첫 시험에서는 작은 테스트 폴더만 선택하고 동의 → 검사 시작 → 결과 확인 → 제출 순서로 진행합니다.
5. 웹에서 파일 목록·탐지 근거·검사 누락이 보이는지 확인합니다.
6. 다음에는 다른 Windows 11 PC에서도 설치와 제출을 확인합니다. 공개 링크를 만들었다면 로그아웃한 브라우저에서 열어보고, 공유 중지 후 접근이 막히는지도 확인합니다.

웹은 Cloudflare에서 실행되므로 이 PC나 로컬 서버를 계속 켜둘 필요 없습니다. 플레이어 PC는 검사·제출 중 켜져 있어야 합니다. 무료 한도·장애에 따른 중단 가능성은 있습니다. 대규모 운영 전 실제 보고서로 사용량을 확인하세요.

현재 검증하지 못한 항목은 Windows 11 새 설치·다른 PC에서 실제 서버로 보고서 제출입니다. GitHub 로그인과 실제 코드 발급은 확인했습니다. 파일 발견을 게임에서 사용했다는 사실로 해석하지 않습니다.

다른 운영자가 처음부터 설치하거나 명령어로 관리할 때는 [전체 배포 안내](DEPLOY.md)를 참고하세요.
