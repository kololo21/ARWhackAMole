import {execFileSync} from 'node:child_process';
import {readdir,readFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';
const root=fileURLToPath(new URL('../',import.meta.url));
let count=0;
for(const dir of ['public','server','scripts','tests'])for(const name of await readdir(resolve(root,dir))){
  if(name.endsWith('.js')){execFileSync(process.execPath,['--check',resolve(root,dir,name)]);count++;}
}
for(const path of ['package.json','package-lock.json'])JSON.parse(await readFile(resolve(root,path),'utf8'));
for(const path of (await readdir(resolve(root,'public'))).filter(name=>name.endsWith('.html')).map(name=>'public/'+name)){
  const html=await readFile(resolve(root,path),'utf8');
  for(const match of html.matchAll(/(?:src|href)="(\/[^"?#]+\.(?:js|css))"/g))await readFile(resolve(root,'public',match[1].slice(1)));
}
console.log(`Checked ${count} JavaScript files, JSON manifests and page assets. Static web: no bundling required.`);
