import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createClient} from '@libsql/client';
import {readFileSync} from 'node:fs';
import {connectDatabase,libsqlDatabase} from '../worker/database';
import worker from '../worker/index';

test('libSQL adapter preserves parameters, changes, constraints and cascading deletion',async()=>{
 const client=createClient({url:':memory:'});
 try{
  for(const file of ['0001_initial.sql','0002_report_shares.sql','0003_share_details.sql','0004_evidence_indexes.sql'])await client.executeMultiple(readFileSync(new URL('../migrations/'+file,import.meta.url),'utf8'));
  const db=libsqlDatabase(client);
  await db.prepare('INSERT INTO operators(id,login,role) VALUES(?,?,?)').bind('1',"quote'한글",'owner').run();
  assert.equal((await db.prepare('SELECT login FROM operators WHERE id=?').bind('1').first<{login:string}>())?.login,"quote'한글");
  assert.equal(await db.prepare('SELECT id FROM operators WHERE id=?').bind('missing').first(),null);
  const stmt=db.prepare('SELECT id FROM operators WHERE id=?');
  assert.equal(await stmt.bind('2').first(),null);assert.equal((await stmt.bind('1').all()).results.length,1);
  await db.prepare('INSERT INTO scans(id,owner_id,nickname,reason,scope,code_hash,code_expires,created,expires,state) VALUES(?,?,?,?,?,?,?,?,?,?)').bind('scan','1','test','test','{}','hash',1,1,2,'uploading').run();
  assert.equal((await db.prepare('INSERT INTO chunks VALUES(?,?,?,?,?)').bind('scan',0,'hash','{}',2).run()).meta.changes,1);
  assert.equal((await db.prepare('SELECT bytes FROM scans').first<{bytes:number}>())?.bytes,2);
  await assert.rejects(db.prepare('INSERT INTO chunks VALUES(?,?,?,?,?)').bind('scan',1,'hash','{}',10485760).run(),/report_limit_or_closed/);
  assert.equal((await db.prepare('DELETE FROM scans WHERE id=?').bind('scan').run()).meta.changes,1);
  assert.equal((await db.prepare('SELECT count(*) n FROM chunks').first<{n:number}>())?.n,0);
  await assert.rejects(db.prepare('INSERT INTO chunks VALUES(?,?,?,?,?)').bind('missing',0,'hash','{}',2).run(),/FOREIGN KEY/);
 }finally{client.close()}
});

test('Turso configuration fails closed and never switches silently to D1',()=>{
 const DB={prepare(){throw new Error('D1 must not be queried')}} as any;
 assert.throws(()=>connectDatabase({DB,DB_PROVIDER:'turso'}),/credentials/);
 assert.throws(()=>connectDatabase({DB,DB_PROVIDER:'unknown'}),/provider/);
 for(const url of ['http://db.turso.io','https://evil.test','https://db.turso.io@evil.test','https://db.turso.io/path','https://db.turso.io?tls=0'])assert.throws(()=>connectDatabase({DB_PROVIDER:'turso',TURSO_DATABASE_URL:url,TURSO_AUTH_TOKEN:'test'}),/endpoint/);
 assert.equal(connectDatabase({DB}).db,DB);
});

test('maintenance stops every API write/read and scheduled purge before accessing DB',async()=>{
 const env={MAINTENANCE:'true',PUBLIC_ORIGIN:'https://sentinel.test',DB:{prepare(){throw new Error('unexpected DB access')}},ASSETS:{fetch(){return new Response('UI')}},ENVIRONMENT:'production'} as any;
 for(const [path,method] of [['/api/health','GET'],['/api/claim','POST'],['/api/player/id/chunks/0','PUT'],['/auth/callback','GET']]){
  const response=await worker.fetch(new Request('https://sentinel.test'+path,{method}),env);assert.equal(response.status,503);assert.match(await response.text(),/이전 중/);
 }
 await worker.scheduled({} as any,env);
});
