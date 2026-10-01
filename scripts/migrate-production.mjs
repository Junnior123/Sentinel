import {spawnSync} from 'node:child_process';
import {createRequire} from 'node:module';
import {readProduction} from './production-config.mjs';
try {
 const config=readProduction();
 if(config.vars.DB_PROVIDER==='turso') {
  console.log('Turso를 사용하므로 D1을 수정하지 않습니다. 새 SQL은 Turso에 적용한 뒤 배포하세요.');
 } else {
  const require=createRequire(import.meta.url);
  const result=spawnSync(process.execPath,[require.resolve('wrangler/bin/wrangler.js'),'d1','migrations','apply','DB','--remote','--config','wrangler.production.json'],{stdio:'inherit'});
  if(result.error)throw result.error;
  if(result.status!==0)throw new Error('D1 마이그레이션에 실패했습니다.');
 }
} catch(error) {console.error(error instanceof Error?error.message:'DB 준비에 실패했습니다.');process.exitCode=1;}
