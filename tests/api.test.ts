import{test}from'node:test';import assert from'node:assert/strict';import{DatabaseSync}from'node:sqlite';import{readFileSync}from'node:fs';import worker,{hash,type Env}from'../worker/index';
class SqlD1{db=new DatabaseSync(':memory:');constructor(){this.db.exec(readFileSync(new URL('../migrations/0002_report_shares.sql',import.meta.url),'utf8'));this.db.exec(readFileSync(new URL('../migrations/0001_initial.sql',import.meta.url),'utf8'));this.db.exec(readFileSync(new URL('../migrations/0003_share_details.sql',import.meta.url),'utf8'))}prepare(sql:string){const db=this.db;let values:any[]=[];const stmt={bind(...args:any[]){values=args;return stmt},async first(){return db.prepare(sql).get(...values)||null},async all(){return{results:db.prepare(sql).all(...values)}},async run(){const r=db.prepare(sql).run(...values);return{meta:{changes:Number(r.changes)}}}};return stmt}}
async function setup(){const db=new SqlD1();const env={DB:db as any,ENVIRONMENT:'development',PUBLIC_ORIGIN:'http://localhost:8787',OWNER_GITHUB_ID:'1',SERVICE_NAME:'Test',ADMISSIONS_OPEN:'true',GITHUB_CLIENT_ID:'',GITHUB_CLIENT_SECRET:'',ASSETS:{fetch:()=>new Response('UI')}}as unknown as Env;for(const id of ['1','2']){db.db.prepare('INSERT INTO operators VALUES(?,?,?,NULL)').run(id,'operator'+id,'owner');db.db.prepare('INSERT INTO auth_sessions VALUES(?,?,?)').run(await hash('session'+id),id,Math.floor(Date.now()/1000)+1000)}
 const call=async(path:string,method='GET',body?:unknown,auth='operator1')=>{const headers:Record<string,string>={'Content-Type':'application/json',Origin:env.PUBLIC_ORIGIN};if(auth.startsWith('operator'))headers.Cookie='wb_session=session'+auth.slice(8);else if(auth)headers.Authorization='Bearer '+auth;return worker.fetch(new Request(env.PUBLIC_ORIGIN+path,{method,headers,body:body===undefined?undefined:JSON.stringify(body)}),env)};
 const create=async()=>{const r=await call('/api/scans','POST',{nickname:'Tester',reason:'test',scope:{gameFiles:true,extraFolders:false,executionTraces:true,bannedModIds:[]}});assert.equal(r.status,201);return await r.json() as any};return{env,db,call,create};}
