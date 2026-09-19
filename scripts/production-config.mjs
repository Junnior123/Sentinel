import {readFileSync} from 'node:fs';
export function validateProduction(config) {
  const errors=[];
  const v=config.vars??{};
  let origin;
  try { origin=new URL(v.PUBLIC_ORIGIN); } catch { errors.push('PUBLIC_ORIGIN에 실제 HTTPS 주소가 필요합니다.'); }
  if(origin&&(origin.protocol!=='https:'||origin.origin!==v.PUBLIC_ORIGIN||origin.username||origin.password||!origin.hostname.includes('.')||/localhost|\.invalid$|\.example$|^127\.|^0\./i.test(origin.hostname))) errors.push('PUBLIC_ORIGIN은 경로·마지막 슬래시 없는 공개 HTTPS 주소여야 합니다.');
  if(!/^[a-z][a-z0-9-]{1,62}$/.test(config.name??''))errors.push('Worker 이름을 확인하세요.');
  if(!/^[1-9][0-9]{0,19}$/.test(v.OWNER_GITHUB_ID??''))errors.push('GitHub 숫자 사용자 ID가 필요합니다.');
  if(!v.SERVICE_NAME?.trim())errors.push('서비스 이름이 필요합니다.');
  if(v.ENVIRONMENT!=='production')errors.push('ENVIRONMENT는 production이어야 합니다.');
  if(!['true','false'].includes(v.ADMISSIONS_OPEN))errors.push('ADMISSIONS_OPEN은 true 또는 false여야 합니다.');
  const db=config.d1_databases?.find(d=>d.binding==='DB');
  if(!db||!/^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i.test(db.database_id))errors.push('Cloudflare에서 생성한 D1 database_id가 필요합니다.');
  if(v.DOWNLOAD_URL&&!/^https:\/\/github\.com\/[A-Za-z0-9-]+\/[A-Za-z0-9_.-]+\/releases\/download\/[^/?#]+\/Sentinel-[0-9.]+-Setup-x64\.exe$/.test(v.DOWNLOAD_URL))errors.push('다운로드 주소는 GitHub Releases의 Sentinel Setup 파일이어야 합니다.');
  if(v.GITHUB_CLIENT_SECRET||v.GITHUB_CLIENT_ID)errors.push('OAuth 자격 증명은 config가 아닌 wrangler secret put으로 등록하세요.');
  return errors;
}
export function readProduction(path='wrangler.production.json') {
  let config;
  try { config=JSON.parse(readFileSync(path,'utf8')); } catch { throw new Error('먼저 npm run setup:production으로 실서버 설정을 만들어 주세요.'); }
  const errors=validateProduction(config);
  if(errors.length)throw new Error(errors.join('\n'));
  return config;
}
