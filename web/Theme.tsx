import {useEffect,useState} from 'react';
type Mode='dark'|'light';
function initial():Mode{try{return localStorage.getItem('sentinel-theme')==='light'?'light':'dark'}catch{return 'dark'}}
document.documentElement.dataset.theme=initial();
export function ThemeButton(){
 const [mode,setMode]=useState<Mode>(()=>document.documentElement.dataset.theme==='light'?'light':initial());
 useEffect(()=>{document.documentElement.dataset.theme=mode;},[mode]);
 function toggle(){const next=mode==='dark'?'light':'dark';setMode(next);try{localStorage.setItem('sentinel-theme',next)}catch{/* The theme still changes when browser storage is unavailable. */}}
 return <button className="theme-toggle" type="button" onClick={toggle} aria-label={mode==='dark'?'화이트 모드로 전환':'다크 모드로 전환'} title={mode==='dark'?'화이트 모드로 전환':'다크 모드로 전환'}><svg viewBox="0 0 24 24" width="17" height="17" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="1.6">{mode==='dark'?<><circle cx="12" cy="12" r="4"/><path d="M12 2v2m0 16v2M2 12h2m16 0h2M5 5l1.5 1.5m11 11L19 19M5 19l1.5-1.5m11-11L19 5"/></>:<path d="M20 15.5A8.5 8.5 0 0 1 8.5 4 8.5 8.5 0 1 0 20 15.5Z"/>}</svg><span>{mode==='dark'?'화이트 모드':'다크 모드'}</span></button>;
}
