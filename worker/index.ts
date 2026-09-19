import {z} from 'zod';
import {readSharedReport} from './sharing';
import {chunkSchema,scopeSchema,summarySchema} from '../shared/contracts';
export interface Env {DB:D1Database; ASSETS:Fetcher; PUBLIC_ORIGIN:string; OWNER_GITHUB_ID:string; SERVICE_NAME:string; ADMISSIONS_OPEN:string; GITHUB_CLIENT_ID:string; GITHUB_CLIENT_SECRET:string; ENVIRONMENT?:string; DOWNLOAD_URL?:string}
const now=()=>Math.floor(Date.now()/1000), day=86400;
export const hash=async(s:string)=>Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(s))),x=>x.toString(16).padStart(2,'0')).join('');
const token=()=>Array.from(crypto.getRandomValues(new Uint8Array(32)),x=>x.toString(16).padStart(2,'0')).join('');
const json=(data:unknown,status=200)=>Response.json(data,{status,headers:{'Cache-Control':'no-store','X-Content-Type-Options':'nosniff','Referrer-Policy':'no-referrer'}});
class HttpError extends Error {constructor(public status:number,message:string){super(message)}}
function fail(status:number,message:string):never{throw new HttpError(status,message)}
async function body(r:Request,max=262144){if(Number(r.headers.get('content-length'))>max)fail(413,'요청이 너무 큽니다.'); const reader=r.body?.getReader();if(!reader)fail(400,'본문이 없습니다.');let total=0;const parts:Uint8Array[]=[];try{while(true){const{done,value}=await reader.read();if(done)break;total+=value.length;if(total>max){await reader.cancel();fail(413,'요청이 너무 큽니다.')}parts.push(value)}}finally{reader.releaseLock()}const b=new Uint8Array(total);let i=0;for(const p of parts){b.set(p,i);i+=p.length}try{return JSON.parse(new TextDecoder().decode(b))}catch{fail(400,'잘못된 JSON입니다.')}}
const cookie=(r:Request,k:string)=>r.headers.get('cookie')?.split(';').map(x=>x.trim()).find(x=>x.startsWith(k+'='))?.slice(k.length+1);
const cookieHeader=(e:Env,k:string,v:string,age:number)=>`${k}=${v}; Path=/; HttpOnly; SameSite=Lax; Max-Age=${age}${e.PUBLIC_ORIGIN.startsWith('https:')?'; Secure':''}`;
async function audit(e:Env,actor:string,action:string,id:string|null=null){await e.DB.prepare('INSERT INTO audit(actor,action,scan_id,created) VALUES(?,?,?,?)').bind(actor,action,id,now()).run()}
async function rate(e:Env,key:string,limit:number,seconds:number){const bucket=Math.floor(now()/seconds);const r=await e.DB.prepare('INSERT INTO rate_limits(key,count,expires) VALUES(?,1,?) ON CONFLICT(key) DO UPDATE SET count=count+1 RETURNING count').bind(key+':'+bucket,now()+seconds*2).first<{count:number}>();if(!r||r.count>limit)fail(429,'요청이 많습니다. 잠시 후 다시 시도해 주세요.')}
async function operator(r:Request,e:Env){const t=cookie(r,'wb_session');if(!t)fail(401,'로그인이 필요합니다.');const user=await e.DB.prepare('SELECT o.* FROM auth_sessions a JOIN operators o ON o.id=a.operator_id WHERE a.token_hash=? AND a.expires>?').bind(await hash(t),now()).first<{id:string;login:string;role:string}>();if(!user)fail(401,'로그인이 만료되었습니다.');if(!['GET','HEAD'].includes(r.method)&&r.headers.get('origin')!==e.PUBLIC_ORIGIN)fail(403,'출처가 일치하지 않습니다.');return user}
async function scanFor(r:Request,e:Env,id:string,player=false){if(!/^[a-f0-9-]{36}$/.test(id))fail(404,'검사를 찾을 수 없습니다.');let s:any;if(player){const b=r.headers.get('authorization');if(!b?.startsWith('Bearer '))fail(401,'검사 토큰이 필요합니다.');s=await e.DB.prepare('SELECT * FROM scans WHERE id=? AND token_hash=? AND expires>?').bind(id,await hash(b.slice(7)),now()).first()}else{const u=await operator(r,e);s=await e.DB.prepare('SELECT * FROM scans WHERE id=? AND owner_id=? AND expires>?').bind(id,u.id,now()).first()}if(!s)fail(404,'검사를 찾을 수 없습니다.');return s}
const publicScan=(s:any)=>({id:s.id,nickname:s.nickname,reason:s.reason,scope:JSON.parse(s.scope),state:s.state,created:s.created,expires:s.expires,progress:s.progress?JSON.parse(s.progress):null,summary:s.summary?JSON.parse(s.summary):null,review:s.review?JSON.parse(s.review):null,chunkCount:s.chunk_count??0,receipt:s.receipt});
async function routes(r:Request,e:Env):Promise<Response>{
 const url=new URL(r.url),p=url.pathname;
 const development=e.ENVIRONMENT==='development';
 let origin:URL;try{origin=new URL(e.PUBLIC_ORIGIN)}catch{fail(503,'서비스 주소 설정이 필요합니다.')}
 if(!development&&(origin.protocol!=='https:'||origin.origin!==e.PUBLIC_ORIGIN||!origin.hostname.includes('.')||/localhost|\.invalid$|\.example$/.test(origin.hostname)||!/^\d+$/.test(e.OWNER_GITHUB_ID)))fail(503,'실서버 설정이 완료되지 않았습니다.');
 if(url.origin!==origin.origin)fail(421,'초대받은 서비스 주소로 접속해 주세요.');
 if(p.startsWith('/dev/'))fail(404,'요청을 찾을 수 없습니다.');
 if(p==='/api/health'&&r.method==='GET'){
  await e.DB.prepare('SELECT id FROM operators LIMIT 1').first();
  await e.DB.prepare('SELECT sections FROM report_shares LIMIT 1').first();
  const ready=development||Boolean(e.GITHUB_CLIENT_ID&&e.GITHUB_CLIENT_SECRET);
  const downloadUrl=/^https:\/\/github\.com\/[A-Za-z0-9-]+\/[A-Za-z0-9_.-]+\/releases\/download\/[^/?#]+\/Sentinel-[0-9.]+-Setup-x64\.exe$/.test(e.DOWNLOAD_URL||'')?e.DOWNLOAD_URL:null;
  return json({service:e.SERVICE_NAME,schemaVersion:1,admissionsOpen:ready&&e.ADMISSIONS_OPEN==='true',ready,environment:development?'development':'production',downloadUrl,retentionDays:7});
 }
 if(!development&&(!e.GITHUB_CLIENT_ID||!e.GITHUB_CLIENT_SECRET))fail(503,'GitHub 로그인 설정을 마쳐야 검사를 접수할 수 있습니다.');
 if(p.startsWith('/api/shared/')){await rate(e,'public-share:'+await hash(r.headers.get('cf-connecting-ip')||'local'),60,300);return readSharedReport(r,e,p.slice('/api/shared/'.length));}
 const shareMatch=/^\/api\/scans\/([a-f0-9-]{36})\/share$/.exec(p);
 if(shareMatch){
  const s=await scanFor(r,e,shareMatch[1]);
  if(r.method==='DELETE'){await e.DB.prepare('DELETE FROM report_shares WHERE scan_id=?').bind(s.id).run();await audit(e,s.owner_id,'revoke-share',s.id);return json({ok:true});}
  if(r.method==='POST'){
   if(!s.finalized)fail(409,'제출이 완료된 보고서만 공유할 수 있습니다.');
   const b=z.object({publishSummary:z.literal(true),hours:z.number().int().min(1).max(168),sections:z.array(z.enum(['files','findings','artifacts','coverage'])).max(4).default([])}).strict().parse(await body(r,1024));
   const key=token(),expires=Math.min(s.expires,now()+b.hours*3600);
   await e.DB.prepare('INSERT INTO report_shares(scan_id,token_hash,expires,sections) VALUES(?,?,?,?) ON CONFLICT(scan_id) DO UPDATE SET token_hash=excluded.token_hash,expires=excluded.expires,sections=excluded.sections').bind(s.id,await hash(key),expires,JSON.stringify([...new Set(b.sections)])).run();
   await audit(e,s.owner_id,b.sections.length?'publish-details':'publish-summary',s.id);return json({url:e.PUBLIC_ORIGIN+'/#report='+key,expires},201);
  }
  fail(405,'지원하지 않는 요청입니다.');
 }
 if(p==='/auth/github'&&r.method==='GET'){
  if(!e.GITHUB_CLIENT_ID||!e.GITHUB_CLIENT_SECRET||!e.OWNER_GITHUB_ID)fail(503,'GitHub 로그인 설정이 필요합니다.');
  await rate(e,'oauth:'+await hash(r.headers.get('cf-connecting-ip')||'local'),20,300);
  const state=token();await e.DB.prepare('INSERT INTO oauth_states(hash,expires) VALUES(?,?)').bind(await hash(state),now()+600).run();
  const u=new URL('https://github.com/login/oauth/authorize');u.search=new URLSearchParams({client_id:e.GITHUB_CLIENT_ID,redirect_uri:e.PUBLIC_ORIGIN+'/auth/callback',state,scope:'read:user'}).toString();
  return new Response(null,{status:302,headers:{Location:u.toString(),'Set-Cookie':cookieHeader(e,'wb_oauth',state,600)}});
 }
 if(p==='/auth/callback'&&r.method==='GET'){
  const state=url.searchParams.get('state');if(!state||state!==cookie(r,'wb_oauth'))fail(403,'로그인 요청이 일치하지 않습니다.');
  const used=await e.DB.prepare('DELETE FROM oauth_states WHERE hash=? AND expires>? RETURNING hash').bind(await hash(state),now()).first();if(!used)fail(403,'로그인 요청이 만료되었습니다.');
  const code=url.searchParams.get('code');if(!code)fail(400,'GitHub 승인이 취소되었습니다.');
  const response=await fetch('https://github.com/login/oauth/access_token',{method:'POST',headers:{Accept:'application/json','Content-Type':'application/json'},body:JSON.stringify({client_id:e.GITHUB_CLIENT_ID,client_secret:e.GITHUB_CLIENT_SECRET,code,redirect_uri:e.PUBLIC_ORIGIN+'/auth/callback'}),signal:AbortSignal.timeout(15000)});
  const data:any=await response.json();if(!response.ok||!data.access_token)fail(502,'GitHub 인증에 실패했습니다.');
  const ur=await fetch('https://api.github.com/user',{headers:{Authorization:`Bearer ${data.access_token}`,'User-Agent':'Watchblock',Accept:'application/vnd.github+json'},signal:AbortSignal.timeout(15000)});const gu:any=await ur.json();if(!ur.ok||!Number.isSafeInteger(gu.id)||typeof gu.login!=='string')fail(502,'GitHub 사용자를 확인할 수 없습니다.');
  const id=String(gu.id);if(id===e.OWNER_GITHUB_ID)await e.DB.prepare("INSERT INTO operators(id,login,role) VALUES(?,?,'owner') ON CONFLICT(id) DO UPDATE SET login=excluded.login,role='owner'").bind(id,gu.login).run();
  const o=await e.DB.prepare('SELECT * FROM operators WHERE id=?').bind(id).first();if(!o)fail(403,'초대된 운영자만 로그인할 수 있습니다.');await e.DB.prepare('UPDATE operators SET login=? WHERE id=?').bind(gu.login,id).run();
  const t=token();await e.DB.prepare('INSERT INTO auth_sessions(token_hash,operator_id,expires) VALUES(?,?,?)').bind(await hash(t),id,now()+day).run();
  await audit(e,id,'login');const headers=new Headers({Location:'/','Cache-Control':'no-store'});headers.append('Set-Cookie',cookieHeader(e,'wb_session',t,day));headers.append('Set-Cookie',cookieHeader(e,'wb_oauth','',0));return new Response(null,{status:302,headers});
 }
 if(p==='/api/me')return json(await operator(r,e));
 if(p==='/api/logout'&&r.method==='POST'){await operator(r,e);await e.DB.prepare('DELETE FROM auth_sessions WHERE token_hash=?').bind(await hash(cookie(r,'wb_session')!)).run();return new Response('{}',{headers:{'Set-Cookie':cookieHeader(e,'wb_session','',0),'Content-Type':'application/json'}})}
 if(p==='/api/operators'){
  const u=await operator(r,e);if(u.role!=='owner')fail(403,'설치자만 운영자를 관리할 수 있습니다.');
  if(r.method==='GET')return json((await e.DB.prepare('SELECT id,login,role FROM operators').all()).results);
  if(r.method==='POST'){const b=z.object({id:z.string().regex(/^\d{1,20}$/),login:z.string().regex(/^[A-Za-z0-9-]{1,39}$/)}).strict().parse(await body(r,4096));await e.DB.prepare("INSERT INTO operators(id,login,role,invited_by) VALUES(?,?,'reviewer',?) ON CONFLICT(id) DO NOTHING").bind(b.id,b.login,u.id).run();await audit(e,u.id,'invite');return json({ok:true})}
 }
 if(p==='/api/scans'){
  const u=await operator(r,e);
  if(r.method==='GET')return json((await e.DB.prepare('SELECT * FROM scans WHERE owner_id=? AND expires>? ORDER BY created DESC LIMIT 100').bind(u.id,now()).all()).results.map(publicScan));
  if(r.method==='POST'){
   if(e.ADMISSIONS_OPEN!=='true')fail(503,'현재 새 검사를 받지 않습니다.');await rate(e,'create:'+u.id,30,day);
   const b=z.object({nickname:z.string().trim().min(1).max(40),reason:z.string().trim().min(1).max(500),scope:scopeSchema}).strict().parse(await body(r,8192));if(!b.scope.gameFiles&&!b.scope.extraFolders&&!b.scope.executionTraces)fail(400,'검사 항목을 선택해 주세요.');
   const size=await e.DB.prepare('SELECT COALESCE(SUM(bytes),0) AS total FROM scans').first<{total:number}>();if((size?.total||0)>350*1024*1024)fail(503,'보관 용량이 부족합니다. 기존 보고서를 정리해 주세요.');
   const id=crypto.randomUUID(),code=token().slice(0,12).toUpperCase();await e.DB.prepare('INSERT INTO scans(id,owner_id,nickname,reason,scope,code_hash,code_expires,created,expires) VALUES(?,?,?,?,?,?,?,?,?)').bind(id,u.id,b.nickname,b.reason,JSON.stringify(b.scope),await hash(code),now()+900,now(),now()+7*day).run();await audit(e,u.id,'create',id);return json({id,code,expires:now()+900},201)
  }
 }
 if(p==='/api/claim'&&r.method==='POST'){
  await rate(e,'claim:'+await hash(r.headers.get('cf-connecting-ip')||'local'),15,300);
  const {code,capabilities=[]}=z.object({code:z.string().regex(/^[A-Fa-f0-9 -]{12,16}$/),capabilities:z.array(z.enum(['all-files','hardware-changes'])).max(2).optional()}).strict().parse(await body(r,1024));
  const pending=await e.DB.prepare("SELECT scope FROM scans WHERE code_hash=? AND code_expires>? AND state='created'").bind(await hash(code.replace(/[ -]/g,'').toUpperCase()),now()).first<{scope:string}>();
  if(pending){const scope=JSON.parse(pending.scope);if(scope.allFiles&&!capabilities.includes('all-files')||scope.hardwareChanges&&!capabilities.includes('hardware-changes'))fail(426,'이 검사에는 Watchblock 0.2 이상 앱이 필요합니다. 코드는 아직 사용되지 않았습니다.')}
  const t=token();
  const s=await e.DB.prepare("UPDATE scans SET token_hash=?,state='claimed' WHERE code_hash=? AND code_expires>? AND state='created' RETURNING *").bind(await hash(t),await hash(code.replace(/[ -]/g,'').toUpperCase()),now()).first<any>();if(!s)fail(404,'코드가 없거나 만료·사용되었습니다.');const op=await e.DB.prepare('SELECT login FROM operators WHERE id=?').bind(s.owner_id).first<any>();return json({session:publicScan(s),token:t,operator:op.login,service:e.SERVICE_NAME,origin:e.PUBLIC_ORIGIN});
 }
 const m=p.match(/^\/api\/(player|scans)\/([a-f0-9-]+)(?:\/(.*))?$/);
 if(m){const player=m[1]==='player',id=m[2],action=m[3]||'',s=await scanFor(r,e,id,player);
  if(action===''&&r.method==='GET'){await audit(e,player?'player':s.owner_id,'view',id);return json(publicScan(s))}
  if(action===''&&r.method==='DELETE'){await audit(e,player?'player':s.owner_id,'delete',id);await e.DB.prepare('DELETE FROM scans WHERE id=?').bind(id).run();return json({ok:true})}
  if(action==='progress'&&player&&r.method==='POST'){
   const b=z.object({stage:z.enum(['scanning','awaiting-submit','cancelled','declined','failed']),count:z.number().int().min(0).max(10000000)}).strict().parse(await body(r,1024));await rate(e,'progress:'+id,60,60);
   const changed=await e.DB.prepare("UPDATE scans SET progress=?,state=? WHERE id=? AND finalized=0 AND state IN ('claimed','scanning','awaiting-submit')").bind(JSON.stringify(b),b.stage,id).run();if(!changed.meta.changes)fail(409,'이미 종료되거나 제출 중인 검사입니다.');return json({ok:true})
  }
  const cm=action.match(/^chunks\/(\d+)$/);
  if(cm){const seq=Number(cm[1]);if(seq>1000)fail(400,'잘못된 페이지입니다.');
   if(r.method==='PUT'&&player){
    if(['cancelled','declined','failed'].includes(s.state))fail(409,'종료된 검사입니다.');
    const b=chunkSchema.parse(await body(r));for(const f of b.files)if(/^(?:[a-z]:|[\\/])|(?:^|[\\/])\.\.(?:[\\/]|$)/i.test(f.path))fail(400,'개인 절대 경로는 제출할 수 없습니다.');
    const data=JSON.stringify(b),h=await hash(data);const old=await e.DB.prepare('SELECT hash FROM chunks WHERE scan_id=? AND seq=?').bind(id,seq).first<any>();if(old){if(old.hash!==h)fail(409,'이미 제출한 페이지와 다릅니다.');return json({hash:h})}
    if(s.finalized||!['claimed','scanning','awaiting-submit','uploading'].includes(s.state))fail(409,'제출할 수 없는 상태입니다.');
    await e.DB.prepare("UPDATE scans SET state='uploading' WHERE id=? AND state IN ('claimed','scanning','awaiting-submit')").bind(id).run();
    await e.DB.prepare('INSERT INTO chunks(scan_id,seq,hash,data,bytes) VALUES(?,?,?,?,?)').bind(id,seq,h,data,new TextEncoder().encode(data).length).run();return json({hash:h});
   }
   if(r.method==='GET'){if(!player&&!s.finalized)fail(409,'보고서 제출이 완료되지 않았습니다.');const c=await e.DB.prepare('SELECT data,hash FROM chunks WHERE scan_id=? AND seq=?').bind(id,seq).first<any>();if(!c)fail(404,'페이지가 없습니다.');await audit(e,player?'player':s.owner_id,'view-chunk',id);return json({chunk:JSON.parse(c.data),hash:c.hash})}
  }
  if(action==='complete'&&player&&r.method==='POST'){
   const b=z.object({summary:summarySchema,hashes:z.array(z.string().regex(/^[a-f0-9]{64}$/)).min(1).max(1001)}).strict().parse(await body(r));
   const chunks=(await e.DB.prepare('SELECT seq,hash FROM chunks WHERE scan_id=? ORDER BY seq').bind(id).all<any>()).results;
   if(chunks.length!==b.hashes.length||chunks.some((c,i)=>c.seq!==i||c.hash!==b.hashes[i]))fail(409,'보고서 페이지가 누락되거나 일치하지 않습니다.');
   const receipt=await hash(JSON.stringify(b));if(s.finalized){if(s.receipt!==receipt)fail(409,'이미 다른 보고서로 완료되었습니다.');return json({receipt})}
   const result=await e.DB.prepare("UPDATE scans SET finalized=1,state='submitted',summary=?,chunk_count=?,receipt=? WHERE id=? AND finalized=0 AND state='uploading'").bind(JSON.stringify(b.summary),chunks.length,receipt,id).run();if(!result.meta.changes)fail(409,'제출 상태가 변경되었습니다.');await audit(e,'player','submit',id);return json({receipt});
  }
  if(action==='review'&&!player&&r.method==='POST'){if(!s.finalized)fail(409,'제출된 보고서만 검토할 수 있습니다.');const b=z.object({verdict:z.enum(['no-action','needs-review','policy-violation']),note:z.string().trim().min(1).max(4000)}).strict().parse(await body(r,16384));await e.DB.prepare('UPDATE scans SET review=?,reviewed_by=? WHERE id=?').bind(JSON.stringify(b),s.owner_id,id).run();await audit(e,s.owner_id,'review',id);return json({ok:true})}
 }
 if(p.startsWith('/api/')||p.startsWith('/auth/'))fail(404,'요청을 찾을 수 없습니다.');return e.ASSETS.fetch(r);
}
export default {
 async fetch(r:Request,e:Env){
  let response:Response;
  try{response=await routes(r,e)}catch(error){if(error instanceof HttpError)response=json({error:error.message},error.status);else if(error instanceof z.ZodError)response=json({error:'요청 형식이 올바르지 않습니다.'},400);else{console.error('Request failed',error instanceof Error?error.name:'Unknown');response=json({error:'서비스에 연결할 수 없습니다. 잠시 후 재시도해 주세요. 운영자는 DB 용량·무료 한도를 확인해 주세요.'},503)}}
  const secured=new Response(response.body,response);
  secured.headers.set('X-Frame-Options','DENY');secured.headers.set('X-Content-Type-Options','nosniff');secured.headers.set('Referrer-Policy','no-referrer');secured.headers.set('Cache-Control','no-store');secured.headers.set('X-Robots-Tag','noindex, nofollow');
  if(e.ENVIRONMENT!=='development')secured.headers.set('Strict-Transport-Security','max-age=31536000');
  if(secured.status===429)secured.headers.set('Retry-After','60');
  return secured;
 },
 async scheduled(_event:ScheduledController,e:Env){for(const [table,column] of [['scans','expires'],['auth_sessions','expires'],['oauth_states','expires'],['rate_limits','expires']] as const)await e.DB.prepare(`DELETE FROM ${table} WHERE ${column}<=?`).bind(now()).run();await e.DB.prepare('DELETE FROM audit WHERE created<?').bind(now()-7*day).run()}
};
