import {build} from 'esbuild-wasm';
import {readFileSync,writeFileSync,mkdirSync,cpSync,renameSync,existsSync} from 'node:fs';
try {
 const result=await build({entryPoints:['web/main.tsx'],outdir:'dist/assets',entryNames:'[name]-[hash]',bundle:true,platform:'browser',format:'esm',target:'es2022',jsx:'automatic',minify:true,define:{'process.env.NODE_ENV':'"production"'},metafile:true,write:false});
 const js=result.outputFiles.find(f=>f.path.endsWith('.js')),css=result.outputFiles.find(f=>f.path.endsWith('.css'));
 if(!js||!css)throw new Error('Missing web bundle');
 // Keep the previous build if compilation fails; avoid deploying stale asset files.
 if(existsSync('dist')) {mkdirSync('artifacts',{recursive:true});renameSync('dist','artifacts/dist-before-'+Date.now());}
 mkdirSync('dist/assets',{recursive:true});
 for(const file of result.outputFiles)writeFileSync(file.path,file.contents);
 cpSync('public','dist',{recursive:true});
 const name=p=>'/assets/'+p.replaceAll('\\','/').split('/').pop();
 const html=readFileSync('index.html','utf8');
 if(!html.includes('<script type="module" src="/web/main.tsx"></script>'))throw new Error('Unknown HTML entry');
 writeFileSync('dist/index.html',html.replace('<script type="module" src="/web/main.tsx"></script>',`<link rel="stylesheet" href="${name(css.path)}"/><script type="module" src="${name(js.path)}"></script>`));
 console.log('Web bundle: dist/');
} catch(error) {console.error(error instanceof Error?error.message:'Web build failed');process.exitCode=1;}
