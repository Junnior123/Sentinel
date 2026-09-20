import {createInterface} from 'node:readline/promises';
import {readFileSync,writeFileSync,existsSync,mkdirSync} from 'node:fs';
import {validateProduction} from './production-config.mjs';
const rl=createInterface({input:process.stdin,output:process.stdout});
async function ask(label,fallback=''){const answer=(await rl.question(`${label}${fallback?` [${fallback}]`:''}: `)).trim();return answer||fallback;}
try {
  console.log('공개 설정만 입력합니다. 비밀번호·OAuth Secret은 입력하지 마세요.');
  const file='wrangler.production.json';
  if(existsSync(file)&&(await ask('기존 배포 설정을 덮어쓸까요? yes 입력'))!=='yes')throw new Error('기존 설정을 유지했습니다.');
  const name=await ask('Cloudflare Worker 이름','sentinel');
  const origin=await ask('실제 서비스 HTTPS 주소 (예: https://sentinel.계정이름.workers.dev)');
  const databaseId=await ask('D1 생성 후 표시된 database_id');
  const login=await ask('설치자 GitHub 사용자명','Junnior123');
  if(!/^[A-Za-z0-9-]{1,39}$/.test(login))throw new Error('GitHub 사용자명을 확인하세요.');
  let ownerId='';
  try {
    const r=await fetch(`https://api.github.com/users/${encodeURIComponent(login)}`,{headers:{'User-Agent':'Sentinel-Setup',Accept:'application/vnd.github+json'},signal:AbortSignal.timeout(15000)});
    if(!r.ok)throw new Error('GitHub 조회 실패');
    const user=await r.json();
    if(!Number.isSafeInteger(user.id)||user.login?.toLowerCase()!==login.toLowerCase())throw new Error('계정 확인 실패');
    ownerId=String(user.id);console.log(`확인된 설치자: ${user.login} (ID ${ownerId})`);
  } catch { ownerId=await ask(`GitHub API 조회 실패. https://api.github.com/users/${login} 의 숫자 id`); }
  const serviceName=await ask('플레이어에게 표시할 서비스 이름','Sentinel');
  const download=await ask('게시 완료된 GitHub Releases Setup 다운로드 주소 (아직 없으면 Enter)');
  const config=JSON.parse(readFileSync('wrangler.jsonc','utf8'));
  config.name=name;config.workers_dev=true;config.preview_urls=false;
  config.d1_databases[0].database_id=databaseId;
  config.vars={PUBLIC_ORIGIN:origin,OWNER_GITHUB_ID:ownerId,SERVICE_NAME:serviceName,ADMISSIONS_OPEN:'true',ENVIRONMENT:'production',DOWNLOAD_URL:download};
  const errors=validateProduction(config);if(errors.length)throw new Error(errors.join('\n'));
  writeFileSync(file,JSON.stringify(config,null,2)+'\n');
  mkdirSync('artifacts/production',{recursive:true});
  writeFileSync('artifacts/production/Sentinel.service.json',JSON.stringify({origin},null,2)+'\n');
  console.log(`설정 저장 완료. OAuth callback: ${origin}/auth/callback\n다음 순서는 CONTRIBUTING.md를 확인하세요.`);
} catch(error){console.error(error.message);process.exitCode=1;} finally {rl.close();}
