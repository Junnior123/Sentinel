import {chromium} from 'playwright';
const browser=await chromium.launch({headless:true,channel:process.env.PLAYWRIGHT_CHANNEL||'msedge'});
try {
 const page=await browser.newPage({viewport:{width:1380,height:950}});await page.goto('http://localhost:8787/dev/login');
 await page.getByRole('row').filter({hasText:'로컬 통합 검증'}).filter({hasText:'제출 완료'}).first().getByRole('button',{name:'열기'}).click();
 await page.getByText('공개 보고서 공유',{exact:true}).click();
 await page.getByRole('checkbox',{name:'탐지 근거',exact:true}).check();
 await page.getByRole('checkbox',{name:'전체 파일 목록',exact:true}).check();
 await page.getByRole('checkbox',{name:'실행·삭제 흔적',exact:true}).check();
 await page.getByRole('checkbox',{name:'검사 누락·범위',exact:true}).check();
 await page.getByLabel('링크를 가진 누구나 선택한 정보를 볼 수 있음을 확인했습니다.').check();
 await page.getByRole('button',{name:'공유 링크 만들기'}).click();
 const input=page.getByLabel('공개 보고서 링크');await input.waitFor();const url=await input.inputValue();
 const guest=await browser.newContext();const publicPage=await guest.newPage();await publicPage.goto(url);await publicPage.getByText('공개 검사 보고서',{exact:true}).waitFor();await publicPage.getByText('검사 파일',{exact:true}).waitFor();
 if((await publicPage.locator('body').innerText()).includes('로컬 통합 검증'))throw Error('Public summary leaked nickname');
 await publicPage.locator('.public-record').first().waitFor();
 await publicPage.locator('.public-record summary').first().click();
 await publicPage.screenshot({path:'artifacts/public-details.png',fullPage:true});
 await publicPage.getByRole('button',{name:'전체 파일 목록',exact:true}).click();
 await publicPage.locator('.public-record summary').first().click();
 await publicPage.getByText('SHA-256:',{exact:false}).first().waitFor();
 await publicPage.setViewportSize({width:390,height:844});
 if(await publicPage.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth+1))throw Error('Public report overflow');
 await page.getByRole('button',{name:'공유 중지'}).click();await page.getByText('공유 링크를 철회했습니다.').waitFor();
 await publicPage.reload();await publicPage.getByRole('alert').filter({hasText:'만료'}).waitFor();
 console.log('PASS real UI publish → anonymous detailed report → revoke');await guest.close();
}finally{await browser.close()}
