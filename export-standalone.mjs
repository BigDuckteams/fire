import {readFile,writeFile} from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import vm from 'node:vm';
const root=path.dirname(fileURLToPath(import.meta.url));
const read=name=>readFile(path.join(root,'dist',name),'utf8');
const modules=[['engine.js',['GameEngine']],['levels.js',['SOLO_LEVELS','COOP_LEVELS','CHAPTERS','EASTER_EGG']],['online.js',['OnlineSession']],['progress.js',['loadProgress','saveProgress','recordResult','totalStars','completedCount','canPlay','raceUnlocked','eggUnlocked']]];
let bundle='(()=>{\n';
for(const [file,names] of modules){let source=await read(file);source=source.replace(/^export /gm,'').replaceAll('import.meta.url','document.baseURI');bundle+=`const {${names.join(',')}}=(()=>{${source}\nreturn {${names.join(',')}};})();\n`;}
bundle+=(await read('app.js')).replace(/^import .*?;\r?\n/gm,'')+'\n})();';
new vm.Script(bundle,{filename:'emberbound-standalone.js'});
let html=await read('index.html');
html=html.replace('<link rel="stylesheet" href="./styles.css">',`<style>${await read('styles.css')}</style>`);
html=html.replace('href="./assets/flame.svg"',`href="data:image/svg+xml;base64,${Buffer.from(await read('assets/flame.svg')).toString('base64')}"`);
const vendor=(await read('vendor/peerjs.min.js')).replace(/<\/script/gi,'<\\/script');
html=html.replace('<script src="./vendor/peerjs.min.js"></script><script type="module" src="./app.js"></script>',`<script>${vendor}</script><script>${bundle.replace(/<\/script/gi,'<\\/script')}</script>`);
const target=path.join(root,'Огонёк.html');await writeFile(target,html);console.log(`Standalone game: ${target} (${Buffer.byteLength(html)} bytes)`);
