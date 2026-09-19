import {test} from 'node:test';
import assert from 'node:assert/strict';
import {validateProduction} from '../scripts/production-config.mjs';
const good=()=>({name:'sentinel',vars:{PUBLIC_ORIGIN:'https://sentinel.account.workers.dev',OWNER_GITHUB_ID:'123',SERVICE_NAME:'Sentinel',ENVIRONMENT:'production',ADMISSIONS_OPEN:'true',DOWNLOAD_URL:''},d1_databases:[{binding:'DB',database_id:'12345678-1234-1234-1234-123456789abc'}]});
test('production config blocks local origins, placeholder databases and embedded secrets',()=>{
 assert.deepEqual(validateProduction(good()),[]);
 for(const origin of ['http://localhost:8787','https://localhost','https://sentinel.example','https://site.workers.dev/path','https://site.workers.dev/','https://user:secret@site.workers.dev','https://site.invalid']){const c=good();c.vars.PUBLIC_ORIGIN=origin;assert.ok(validateProduction(c).length,origin);}
 const c=good();c.d1_databases[0].database_id='REPLACE_WITH_D1_ID';assert.ok(validateProduction(c).length);
 const secret=good();secret.vars.GITHUB_CLIENT_SECRET='private';assert.ok(validateProduction(secret).length);
});
test('installer links are restricted to explicit GitHub release Setup assets',()=>{
 const c=good();c.vars.DOWNLOAD_URL='https://github.com/Junnior123/Sentinel/releases/download/v0.5.0/Sentinel-0.5.0-Setup-x64.exe';assert.deepEqual(validateProduction(c),[]);
 for(const url of ['javascript:alert(1)','https://evil.example/app.exe','https://github.com.evil.test/a/b/releases/download/v1/Sentinel-0.5.0-Setup-x64.exe']){c.vars.DOWNLOAD_URL=url;assert.ok(validateProduction(c).length);}
});
