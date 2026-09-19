import {chromium} from 'playwright';
import assert from 'node:assert/strict';
const browser=await chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'msedge'});
try{
 const page=await browser.newPage({viewport:{width:1360,height:900},reducedMotion:'reduce'}),errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://localhost:8787/dev/login');
 await page.getByRole('heading',{name:'검사 관리',exact:true}).waitFor();
 await page.screenshot({path:'artifacts/web-theme-dark.png',fullPage:true});
 await page.getByRole('button',{name:'화이트 모드로 전환'}).click();
 assert.equal(await page.evaluate(()=>getComputedStyle(document.body).backgroundColor),'rgb(244, 244, 245)');
 await page.screenshot({path:'artifacts/web-theme-light.png',fullPage:true});
 await page.reload();await page.getByRole('button',{name:'다크 모드로 전환'}).waitFor();
 await page.goto('http://localhost:8787/#scan=ABCDEF123456');await page.getByRole('button',{name:'앱에 붙여넣을 초대 복사'}).waitFor();
 assert.equal(await page.evaluate(()=>document.documentElement.dataset.theme),'light');
 await page.setViewportSize({width:390,height:844});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
 await page.screenshot({path:'artifacts/web-invite-light.png',fullPage:true});
 await page.getByRole('button',{name:'다크 모드로 전환'}).click();
 assert.equal(await page.evaluate(()=>getComputedStyle(document.body).backgroundColor),'rgb(20, 20, 22)');
 assert.deepEqual(errors,[]);console.log('PASS web gray dark/light, reload persistence, invitation route, 390px layout');
}finally{await browser.close();}
