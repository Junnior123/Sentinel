# Cloudflare에 Sentinel 배포하기

**이 안내를 끝내면 내 PC를 꺼도 웹에 접속할 수 있습니다.** 호스팅은 Cloudflare Free, 설치 파일 배포는 GitHub Releases를 사용합니다. 자동 유료 전환은 하지 않습니다. 무료 한도와 장애에 따른 중단은 가능하며 무중단을 보증하지 않습니다.

현재 Junnior123의 웹·DB는 https://sentinel.sentinel-ac.workers.dev 에 배포됐습니다. **본인은 [지금 할 일만 따라하기](DEPLOY-EASY.md)부터 읽으세요.** 아래는 다른 운영자가 새 계정에 설치하는 전체 절차입니다. 이미 만든 DB와 웹을 다시 만들지 않습니다.

## 1. Cloudflare 계정 만들기

1. [Cloudflare 가입](https://dash.cloudflare.com/sign-up)에서 가입하고 이메일을 확인합니다.
2. **Workers & Pages**로 이동합니다. 도메인을 구매하거나 기존 도메인을 추가할 필요는 없습니다.
3. Workers의 무료 `workers.dev` 서브도메인을 설정합니다. 메뉴 이름이 바뀌었다면 Workers 설정에서 `workers.dev`를 찾습니다.
4. 예를 들어 서브도메인을 `my-server`로 정하고 Worker 이름을 `sentinel`로 쓰면 서비스 주소는 `https://sentinel.my-server.workers.dev`입니다. 실제 계정에서 확인한 값을 사용합니다.
5. Workers **Free** 플랜을 유지합니다. 이 안내에는 유료 플랜·R2·카드 결제가 필요하지 않습니다.

[공식 workers.dev 설정 안내](https://developers.cloudflare.com/workers/configuration/routing/workers-dev/)

## 2. 명령을 실행할 폴더 열기

Windows 탐색기에서 소스 폴더를 열고 주소창에 `powershell`을 입력한 다음 Enter를 누릅니다. `package.json`이 있는 폴더여야 합니다. 예시 작업 폴더는 다음과 같습니다.

```text
C:\Projects\Sentinel
```

[Node.js](https://nodejs.org/en/download) **24 LTS**를 설치합니다. 이미 설치되어 있다면 그대로 사용합니다. 다음 명령은 한 줄씩 실행하고 완료된 뒤 다음 줄을 실행합니다.

```powershell
npm install --global pnpm@11.19.0
pnpm install --frozen-lockfile
pnpm exec wrangler login
```

브라우저가 열리면 Cloudflare 로그인과 Wrangler 권한을 승인합니다. 비밀번호나 토큰을 채팅에 보내지 않습니다.

## 3. 보고서 DB 만들기

```powershell
pnpm exec wrangler d1 create watchblock
```

출력에 표시되는 `database_id` 값을 복사해 둡니다. `watchblock`은 내부 DB 이름이라 그대로 사용해도 됩니다. 이미 같은 이름의 DB를 만들었다면 또 만들지 말고 Cloudflare **D1 → 해당 DB → Overview**에서 ID를 확인합니다.

## 4. 실서버 설정 만들기

```powershell
pnpm setup:production
```

질문에 다음과 같이 답합니다.

| 입력 항목 | 넣을 값 |
| --- | --- |
| Worker 이름 | `sentinel` 또는 본인이 정한 이름 |
| HTTPS 주소 | 1단계에서 정한 실제 주소. 끝에 `/`를 붙이지 않음 |
| database_id | 3단계에서 복사한 값 |
| GitHub 사용자명 | `Junnior123` (그냥 Enter로 선택 가능) |
| 서비스 이름 | 플레이어에게 보일 이름 |
| Setup 다운로드 주소 | 아직 GitHub에 올리지 않았다면 Enter |

GitHub 숫자 ID는 자동으로 조회합니다. 조회가 막힌 경우 표시된 GitHub API 주소에서 `id`의 숫자 값을 직접 입력합니다. 이름이 비슷한 다른 계정이 아닌지 확인합니다.

`wrangler.production.json`과 `artifacts/production/Sentinel.service.json`이 생성됩니다. 이 파일에는 공개 주소·설치자 숫자 ID만 들어가며 비밀키를 넣지 않습니다. 로컬 테스트 설정인 `wrangler.jsonc`는 수정하지 않습니다.

## 5. DB 적용과 첫 배포

```powershell
pnpm db:production
pnpm run deploy
```

0001~0003 마이그레이션이 적용되고 웹이 배포됩니다. 설정이 빠졌거나 localhost 주소이면 명령이 중단됩니다. 이 시점에는 웹이 열려도 **GitHub 로그인 준비 중**이 표시되는 것이 정상입니다.

## 6. GitHub 로그인 연결

[GitHub OAuth App 만들기](https://github.com/settings/applications/new)를 열고 아래처럼 입력합니다.

| 항목 | 값 |
| --- | --- |
| Application name | `Sentinel 운영자 웹` |
| Homepage URL | 실제 서비스 주소 |
| Authorization callback URL | 실제 서비스 주소 뒤에 `/auth/callback` |

등록 후 **Client ID**와 **Generate a new client secret**으로 만든 Secret을 각각 아래 명령의 입력창에 붙여넣습니다.

```powershell
pnpm exec wrangler secret put GITHUB_CLIENT_ID --config wrangler.production.json
pnpm exec wrangler secret put GITHUB_CLIENT_SECRET --config wrangler.production.json
```

비밀값을 명령 뒤에 직접 적거나 JSON 파일·GitHub 저장소·채팅에 넣지 않습니다. 두 명령이 끝나면 웹을 새로고침하고 **GitHub로 로그인**을 누릅니다. `Junnior123`은 설치자로 등록됩니다. 다른 운영자는 로그인 후 **운영자 초대**에서 등록합니다.

[GitHub 공식 안내](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/creating-an-oauth-app), [Cloudflare Secret 안내](https://developers.cloudflare.com/workers/configuration/secrets/)

## 7. 설치 파일 공개하기

1. GitHub에서 `Junnior123` 계정으로 새 공개 저장소를 만듭니다. 저장소 이름은 직접 정합니다.
2. 소스 ZIP `Sentinel-0.5.1-source.zip`을 풀어 소스 파일을 올립니다. ZIP 한 개만 소스로 올리면 Actions가 실행되지 않습니다. 숨김 `.github` 폴더도 포함합니다.
3. `.tools`, `samples/private`, `.wrangler`, `.dev.vars`, 개인 보고서와 비밀키는 올리지 않습니다. 제공 소스 ZIP에서는 제외되어 있습니다.
4. 저장소 **Releases → Draft a new release**에서 태그 `v0.5.1`을 만들고 다음 파일을 첨부합니다.
   - `Sentinel-0.5.1-Setup-x64.exe`
   - `Sentinel-0.5.1-Setup-x64.exe.sha256`
5. 출시 내용을 확인하고 Publish release를 누릅니다. 설치 파일은 아직 코드 서명되지 않았습니다. 이 사실과 검증 범위를 출시 설명에 적어 주세요.
6. 게시된 Setup 파일의 **링크 주소 복사**를 누릅니다. 보통 `https://github.com/Junnior123/저장소이름/releases/download/v0.5.1/Sentinel-0.5.1-Setup-x64.exe` 형태입니다.
7. `wrangler.production.json`의 `DOWNLOAD_URL`에 이 주소를 넣고 `pnpm run deploy`를 다시 실행합니다.
8. 로그아웃한 웹 첫 화면과 플레이어 초대 화면에서 **Windows 앱 설치** 버튼을 눌러 실제 다운로드되는지 확인합니다.

앱을 특정 서버에 고정해 다시 만들 필요는 없습니다. **공용 Setup 설치 → 웹 초대 링크 붙여넣기**로 실제 HTTPS 서버를 연결합니다. 코드만 입력하는 전용 앱이 필요할 때만 생성된 `Sentinel.service.json`을 `Sentinel.exe` 옆에 둡니다.

## 8. 공개 운영 전 시험

1. 실제 주소 뒤 `/api/health`를 열어 `ready: true`, `environment: production`인지 봅니다. `admissionsOpen: true`이면 새 검사 접수가 가능합니다.
2. 운영자가 웹에서 코드를 발급하고, **다른 PC**에 설치한 앱에 초대 링크를 붙여넣습니다.
3. 먼저 작은 테스트 폴더로 동의 → 검사 → 제출 → 웹 조회를 확인합니다.
4. 보고서 전송 중 인터넷을 잠깐 끊었다가 연결하고 **보고서 제출**을 다시 눌러 봅니다. 동일한 묶음은 중복 저장하지 않습니다. 앱을 닫으면 연결 토큰이 사라지므로 전송이 끝날 때까지 앱을 유지합니다.
5. 공개 보고서 링크를 휴대폰 모바일 데이터로 열어 확인하고 **공유 중지** 후 열리지 않는지 확인합니다.
6. 자신의 PC와 로컬 서버를 끈 뒤에도 휴대폰에서 웹이 열리는지 확인합니다.
7. Windows 11 PC에서 Setup 설치·실행·검사·제거를 확인합니다. 관리자 권한으로 실제 휴지통 삭제 저널을 읽는 검증도 별도로 필요합니다.

코드·로컬 환경 테스트와 실제 Cloudflare 웹·DB 배포를 확인했습니다. GitHub OAuth 로그인과 코드 발급도 확인했습니다. 무료 CPU 한도에서의 대형 보고서 처리와 타 PC 제출은 추가 검증이 필요합니다. 표본 탐지 결과를 전체 치트 탐지율로 해석하지 않습니다.

## 운영 중 할 일

- Cloudflare **Workers → Metrics**에서 요청 수·오류·CPU 시간을 봅니다. Free의 CPU 제한은 요청당 10ms입니다. 큰 보고서로 오류가 반복되면 신규 접수를 중단하고 원인을 확인합니다.
- **D1 → Metrics**에서 저장량·읽기·쓰기를 봅니다. 무료 DB 하나당 500MB이며 앱은 보고서 본문 약 350MB부터 신규 접수를 차단합니다. 인덱스·메타데이터도 용량을 사용하므로 대시보드 수치가 우선입니다.
- 보고서는 7일 후 만료됩니다. 시간별 작업이 만료 데이터를 지웁니다. 한도 초과로 정리 실행이 지연되어도 API는 만료 결과를 공개하지 않습니다.
- 접수를 잠시 멈추려면 `wrangler.production.json`의 `ADMISSIONS_OPEN`을 문자열 `"false"`로 바꾸고 `pnpm run deploy`합니다. 기존 보고서는 볼 수 있습니다.
- 업데이트: 새 코드로 `pnpm install --frozen-lockfile` → `pnpm test` → `pnpm db:production` → `pnpm run deploy`합니다. 새 Setup은 Releases에 게시하고 `DOWNLOAD_URL`을 갱신합니다.
- Actions 자동 배포는 선택 사항입니다. 실제 설정 파일을 저장소에 넣고 GitHub Actions의 `production` Environment에 `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`를 등록한 뒤 **Deploy operator dashboard → Run workflow**를 실행합니다. 처음에는 위 수동 배포만 해도 됩니다.

## 자주 막히는 부분

| 증상 | 확인할 것 |
| --- | --- |
| 설치 버튼 대신 배포 준비 중 | Setup을 Releases에 게시한 뒤 실제 파일 URL 설정·재배포 |
| 로그인 준비 중 | 두 OAuth Secret과 OWNER_GITHUB_ID 확인 |
| OAuth callback 오류 | GitHub callback 주소가 실제 주소 + `/auth/callback`과 완전히 같은지 확인 |
| 서비스에 연결할 수 없음 | D1 마이그레이션·바인딩·무료 한도 확인 |
| 코드 만료·사용됨 | 새 코드 발급. 발급 후 15분, 한 번만 연결 가능 |
| 앱 응답 주소가 다름 | 초대 링크와 PUBLIC_ORIGIN 일치 여부 확인 |
| 429·무료 한도 오류 | 재시도를 반복하지 말고 사용량·초기화 시간을 확인 |

호스팅 13곳의 비교 근거는 [FREE-HOSTING.md](FREE-HOSTING.md)에 있습니다.
