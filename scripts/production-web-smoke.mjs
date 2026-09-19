import {chromium} from 'playwright';
import assert from 'node:assert/strict';
const browser=await chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'msedge'});
try {
 const page=await browser.newPage({viewport:{width:1280,height:850}}),errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 const download='https://github.com/Junnior123/Sentinel/releases/download/v0.5.0/Sentinel-0.5.0-Setup-x64.exe';
 let mode='ready';
 await page.route('**/api/health',route=>route.fulfill({status:200,json:{ready:mode!=='unconfigured',environment:'production',service:'Test service',admissionsOpen:mode==='ready',downloadUrl:download,retentionDays:7}}));
 await page.route('**/api/me',route=>route.fulfill(mode==='outage'?{status:503,body:'Service Unavailable',contentType:'text/html'}:{status:401,json:{error:'로그인이 필요합니다.'}}));
 await page.goto('http://localhost:8787/');
 await page.getByRole('link',{name:'Windows 앱 설치'}).waitFor();
 assert.equal(await page.getByRole('link',{name:'Windows 앱 설치'}).getAttribute('href'),download);
 await page.screenshot({path:'artifacts/production-login.png',fullPage:true});
 await page.goto('http://localhost:8787/#scan=ABCDEF123456');
 await page.getByRole('link',{name:'Windows 앱 설치'}).waitFor();
 await page.getByRole('button',{name:'앱에 붙여넣을 초대 복사'}).waitFor();
 await page.setViewportSize({width:390,height:844});
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
 await page.evaluate(()=>Promise.all(document.getAnimations().filter(a=>Number.isFinite(a.effect?.getComputedTiming().endTime)).map(a=>a.finished.catch(()=>{}))));
 await page.screenshot({path:'artifacts/production-invite-mobile.png',fullPage:true});
 mode='outage';await page.goto('http://localhost:8787/');
 await page.getByRole('button',{name:'다시 연결'}).waitFor();
 assert.ok((await page.getByRole('alert').textContent()).includes('서버 응답'));
 mode='unconfigured';await page.reload();await page.getByText('운영자가 GitHub 로그인 설정을 준비 중입니다.').waitFor();
 assert.equal(await page.getByRole('link',{name:'GitHub로 로그인'}).count(),0);
 assert.deepEqual(errors,[]);
 console.log('PASS production download, player invitation, mobile, outage and unconfigured states (mock API)');
}finally{await browser.close();}
