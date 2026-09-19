# Sentinel Windows 설치 패키지

1. `dotnet publish desktop/Watchblock.App -c Release -r win-x64 --self-contained true -p:RestoreConfigFile="$PWD/NuGet.Config" -o artifacts/sentinel-app`
2. `./scripts/package.ps1` (런타임 고지와 문서 준비)
3. `./scripts/install-inno.ps1` (해시 고정 Inno Setup 6.7.3 빌드 도구 설치)
4. `./scripts/build-installer.ps1`

결과: `artifacts/Sentinel-<버전>-Setup-x64.exe`, 별도 SHA-256 파일. 개발 도구 설치는 해당 PC에서 한 번만 하면 됩니다. 기존 ISCC는 `-Compiler`로 지정할 수 있습니다.

AppId는 업데이트 식별자이므로 버전이 올라가도 변경하지 않습니다. 실행 중인 앱은 mutex로 설치·제거를 막으며 검사를 강제로 종료하지 않습니다. 설치 파일은 현재 사용자 계정에만 설치합니다. 보고서나 전체 사용자 폴더를 삭제하는 동작은 없습니다. 서버 전용 앱을 만들려면 빌드 전에 공개 origin만 포함한 `Sentinel.service.json`을 `artifacts/sentinel-app`에 넣으세요. 기존 설치의 서버 설정은 덮어쓰지 않습니다.
