# 외부 구성요소

자체 작성 코드는 MIT입니다. 다음 구성요소의 라이선스는 각 저작권자에게 있습니다. 버전은 프로젝트 파일 및 pnpm-lock.yaml에 고정합니다.

- .NET / WPF: MIT. 배포본에 포함되는 런타임의 LICENSE·ThirdPartyNotices 파일을 보존합니다. https://github.com/dotnet/runtime / https://github.com/dotnet/wpf
- React / React DOM: MIT. https://github.com/facebook/react
- Zod: MIT. https://github.com/colinhacks/zod
- Cloudflare Workers SDK / Wrangler: MIT 또는 Apache-2.0. 개발·배포 도구. https://github.com/cloudflare/workers-sdk
- Inno Setup 6.7.3: 설치·제거 프로그램 생성 도구. 자체 Inno Setup License로 배포되며 MIT가 아닙니다. Setup 설치 폴더에 `INNO-SETUP-LICENSE.txt`를 포함합니다. 공식 배포 파일의 SHA-256을 확인해 빌드합니다. https://github.com/jrsoftware/issrc / https://jrsoftware.org/isdl.php
- Vite, TypeScript, tsx, Playwright 등 개발 도구는 해당 패키지 라이선스를 따릅니다. 배포 소스에는 패키지 잠금 파일과 설치 절차를 포함합니다.

Windows 시스템 API는 OS가 제공합니다. 외부 스크린셰어 프로그램의 검사 코드를 복사하거나 실행하지 않습니다. Prefetch 최소 파서는 자체 구현이며 지원 형식을 벗어나면 검사 누락을 기록합니다.

검증용 Meteor, 오토클리커 등은 각 프로젝트의 라이선스가 적용됩니다. 코드·바이너리를 이 프로그램에 포함하지 않으며 출처 URL·검증 해시만 배포합니다. 정상 .NET 표본도 테스트 아티팩트에만 두고 재배포하지 않습니다.
