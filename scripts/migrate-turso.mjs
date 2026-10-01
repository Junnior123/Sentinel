import {createClient} from '@libsql/client/web';
import {DatabaseSync} from 'node:sqlite';
import {readFileSync,writeFileSync} from 'node:fs';
import {createHash} from 'node:crypto';

const tables=['operators','auth_sessions','oauth_states','scans','chunks','audit','rate_limits','report_shares'];
const identifier=value=>{if(!/^[a-z_][a-z_0-9]*$/i.test(value))throw new Error('Unexpected SQL identifier');return '"'+value+'"'};
const digest=rows=>createHash('sha256').update(JSON.stringify(rows)).digest('hex');
const mode=process.argv[2];
let client,source;
try{
 const connection=JSON.parse(readFileSync('.tools/turso-connection.json','utf8'));
 const url=new URL(connection.url);if(!['https:','libsql:'].includes(url.protocol)||!url.hostname.endsWith('.turso.io')||url.username||url.password)throw new Error('Expected Turso endpoint');url.protocol='https:';
 client=createClient({url:url.toString(),authToken:connection.authToken,intMode:'number',fetch:(input,init)=>fetch(input,{...init,signal:AbortSignal.timeout(30000)})});
 if(Number((await client.execute('PRAGMA foreign_keys')).rows[0]?.foreign_keys)!==1)throw new Error('Remote foreign key enforcement is disabled');
 if(mode==='probe'){
  console.log(JSON.stringify({foreignKeys:true,tables:(await client.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")).rows.map(row=>row.name)}));
 }else if(mode==='import'||mode==='verify'){
  const config=JSON.parse(readFileSync('wrangler.production.json','utf8'));
  if(config.vars.MAINTENANCE!=='true')throw new Error('Maintenance must remain active during import and verification');
  const response=await fetch(config.vars.PUBLIC_ORIGIN+'/api/health');if(response.status!==503||!(await response.text()).includes('이전 중'))throw new Error('Live maintenance not active');
  const sql=readFileSync('.tools/d1-before-turso.sql','utf8');source=new DatabaseSync(':memory:');source.exec(sql);
  if(source.prepare('PRAGMA integrity_check').get().integrity_check!=='ok'||source.prepare('PRAGMA foreign_key_check').all().length)throw new Error('Source backup failed integrity check');
  const schema=source.prepare("SELECT type,name,tbl_name,sql FROM sqlite_master WHERE sql IS NOT NULL AND name NOT LIKE 'sqlite_%'").all().filter(row=>tables.includes(row.tbl_name));
  if(mode==='import'){
   const existing=(await client.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE 'libsql_%'")).rows;
   if(existing.length)throw new Error('Target must be empty; refusing overwrite');
   // Import rows before triggers, otherwise finalized chunks would be rejected and bytes counted twice.
   for(const table of tables){const definition=schema.find(row=>row.type==='table'&&row.name===table);if(!definition)throw new Error('Source table missing: '+table);await client.execute(definition.sql);}
   for(const table of tables){
    const columns=source.prepare('PRAGMA table_info('+identifier(table)+')').all().map(row=>row.name);
    const insert='INSERT INTO '+identifier(table)+'('+columns.map(identifier).join(',')+') VALUES('+columns.map(()=>'?').join(',')+')';
    let batch=[],size=0,count=0;
    for(const row of source.prepare('SELECT * FROM '+identifier(table)).iterate()){
     const args=columns.map(key=>row[key]);const bytes=Buffer.byteLength(JSON.stringify(args));
     if(batch.length&&(size+bytes>512*1024||batch.length>=100)){await client.batch(batch,'write');batch=[];size=0;}
     batch.push({sql:insert,args});size+=bytes;count++;
    }
    if(batch.length)await client.batch(batch,'write');console.log(table+': imported '+count);
   }
   for(const item of schema.filter(row=>row.type!=='table'))await client.execute(item.sql);
  }
  const checks=[];
  for(const table of tables){
   const columns=source.prepare('PRAGMA table_info('+identifier(table)+')').all();
   const keys=columns.filter(row=>row.pk).sort((a,b)=>a.pk-b.pk).map(row=>identifier(row.name));
   const query='SELECT * FROM '+identifier(table)+' ORDER BY '+keys.join(',');
   const expected=source.prepare(query).all();let actual=[];
   for(let offset=0;;offset+=25){const rows=(await client.execute(query+' LIMIT 25 OFFSET '+offset)).rows;actual.push(...rows.map(row=>Object.fromEntries(columns.map(col=>[col.name,row[col.name]]))));if(rows.length<25)break;}
   if(digest(expected)!==digest(actual))throw new Error('Row checksum mismatch: '+table);
   checks.push({table,rows:expected.length,sha256:digest(expected)});console.log(table+': verified '+expected.length);
  }
  const remoteSchema=(await client.execute("SELECT type,name,tbl_name,sql FROM sqlite_master WHERE sql IS NOT NULL AND name NOT LIKE 'sqlite_%'")).rows.filter(row=>tables.includes(row.tbl_name));
  const normalize=rows=>rows.map(row=>[row.type,row.name,row.sql.trim().replace(/;$/,'')]).sort((a,b)=>a[1].localeCompare(b[1]));
  if(digest(normalize(schema))!==digest(normalize(remoteSchema)))throw new Error('Schema/index/trigger mismatch');
  if((await client.execute('PRAGMA foreign_key_check')).rows.length)throw new Error('Target foreign key check failed');
  writeFileSync('artifacts/turso-migration-verified.json',JSON.stringify({at:new Date().toISOString(),backupSha256:createHash('sha256').update(sql).digest('hex'),checks},null,2));
  console.log('All row checksums, schema, indexes and triggers verified.');
 }else throw new Error('Use probe, import or verify');
}catch(error){console.error(error.message?.replace(/eyJ[A-Za-z0-9_.-]{50,}/g,'[redacted]')||'Migration failed');process.exitCode=1;}
finally{source?.close();client?.close();}
