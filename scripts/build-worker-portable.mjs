import {build} from 'esbuild-wasm';
try{
 await build({entryPoints:['worker/index.ts'],outfile:'artifacts/worker.mjs',bundle:true,platform:'browser',format:'esm',target:'es2022',conditions:['workerd','worker','browser'],minify:true,logLevel:'warning'});
 console.log('Worker bundle: artifacts/worker.mjs');
}catch(error){console.error(error instanceof Error?error.message:'Build failed');process.exitCode=1;}
