# Sentinel 0.5.1

- 앱과 웹을 회색·차콜 테마로 정리하고 화이트 모드 전환 및 설정 저장을 추가했습니다.
- 앱 상단 Windows / Java Edition 문구를 제거했습니다.
- 검사 중 회전하는 십자선을 고정된 폴더·돋보기로 변경했습니다. 폴더 상단 높이를 줄이고 그림과 배경을 약 10% 축소했습니다.
- Cloudflare 초기 DB 적용 오류를 수정했습니다. 용량·완료 상태를 막는 기존 DB 조건은 유지했습니다.
- 처음 운영하는 사람을 위한 [따라하기 안내](DEPLOY-EASY.md)를 추가했습니다.

## 배포와 검증

2026-09-19에 https://sentinel.sentinel-ac.workers.dev 로 웹을 배포하고 원격 D1 마이그레이션 3개를 적용했습니다. HTTPS 웹·DB 상태 응답과 보안 헤더를 확인했습니다. GitHub 로그인 비밀값과 공개 Setup 다운로드 주소는 아직 설정하지 않았습니다. 로그인 설정 전 신규 검사는 차단합니다.

API·계약 17개와 배포 설정 2개 테스트, TypeScript 검사 및 웹 빌드, WPF 다크·화이트 전환·설정 저장·수정된 아이콘 렌더링 검증을 통과했습니다. 그림 확인용 진행 화면은 UI 표본이며 실제 검사 성능 측정이 아닙니다.

기존 설치 앱을 자동으로 덮어쓰지 않았습니다. Windows 11 새 설치, 실제 GitHub 로그인, 다른 PC에서 검사와 제출, 무료 환경의 대형 보고서 처리 시험은 남아 있습니다.

## 로그인 연결 완료

2026-09-19에 GitHub Secret 설정과 실제 OAuth 로그인을 완료했습니다. 새 주소는 https://sentinel.sentinel-ac.workers.dev 이며 Junnior123 운영자 화면과 실서버 검사 코드 발급을 확인했습니다. 위의 OAuth 미설정 상태는 초기 배포 당시 기록입니다. 현재 검사 접수는 활성화됐고 Setup 공개와 타 PC 제출 시험은 남아 있습니다.

공개 소스: https://github.com/Junnior123/Sentinel · 배포 파일: https://github.com/Junnior123/Sentinel/releases/tag/v0.5.1
