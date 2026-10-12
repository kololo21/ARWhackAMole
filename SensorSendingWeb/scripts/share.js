import {spawn} from 'node:child_process';
import {existsSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
const port=Number(process.env.PORT||8787);
try{const r=await fetch(`http://127.0.0.1:${port}/health`);if(!r.ok)throw new Error();}
catch{console.error('先に別のターミナルで npm start を実行してください。');process.exit(1);}
console.log('一時的なHTTPS URLを作成します。終了はCtrl+C。Unityはローカルの ws://127.0.0.1:'+port+'/ws に接続できます。');
const local=fileURLToPath(new URL('../.tools/cloudflared',import.meta.url));
const child=spawn(process.env.CLOUDFLARED_PATH||(existsSync(local)?local:'cloudflared'),['tunnel','--url',`http://127.0.0.1:${port}`,'--no-autoupdate'],{stdio:'inherit'});
child.on('error',e=>{console.error(`cloudflaredを起動できません (${e.code})。\nbrew install cloudflared\nまたは CLOUDFLARED_PATH=/path/to/cloudflared npm run share を利用してください。`);process.exitCode=1;});
child.on('exit',code=>{process.exitCode=code??0;});
process.on('SIGINT',()=>child.kill('SIGINT'));process.on('SIGTERM',()=>child.kill('SIGTERM'));
