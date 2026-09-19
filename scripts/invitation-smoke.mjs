import {chromium} from 'playwright';
import {readFile} from 'node:fs/promises';
import assert from 'node:assert/strict';
const browser=await chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'msedge'});
try {
 const page=await browser.newPage({viewport:{width:1280,height:900}});
 await page.goto('http://localhost:8787/dev/login');
 await page.getByRole('button',{name:'＋ 검사 만들기'}).click();
 await page.getByLabel('대상 닉네임').fill('초대 흐름 검증');
 await page.getByLabel('검사 사유').fill('플레이어 초대 링크와 파일 전달 확인');
 await page.getByRole('button',{name:'코드 발급',exact:true}).click();
 const input=page.getByLabel('플레이어 초대 링크');await input.waitFor();
 const link=await input.inputValue();assert.match(link,/^http:\/\/localhost:8787\/#scan=[A-Fa-f0-9]{12}$/);
 const downloading=page.waitForEvent('download');await page.getByRole('button',{name:'초대 파일 받기',exact:true}).click();
 const file=await downloading;await file.saveAs('artifacts/test.watchblock.json');
 const invite=JSON.parse(await readFile('artifacts/test.watchblock.json','utf8'));
 assert.equal(invite.origin,'http://localhost:8787');assert.equal(invite.code,link.split('=')[1]);assert.equal(invite.schemaVersion,1);
 await page.screenshot({path:'artifacts/web-invitation.png',fullPage:true});
 const player=await browser.newPage({viewport:{width:1000,height:850}});
 await player.goto(link);await player.getByRole('heading',{name:'검사 초대를 받았습니다'}).waitFor();
 assert.equal(await player.getByRole('link',{name:'GitHub로 로그인'}).count(),0);
 await player.screenshot({path:'artifacts/web-player-invitation.png',fullPage:true});
 console.log('PASS invitation creation, JSON download, code/origin match, player page without operator login');
}finally{await browser.close()}
