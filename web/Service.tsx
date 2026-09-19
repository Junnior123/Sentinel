import {useEffect,useState} from 'react';
export type ServiceInfo={service:string;ready:boolean;environment:string;admissionsOpen:boolean;downloadUrl:string|null;retentionDays:number};
export class ApiError extends Error {constructor(message:string,public status:number){super(message)}}
export async function request<T>(path:string,method='GET',body?:unknown):Promise<T>{
 let response:Response;
 try{response=await fetch(path,{method,headers:body?{'Content-Type':'application/json'}:undefined,body:body?JSON.stringify(body):undefined,signal:AbortSignal.timeout(30000)});}
 catch{throw new ApiError('서버에 연결할 수 없습니다. 인터넷 연결을 확인하고 다시 시도해 주세요.',0);}
 let data:any;
 try{data=await response.json()}catch{throw new ApiError(response.status===429?'서비스 이용 한도에 도달했습니다. 잠시 후 다시 시도해 주세요.':'서버 응답을 읽을 수 없습니다. 잠시 후 다시 시도해 주세요.',response.status);}
 if(!response.ok)throw new ApiError(typeof data?.error==='string'?data.error:`요청에 실패했습니다 (${response.status}).`,response.status);
 return data as T;
}
export function useService(){
 const [service,setService]=useState<ServiceInfo|null>(null),[error,setError]=useState('');
 useEffect(()=>{let stale=false;request<ServiceInfo>('/api/health').then(s=>{if(!stale)setService(s)}).catch(e=>{if(!stale)setError(e.message)});return()=>{stale=true}},[]);
 return {service,error};
}
export function AppDownload(){
 const {service,error}=useService();
 if(error)return <p className="error" role="alert">{error}</p>;
 if(!service)return <span className="small muted">앱 다운로드 확인 중…</span>;
 return service.downloadUrl?<a className="primary" href={service.downloadUrl} rel="noreferrer">Windows 앱 설치 ↓</a>:<p className="small muted">{service.environment==='development'?'로컬 테스트 서버 · 설치 파일은 artifacts 폴더에 있습니다.':'앱 배포 준비 중입니다. 운영자에게 설치 파일을 요청하세요.'}</p>;
}
