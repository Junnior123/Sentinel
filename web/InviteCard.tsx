import {ThemeButton} from './Theme';
import {useState} from 'react';
import {AppDownload} from './Service';
export function InviteCard({code,player=false}:{code:string;player?:boolean}) {
 const [message,setMessage]=useState('');
 const origin=location.origin;
 const link=`${origin}/#scan=${encodeURIComponent(code)}`;
 function download(name:string,data:unknown){
  try{const url=URL.createObjectURL(new Blob([JSON.stringify(data,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);setMessage('초대 파일을 플레이어에게 전달하세요.');}
  catch{setMessage('다운로드에 실패했습니다. 초대 링크를 복사해 주세요.');}
 }
 async function copy(value:string){try{await navigator.clipboard.writeText(value);setMessage('복사했습니다. 플레이어에게 전달하세요.')}catch{setMessage('복사 권한이 없습니다. 아래 링크를 선택해 복사하세요.')}}
 return <div className="invite-card"><div><span className="eyebrow">PLAYER INVITATION</span><h2>{player?'검사 초대를 받았습니다':'플레이어를 초대하세요'}</h2><p className="muted">{player?'Sentinel 앱을 열고 아래 초대를 붙여넣으세요.':'링크를 앱에 붙여넣으면 검사 서버가 자동으로 연결됩니다.'}</p></div><div className="invite-pin"><span>일회용 검사 코드 · 발급 후 15분 유효</span><strong>{code}</strong></div>{player&&<div className="invite-download"><AppDownload/></div>}<div className="invite-actions"><button className="primary" onClick={()=>copy(link)}>{player?'앱에 붙여넣을 초대 복사 →':'초대 링크 복사 →'}</button><button onClick={()=>download(`${code}.watchblock.json`,{schemaVersion:1,origin,code})}>초대 파일 받기</button><button onClick={()=>copy(code)}>코드만 복사</button></div><input aria-label="플레이어 초대 링크" readOnly value={link} onFocus={e=>e.target.select()}/><p role="status" className="small muted">{message||(player?'설치 → 초대 붙여넣기 → 범위 확인 → 검사':'플레이어: 앱 열기 → 초대 붙여넣기 → 범위 확인 → 검사')}</p>{!player&&<details><summary>서버 전용 앱으로 배포하기</summary><p className="small muted">설정 파일을 Sentinel.exe 옆에 넣어 함께 배포하면 플레이어는 코드만 입력합니다.</p><button onClick={()=>download('Sentinel.service.json',{origin})}>서버 설정 파일 받기</button></details>}</div>;
}
export function PlayerInvitation({code}:{code:string}){return <div className="app"><header><span className="brand"><span className="mark">▦</span> Sentinel</span><ThemeButton/></header><main style={{maxWidth:720,margin:'40px auto'}}><section className="panel"><InviteCard code={code} player/><p className="small muted">검사 서버: {location.origin}<br/>앱에서 운영자와 범위를 확인한 뒤 동의해야 검사가 시작됩니다.</p></section></main></div>}
