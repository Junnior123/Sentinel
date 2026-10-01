import {createClient, type Client, type InValue} from '@libsql/client/web';

export interface Statement {
 bind(...values: (string|number|null)[]): Statement;
 first<T=Record<string,unknown>>(): Promise<T|null>;
 all<T=Record<string,unknown>>(): Promise<{results:T[]}>;
 run(): Promise<{meta:{changes:number}}>;
}
export interface Database {prepare(sql:string):Statement}
export interface DatabaseBindings {
 DB?:Database;
 DB_PROVIDER?:string;
 TURSO_DATABASE_URL?:string;
 TURSO_AUTH_TOKEN?:string;
}

// Keep existing parameterized SQL and database-side constraints on both backends.
export function libsqlDatabase(client:Pick<Client,'execute'>):Database {
 return {prepare(sql:string){
  const statement=(args:InValue[]):Statement=>({
   bind(...values){return statement(values)},
   async first<T>(){const result=await client.execute({sql,args});return result.rows.length?Object.fromEntries(Object.entries(result.rows[0])) as T:null},
   async all<T>(){const result=await client.execute({sql,args});return {results:result.rows.map(row=>Object.fromEntries(Object.entries(row)) as T)}},
   async run(){const result=await client.execute({sql,args});return {meta:{changes:result.rowsAffected}}}
  });
  return statement([]);
 }};
}

export function connectDatabase(env:DatabaseBindings):{db:Database;close:()=>void} {
 if(!env.DB_PROVIDER||env.DB_PROVIDER==='d1'){
  if(!env.DB)throw new Error('Database binding missing');
  return {db:env.DB,close(){}};
 }
 if(env.DB_PROVIDER!=='turso')throw new Error('Unknown database provider');
 if(!env.TURSO_DATABASE_URL||!env.TURSO_AUTH_TOKEN)throw new Error('Turso credentials missing');
 const url=new URL(env.TURSO_DATABASE_URL);
 if(!['libsql:','https:'].includes(url.protocol)||!url.hostname.endsWith('.turso.io')||url.username||url.password||url.port||url.search||url.hash||!['','/'].includes(url.pathname))throw new Error('Invalid Turso endpoint');
 // HTTPS avoids WebSocket setup in Workers; never fall back to stale D1 on failure.
 url.protocol='https:';
 const client=createClient({url:url.toString(),authToken:env.TURSO_AUTH_TOKEN,intMode:'number',fetch:async(input:RequestInfo|URL,init?:RequestInit)=>fetch(input,{...init,signal:AbortSignal.timeout(15000)})});
 return {db:libsqlDatabase(client),close:()=>client.close()};
}
