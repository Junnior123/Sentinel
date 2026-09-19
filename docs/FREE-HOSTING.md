# 무료 호스팅 비교 · 2026-09-18

**선택: Cloudflare Workers + Static Assets + D1.** Windows 설치 파일은 GitHub Releases로 배포합니다. 기존 서버와 DB 코드를 유지하면서 개인 PC를 꺼도 외부 접속을 받을 수 있는 구성이기 때문입니다. 모든 무료 서비스의 완전한 목록이 아니라, 이 앱에 적용 가능한 주요 13개 서비스를 비교했습니다. 아래 적합성 판단은 공식 조건과 현재 코드 구조를 바탕으로 한 판단이며 서비스별 실측 부하 시험 결과는 아닙니다.

| 서비스 | 현재 무료 조건·제약 | 이 프로젝트에 대한 판단 |
| --- | --- | --- |
| **Cloudflare Workers + D1** | Worker 하루 10만 요청·CPU 10ms. 정적 파일 요청 무료·무제한. D1 하루 500만 행 읽기·10만 행 쓰기, 무료 DB 하나당 500MB | **선택.** 기존 Worker/D1 코드 그대로 사용. 무료 한도 초과 시 중단 가능. [Workers 한도](https://developers.cloudflare.com/workers/platform/limits/), [정적 파일](https://developers.cloudflare.com/workers/static-assets/billing-and-limitations/), [D1 요금](https://developers.cloudflare.com/d1/platform/pricing/), [D1 한도](https://developers.cloudflare.com/d1/platform/limits/) |
| Vercel Hobby | 무료 Hobby는 개인·비상업적 사용에 한정 | 조건에 맞는 개인 프로젝트는 가능하지만 API/DB 이전 필요. [공식 조건](https://vercel.com/docs/plans/hobby) |
| Netlify Free | 신규 계정 월 300 credits, hard limit, 무료 플랜 자동 충전 없음 | 가능하지만 함수·DB 변경과 크레딧 관리 필요. [요금표](https://docs.netlify.com/manage/accounts-and-billing/billing/billing-for-credit-based-plans/credit-based-pricing-plans/) |
| Render Free | 웹 15분 무접속 시 정지, 재시작 약 1분. 무료 PostgreSQL 30일 후 만료 | 지속 운영 DB 용도로 부적합. [공식 제한](https://render.com/docs/free) |
| Railway Free | 첫 30일 $5 시험 크레딧, 이후 월 $1 무료 크레딧 | 소형 시험에는 가능. 이 크레딧으로 서버·DB 상시 운영을 보장하기 어려움. [공식 안내](https://docs.railway.com/pricing/free-trial) |
| Koyeb Free | 512MB·0.1 vCPU 인스턴스, 1시간 무접속 시 scale-to-zero 강제 | 무료 인스턴스는 공식적으로 프로덕션 비권장. [인스턴스](https://www.koyeb.com/docs/reference/instances), [중지 조건](https://www.koyeb.com/docs/run-and-scale/scale-to-zero) |
| Fly.io | 무료 시험 7일 또는 총 VM 2시간 중 먼저 도달한 때 종료 | 장기 무료 운영 후보에서 제외. [시험 조건](https://fly.io/docs/about/free-trial/) |
| Supabase Free | 500MB DB, 저활동 프로젝트 7일 후 일시 정지 가능 | DB·인증 후보지만 웹 호스팅 별도, D1 코드 변경 필요. [요금](https://supabase.com/pricing), [정지 정책](https://supabase.com/docs/guides/platform/free-project-pausing) |
| Firebase | App Hosting은 Blaze 결제 플랜 필요, 무료 사용량 제공 | Spark 정적 호스팅만으로 현재 API를 운영할 수 없음. [App Hosting 비용](https://firebase.google.com/docs/app-hosting/costs) |
| Google Cloud Run | 무료 사용량 제공, Cloud Billing 계정 필요 | 운영은 가능하나 초과 사용 과금과 별도 DB 관리 필요. [Cloud Run](https://cloud.google.com/run/pricing), [무료 이용 조건](https://docs.cloud.google.com/free/docs/free-cloud-features) |
| AWS | 신규 Free Plan 최대 6개월·최대 $200 크레딧, 일부 서비스 Always Free | 장기 무료 구성은 서비스 조합별 계산 필요. 현재 프로젝트보다 설정 복잡. [공식 FAQ](https://aws.amazon.com/free/free-tier-faqs/) |
| Oracle Cloud Always Free | 무료 VM 제공. 지역 용량 부족·유휴 인스턴스 회수 가능 | 가능하지만 서버 업데이트·백업·보안을 직접 관리. [공식 조건](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm) |
| GitHub Pages | 정적 웹 호스팅. 서버 API/DB 기능 없음 | 이 서비스 단독 운영 불가. 앱 설치 파일용 **Releases**는 별도로 사용. [Pages 소개](https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages) |

## 24시간 운영의 의미

Cloudflare가 웹과 API를 실행하고 D1이 보고서를 보관합니다. 내 PC에서 터미널이나 Sentinel을 계속 켜 두지 않아도 웹은 접속을 받습니다. 플레이어 PC의 검사 앱은 검사·제출할 때만 실행합니다. 주기적으로 사이트에 접속시켜 깨우는 프로그램도 필요하지 않습니다. [Workers 작동 방식](https://developers.cloudflare.com/workers/reference/how-workers-works/)

무료 플랜은 무중단 보증이나 무제한 용량이 아닙니다. 요청·DB·CPU 한도 또는 장애로 요청이 실패할 수 있습니다. 사이트 화면이 열려도 보고서 API가 한도에 도달했을 수 있습니다. 유료 전환 없이 Free 플랜을 유지하고, 대시보드에서 사용량을 확인합니다.

현재 설치 파일은 25MiB보다 크므로 Workers 정적 파일에 넣지 않습니다. GitHub Releases에 올린 실제 Setup 파일 주소를 웹 설정에 연결합니다. 코드와 보고서는 Cloudflare에, 설치 파일은 GitHub에 두는 구성입니다.

배포 순서는 [DEPLOY.md](DEPLOY.md)를 따르세요.