const chunk={files:[],artifacts:[],findings:[],coverage:[{collector:'files',status:'partial',reason:'fixture',inspected:0}]};
const summary={schemaVersion:1,ruleVersion:'test',completion:'partial',submission:'complete',started:new Date().toISOString(),finished:new Date().toISOString(),files:0,omitted:0,known:0,review:0,policy:0};
test('production validates origin, blocks missing OAuth and checks database readiness',async()=>{
 const {env,call,db}=await setup();env.ENVIRONMENT='production';
 assert.equal((await call('/api/health')).status,503);
 env.PUBLIC_ORIGIN='https://sentinel.test-account.workers.dev';
 const before=await call('/api/health');assert.equal(before.status,200);assert.equal((await before.json() as any).ready,false);
 assert.equal((await call('/api/scans')).status,503);
 env.GITHUB_CLIENT_ID='test';env.GITHUB_CLIENT_SECRET='not-published';
 env.DOWNLOAD_URL='https://github.com/Junnior123/Sentinel/releases/download/v0.5.0/Sentinel-0.5.0-Setup-x64.exe';
 const good=await call('/api/health');const info=await good.json() as any;
 assert.equal(info.ready,true);assert.equal(info.downloadUrl,env.DOWNLOAD_URL);assert.equal(info.environment,'production');
 assert.ok(!JSON.stringify(info).includes('not-published'));assert.equal(good.headers.get('X-Frame-Options'),'DENY');assert.ok(good.headers.get('Strict-Transport-Security'));
 assert.equal((await worker.fetch(new Request('https://other.workers.dev/api/health'),env)).status,421);
 assert.equal((await call('/dev/login')).status,404);
 env.DOWNLOAD_URL='javascript:alert(1)';assert.equal((await(await call('/api/health')).json() as any).downloadUrl,null);
 db.db.exec('DROP TABLE report_shares');assert.equal((await call('/api/health')).status,503);
});
test('report upload survives a lost final progress notification',async()=>{
 const {call,create}=await setup();const s=await create();const c=await(await call('/api/claim','POST',{code:s.code},'')).json() as any;const path='/api/player/'+s.id;
 await call(path+'/progress','POST',{stage:'scanning',count:0},c.token);
 const uploaded=await call(path+'/chunks/0','PUT',chunk,c.token);assert.equal(uploaded.status,200);const h=(await uploaded.json() as any).hash;
 const completion={summary,hashes:[h]};assert.equal((await call(path+'/complete','POST',completion,c.token)).status,200);
 assert.equal((await call(path+'/chunks/0','PUT',chunk,c.token)).status,200);
 assert.equal((await call(path+'/complete','POST',completion,c.token)).status,200);
});
test('public details are selected explicitly, paginated, masked and revoked with the summary',async()=>{
 const {call,create}=await setup();const s=await create();const c=await(await call('/api/claim','POST',{code:s.code},'')).json() as any;
 const fileId=crypto.randomUUID();const files=Array.from({length:101},(_,i)=>({id:i===0?fileId:crypto.randomUUID(),path:`선택폴더1/Users/private-person/mod-${i}.jar`,size:12,sha256:'a'.repeat(64),hashReason:null,format:'zip',status:'complete',signature:null,modIds:[]}));
 const report={...chunk,files,findings:[{ruleId:'fixture',fileId,category:'review',title:'Static evidence',evidence:['C:\\Users\\private-person\\test.exe','access_token=PRIVATE-TOKEN'],source:'https://example.com/rules'}],artifacts:[{source:'recycle-bin',path:'선택폴더1/sample.exe',time:null,fileId:null,association:'none',note:'휴지통 파일 · 내용 미확인'}]};
 const upload=await call('/api/player/'+s.id+'/chunks/0','PUT',report,c.token);assert.equal(upload.status,200);const h=(await upload.json() as any).hash;
 assert.equal((await call('/api/player/'+s.id+'/complete','POST',{summary:{...summary,files:101,review:1},hashes:[h]},c.token)).status,200);
 const share='/api/scans/'+s.id+'/share';
 const make=async(sections?:string[])=>{const r=await call(share,'POST',{publishSummary:true,hours:24,...(sections?{sections}:{})});assert.equal(r.status,201);return '/api/shared/'+(await r.json() as any).url.split('#report=')[1]};
 const legacy=await make();assert.equal((await call(legacy+'?section=files','GET',undefined,'')).status,403);
 const path=await make(['files','findings','artifacts']);
 assert.equal((await call(legacy,'GET',undefined,'')).status,404);
 assert.equal((await call(path+'?section=coverage','GET',undefined,'')).status,403);
 assert.equal((await call(path+'?section=files&page=-1','GET',undefined,'')).status,400);
 assert.equal((await call(path+'?section=files%27','GET',undefined,'')).status,403);
 const first=await(await call(path+'?section=files','GET',undefined,'')).json() as any;assert.equal(first.items.length,100);assert.equal(first.hasMore,true);assert.equal(first.items[0].sha256,'a'.repeat(64));assert.ok(!JSON.stringify(first).includes('private-person'));
 const second=await(await call(path+'?section=files&page=1','GET',undefined,'')).json() as any;assert.equal(second.items.length,1);assert.equal(second.hasMore,false);assert.notEqual(first.items[0].id,second.items[0].id);
 const evidence=await(await call(path+'?section=findings','GET',undefined,'')).json() as any;assert.equal(evidence.items[0].ruleId,'fixture');assert.ok(!JSON.stringify(evidence).includes('PRIVATE-TOKEN'));assert.ok(!JSON.stringify(evidence).includes('private-person'));
 assert.equal((await(await call(path+'?section=artifacts','GET',undefined,'')).json() as any).items[0].source,'recycle-bin');
 await call(share,'DELETE');assert.equal((await call(path+'?section=files','GET',undefined,'')).status,404);
});
test('seven-day sharing is capped by report retention',async()=>{const {call,create,db}=await setup();const s=await create();const expires=Math.floor(Date.now()/1000)+7200;db.db.prepare('UPDATE scans SET finalized=1,summary=?,expires=? WHERE id=?').run(JSON.stringify(summary),expires,s.id);const r=await call('/api/scans/'+s.id+'/share','POST',{publishSummary:true,hours:168});assert.equal(r.status,201);assert.equal((await r.json() as any).expires,expires);assert.equal((await call('/api/scans/'+s.id+'/share','POST',{publishSummary:true,hours:169})).status,400)});
test('public summary is opt-in, redacted, owner-only, rotated, expiring and revocable',async()=>{
 const {call,create,db}=await setup();const s=await create(),path='/api/scans/'+s.id+'/share';
 assert.equal((await call(path,'POST',{publishSummary:true,hours:24})).status,409);
 db.db.prepare('UPDATE scans SET finalized=1,state=?,summary=?,review=? WHERE id=?').run('submitted',JSON.stringify(summary),JSON.stringify({verdict:'needs-review',note:'PRIVATE NOTE'}),s.id);
 assert.equal((await call(path,'POST',{publishSummary:true,hours:24},'operator2')).status,404);
 assert.equal((await call(path,'POST',{hours:24})).status,400);
 const make=async()=>{const r=await call(path,'POST',{publishSummary:true,hours:24});assert.equal(r.status,201);return (await r.json() as any).url.split('#report=')[1]};
 const key=await make();const publicPath='/api/shared/'+key;const r=await call(publicPath,'GET',undefined,'');assert.equal(r.status,200);const data=await r.json() as any;
 assert.equal(data.completion,'partial');assert.equal(data.verdict,'needs-review');assert.deepEqual(Object.keys(data).sort(),['expires','files','known','review','policy','omitted','completion','submission','verdict'].sort());
 const next=await make();assert.equal((await call(publicPath,'GET',undefined,'')).status,404);
 await call(path,'DELETE');assert.equal((await call('/api/shared/'+next,'GET',undefined,'')).status,404);
 const expired=await make();db.db.prepare('UPDATE report_shares SET expires=0').run();assert.equal((await call('/api/shared/'+expired,'GET',undefined,'')).status,404);
 const deleted=await make();await call('/api/scans/'+s.id,'DELETE');assert.equal((await call('/api/shared/'+deleted,'GET',undefined,'')).status,404);
});
test('operator → one-use claim → upload → receipt → review → deletion',async()=>{const{call,create}=await setup();const s=await create();const claim=await(await call('/api/claim','POST',{code:s.code},'')).json() as any;const t=claim.token;assert.equal((await call('/api/claim','POST',{code:s.code},'')).status,404);const p='/api/player/'+s.id;const uploaded=await(await call(p+'/chunks/0','PUT',chunk,t)).json()as any;assert.ok(uploaded.hash);assert.equal((await call(p+'/chunks/0','PUT',chunk,t)).status,200);assert.equal((await call(p+'/complete','POST',{summary,hashes:[uploaded.hash]},t)).status,200);assert.equal((await call('/api/scans/'+s.id+'/review','POST',{verdict:'needs-review',note:'not proof'})).status,200);assert.equal((await call(p,'DELETE',undefined,t)).status,200);assert.equal((await call(p,'GET',undefined,t)).status,404)});
test('operators cannot read or delete another operator scan',async()=>{const{call,create}=await setup();const s=await create();for(const verb of ['GET','DELETE'])assert.equal((await call('/api/scans/'+s.id,verb,undefined,'operator2')).status,404)});
test('expired code and token denied',async()=>{const{call,create,db}=await setup();const s=await create();db.db.prepare('UPDATE scans SET code_expires=0').run();assert.equal((await call('/api/claim','POST',{code:s.code},'')).status,404);assert.equal((await call('/api/player/'+s.id,'GET',undefined,'invalid')).status,404)});
test('CSRF, missing auth, bad schema, private paths rejected',async()=>{const{call,create,env}=await setup();assert.equal((await call('/api/scans','GET',undefined,'')).status,401);assert.equal((await worker.fetch(new Request(env.PUBLIC_ORIGIN+'/api/scans',{method:'POST',headers:{Cookie:'wb_session=session1',Origin:'https://evil.example'}}),env)).status,403);assert.equal((await call('/api/scans','POST',{})).status,400);const s=await create();const c=await(await call('/api/claim','POST',{code:s.code},'')).json()as any;const file={id:crypto.randomUUID(),path:'C:/Users/private/mod.jar',size:0,sha256:null,hashReason:'fixture',format:'zip',status:'skipped',signature:null,modIds:[]};assert.equal((await call('/api/player/'+s.id+'/chunks/0','PUT',{...chunk,files:[file]},c.token)).status,400)});
test('missing/replaced/replayed chunks and closed-state uploads rejected',async()=>{const{call,create}=await setup();const s=await create();const c=await(await call('/api/claim','POST',{code:s.code},'')).json()as any;const p='/api/player/'+s.id;const h=(await(await call(p+'/chunks/1','PUT',chunk,c.token)).json()as any).hash;assert.equal((await call(p+'/complete','POST',{summary,hashes:[h]},c.token)).status,409);assert.equal((await call(p+'/chunks/1','PUT',{...chunk,coverage:[]},c.token)).status,409);assert.equal((await call(p+'/progress','POST',{stage:'scanning',count:0},c.token)).status,409)});
test('code rate limit and admission switch',async()=>{const{call,env}=await setup();for(let i=0;i<15;i++)await call('/api/claim','POST',{code:'ABCDEFABCDEF'},'');assert.equal((await call('/api/claim','POST',{code:'ABCDEFABCDEF'},'')).status,429);env.ADMISSIONS_OPEN='false';assert.equal((await call('/api/scans','POST',{})).status,503)});
test('expiry purges reports and chunks',async()=>{const{call,create,env,db}=await setup();const s=await create();const c=await(await call('/api/claim','POST',{code:s.code},'')).json()as any;await call('/api/player/'+s.id+'/chunks/0','PUT',chunk,c.token);db.db.prepare('UPDATE scans SET expires=0').run();await worker.scheduled({}as any,env);assert.equal((db.db.prepare('SELECT count(*) n FROM chunks').get()as any).n,0)});
test('10MB limit enforced atomically in database',async()=>{const{db,create}=await setup();const s=await create();db.db.prepare("UPDATE scans SET state='uploading',bytes=10485760 WHERE id=?").run(s.id);assert.throws(()=>db.db.prepare('INSERT INTO chunks VALUES(?,?,?,?,?)').run(s.id,0,'hash','{}',2),/report_limit_or_closed/)});
test('expanded scope requires capable app without consuming code; system findings accepted',async()=>{const{call,create,db}=await setup();const s=await create();db.db.prepare('UPDATE scans SET scope=? WHERE id=?').run(JSON.stringify({gameFiles:true,extraFolders:true,executionTraces:true,allFiles:true,hardwareChanges:true,bannedModIds:[]}),s.id);assert.equal((await call('/api/claim','POST',{code:s.code},'')).status,426);const c=await(await call('/api/claim','POST',{code:s.code,capabilities:['all-files','hardware-changes']},'')).json()as any;assert.ok(c.token);const body={...chunk,findings:[{ruleId:'hardware-baseline-change',fileId:null,category:'review',title:'시스템 변경',evidence:['로컬 기준 변경, 원인 미확인'],source:'https://learn.microsoft.com/'}]};assert.equal((await call('/api/player/'+s.id+'/chunks/0','PUT',body,c.token)).status,200)});
