// Alternative for environments where native esbuild executables cannot run.
import {build} from 'esbuild-wasm';
import {readdirSync,mkdirSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
mkdirSync('artifacts',{recursive:true});
try{
 const files=readdirSync('tests').filter(name=>name.endsWith('.test.ts'));
 for(const name of files)await build({entryPoints:['tests/'+name],outfile:'artifacts/'+name.replace(/\.ts$/,'.mjs'),bundle:true,platform:'node',packages:'external',format:'esm',target:'node22',logLevel:'warning'});
 const result=spawnSync(process.execPath,['--test',...files.map(name=>'artifacts/'+name.replace(/\.ts$/,'.mjs')),...readdirSync('tests').filter(name=>name.endsWith('.test.mjs')).map(name=>'tests/'+name)],{stdio:'inherit'});
 if(result.error)throw result.error;process.exitCode=result.status??1;
}catch(error){console.error(error instanceof Error?error.message:'Portable test failed');process.exitCode=1;}
