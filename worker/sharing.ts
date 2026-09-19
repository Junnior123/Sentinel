import type {Env} from './index';
import {hash} from './index';
const response=(data:unknown,status=200)=>Response.json(data,{status,headers:{'Cache-Control':'no-store','Referrer-Policy':'no-referrer','X-Robots-Tag':'noindex, nofollow','X-Content-Type-Options':'nosniff'}});
export async function readSharedReport(request:Request,env:Env,key:string){
 if(request.method!=='GET')return response({error:'지원하지 않는 요청입니다.'},405);
 if(!/^[a-f0-9]{64}$/.test(key))return response({error:'공유 링크가 없거나 만료되었습니다.'},404);
 const now=Math.floor(Date.now()/1000);
 const row=await env.DB.prepare('SELECT s.id,s.summary,s.review,r.expires,r.sections FROM report_shares r JOIN scans s ON s.id=r.scan_id WHERE r.token_hash=? AND r.expires>? AND s.expires>? AND s.finalized=1').bind(await hash(key),now,now).first<{id:string;summary:string;review:string|null;expires:number;sections:string}>();
 if(!row)return response({error:'공유 링크가 없거나 만료되었습니다.'},404);
 const summary=JSON.parse(row.summary),review=row.review?JSON.parse(row.review):null;
 const allowed=['files','findings','artifacts','coverage'];
 const sections=(JSON.parse(row.sections) as string[]).filter(s=>allowed.includes(s));
 const params=new URL(request.url).searchParams,section=params.get('section');
 if(section!==null){
  if(!sections.includes(section))return response({error:'공개하지 않은 항목입니다.'},403);
  const page=params.get('page')??'0';if(!/^\d{1,4}$/.test(page))return response({error:'잘못된 페이지입니다.'},400);
  // Section is allowlisted above. Query only this report; read at most 101 records per response.
  const result=await env.DB.prepare(`SELECT j.value FROM chunks c,json_each(c.data,'$.${section}') j WHERE c.scan_id=? ORDER BY c.seq,CAST(j.key AS INTEGER) LIMIT 101 OFFSET ?`).bind(row.id,Number(page)*100).all<{value:string}>();
  const items=result.results.slice(0,100).map(r=>project(section,JSON.parse(r.value)));
  return response({items,hasMore:result.results.length>100,page:Number(page)});
 }
 // Deliberate allowlist: no identity, paths, free text, scan ID, hashes or access tokens.
 return response({expires:row.expires,files:summary.files,known:summary.known,review:summary.review,policy:summary.policy,omitted:summary.omitted,completion:summary.completion,submission:summary.submission,verdict:review?.verdict??null,...(sections.length?{sections,ruleVersion:clean(summary.ruleVersion),started:summary.started,finished:summary.finished}:{})});
}

// Public output is a separate projection, never a stored chunk or operator session object.
function clean(value:unknown):string {return String(value??'').slice(0,1024).replace(/[\u0000-\u001f]/g,' ').replace(/Users[\\/][^\\/]+/gi,'Users/[사용자]').replace(/[A-Z]:[\\/][^\s;"<>]+/gi,'[로컬 경로]').replace(/\\\\[^\s;"<>]+/g,'[네트워크 경로]').replace(/((?:access_token|authorization|password|token)\s*[:=]\s*)[^\s;&]+/gi,'$1[비공개]');}
function project(section:string,v:any):unknown {
 switch(section){
  case 'files':return {id:v.id,path:clean(v.path),size:v.size,sha256:v.sha256,hashReason:clean(v.hashReason),format:clean(v.format),status:v.status,signature:clean(v.signature),modIds:v.modIds.map(clean)};
  case 'findings':return {ruleId:clean(v.ruleId),fileId:v.fileId,category:v.category,title:clean(v.title),evidence:v.evidence.map(clean),source:/^https:\/\/[^?#]+$/.test(v.source)?v.source:null};
  case 'artifacts':return {source:v.source,path:clean(v.path),time:v.time,fileId:v.fileId,association:v.association,note:clean(v.note)};
  default:return {collector:clean(v.collector),status:v.status,reason:clean(v.reason),inspected:v.inspected};
 }
}
