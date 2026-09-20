# 기여 안내

수정 범위를 작게 유지하고 오류를 정상 결과로 바꾸지 마세요. 수집 범위 확대는 UI 동의 설명·API 계약·개인정보 테스트를 함께 수정해야 합니다.

## 빌드와 배포

GitHub Actions의 `Build and test`가 Windows 앱과 Setup을 빌드하고 설치·제거를 검사합니다. 로컬에서는 `dotnet publish desktop/Watchblock.App -c Release -r win-x64 --self-contained true -p:RestoreConfigFile="$PWD/NuGet.Config" -o artifacts/sentinel-app` 이후 `scripts/package.ps1`, `scripts/install-inno.ps1`, `scripts/build-installer.ps1`을 순서대로 실행합니다.

운영자 웹은 Cloudflare Workers와 D1을 사용합니다. 자신의 계정에 처음 배포하는 경우:

1. `pnpm exec wrangler login`으로 로그인하고 `pnpm exec wrangler d1 create watchblock`으로 DB를 만듭니다.
2. `pnpm setup:production`에 실제 HTTPS 주소, DB ID, 본인의 GitHub 사용자명을 입력합니다. 생성된 `wrangler.production.json`은 저장소에 올리지 않습니다.
3. `pnpm db:production`과 `pnpm run deploy`를 실행합니다.
4. GitHub OAuth App의 홈페이지를 서비스 주소로, callback을 서비스 주소 뒤 `/auth/callback`으로 설정합니다.
5. 아래 명령의 입력창에 Client ID와 Secret을 각각 저장합니다. 비밀값을 소스에 넣지 않습니다.

```powershell
pnpm exec wrangler secret put GITHUB_CLIENT_ID --config wrangler.production.json
pnpm exec wrangler secret put GITHUB_CLIENT_SECRET --config wrangler.production.json
```

`wrangler.production.json`의 `DOWNLOAD_URL`에 공개된 GitHub Releases Setup 파일 주소를 넣고 재배포하면 로그인·초대 화면에서 바로 다운로드할 수 있습니다. 배포 후 실제 로그인과 작은 폴더의 검사·제출을 확인하세요. 사용량과 오류는 Cloudflare 대시보드에서 확인합니다.

[Cloudflare 배포 설정](https://developers.cloudflare.com/workers/wrangler/configuration/) · [GitHub OAuth App](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/creating-an-oauth-app)

## 규칙 추가

`rules/catalog.json`에 규칙을 추가합니다. `id`, `title`, `category`, `source`, `sha256`, `allEntries`, `allStrings`, `excludeSha256`, `formats`, `validation`을 기록합니다. 특정 버전 해시는 검증한 배포 URL과 `samples/manifest.json`에 버전을 함께 기록합니다.

- `sha256`: 공식 또는 허가된 표본의 정확한 해시. 출처와 파일을 직접 대조합니다.
- `allEntries` / `allStrings`: 모두 만족해야 하는 복수 특징. 경로·문자열만으로 알려진 치트 확정 판정을 하지 않으며 `review`로 내려갑니다.
- `formats`: 적용 형식 제한. 빈 배열은 형식 공통. 텍스트 규칙을 PE·Java에 무차별 적용하지 마세요.
- `entrySha256`: 검증한 내부 파일 경로→SHA-256 조합(최소 2개). ZIP·리소스팩 폴더에서 모든 해시가 일치해야 합니다. 현재 Spectator Xray는 3개 모델 파일을 사용합니다. 파일명만 같은 정상 표본을 반드시 음성 검증하세요.
- `excludeSha256`: 출처가 검증된 정상 파일 또는 별도 정확한 규칙으로 처리한 표본만 예외로 넣습니다.
- 원문 스크립트·파일 이름·서명 없음 하나만으로 치트 사용을 확정하지 마세요.

앱에 규칙은 버전을 고정하여 포함됩니다. 초기 버전은 원격 규칙 자동 업데이트를 하지 않습니다. 규칙 변경 시 앱을 새로 빌드해 배포합니다.

`pnpm samples`는 공식 공개 표본을 로컬 `samples/private`에 다운로드합니다. 파일을 실행하지 않습니다. 배포 출처가 변경되면 실패 원인을 확인한 뒤 manifest를 검토해야 합니다. 원본 바이너리를 Git 또는 앱 배포에 넣지 않습니다.

## 테스트

```powershell
pnpm test
dotnet run --project desktop/Watchblock.Tests -- .
pnpm local
# 별도 터미널
dotnet run --project desktop/Watchblock.Smoke -- .
pnpm test:web
```

탐지 표본 추가 시 정상 표본·이름 변경·압축 변형·사용하지 않고 보유만 한 경우를 같이 검증합니다. 보호 메모리나 다른 사용자 계정 데이터 수집 코드는 첫 버전 범위에 없습니다.
