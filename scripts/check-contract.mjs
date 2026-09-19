import{readFile}from'node:fs/promises';
// Use through tsx: validates the real C# serialized report against the Worker contract.
const{chunkSchema,summarySchema}=await import('../shared/contracts.ts');
for(const name of ['fixture-report','real-sample-report']){const report=JSON.parse(await readFile(`artifacts/${name}.json`));summarySchema.parse(report.summary);for(const c of report.chunks)chunkSchema.parse(c);console.log('CONTRACT PASS',name);}
