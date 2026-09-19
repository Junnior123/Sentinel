import {readProduction} from './production-config.mjs';
try { const config=readProduction(); console.log(`배포 설정 확인 완료: ${config.vars.PUBLIC_ORIGIN}`); }
catch(error){console.error(error.message);process.exitCode=1;}
