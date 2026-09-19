import type{ReportSummary}from'../shared/contracts';
export function ReportHero({summary,nickname,id}:{summary?:ReportSummary;nickname:string;id:string}){
 const tone=!summary?'pending':summary.known>0?'detected':summary.review>0||summary.policy>0||summary.completion!=='complete'?'warning':'clear';
 const verdict={pending:'검사 대기',detected:'치트 프로그램 탐지',warning:'검토가 필요합니다',clear:'탐지 없음'}[tone];
 const seconds=summary?Math.max(0,Math.round((Date.parse(summary.finished)-Date.parse(summary.started))/1000)):0;
 return <div className={`reporthero ${tone}`}><div><span className="eyebrow">SCAN REPORT</span><h2>{nickname}</h2><div className="reportmeta"><span>검사 ID<b>{id.slice(0,8).toUpperCase()}</b></span><span>소요 시간<b>{summary?`${Math.floor(seconds/60)}분 ${seconds%60}초`:'—'}</b></span><span>검사 파일<b>{summary?.files.toLocaleString()??'—'}</b></span></div></div><div className="verdict"><span className="verdicticon">◇</span><strong>{verdict}</strong><small>{summary?'파일 검사 결과 · 게임 내 실제 사용은 별도 확인':'플레이어가 검사를 완료하면 결과가 표시됩니다'}</small></div></div>
}
