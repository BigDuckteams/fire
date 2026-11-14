import assert from 'node:assert/strict';
import { GameEngine } from '../dist/engine.js';
import { SOLO_LEVELS, COOP_LEVELS } from '../dist/levels.js';

// Exercise the browser integration and all render branches without requiring
// GPU/DOM installation in the physics test runner.
const calls=[];
const methods=['setTransform','clearRect','fillRect','strokeRect','beginPath','closePath','moveTo','lineTo','bezierCurveTo','quadraticCurveTo','arc','ellipse','fill','stroke','save','restore','translate','scale','rotate','rect','clip','setLineDash','fillText'];
function context(){const c={};for(const name of methods)c[name]=()=>{};c.createLinearGradient=c.createRadialGradient=()=>({addColorStop(){}});c.drawImage=(source)=>calls.push(source);return c;}
function canvas(){return{width:600,height:325,style:{},getContext:()=>context(),getBoundingClientRect:()=>({width:1100,height:510})};}
globalThis.window={devicePixelRatio:1,addEventListener(){},removeEventListener(){}};
globalThis.document={createElement:()=>canvas()};
globalThis.ResizeObserver=class{observe(){}disconnect(){}};
globalThis.requestAnimationFrame=()=>1;
globalThis.cancelAnimationFrame=()=>{};
let pauses=0;
const engine=new GameEngine(canvas(),{onPause(){pauses++}});
engine.setMuted(true);
engine.load(SOLO_LEVELS[0]);engine.render();
assert.equal(engine.paused,true,'title demonstration does not consume flame');
assert.equal(calls.at(-1).width,600,'renderer presents a low-resolution pixel surface');
assert.equal(calls.at(-1).height,325);
engine.start();let prevented=false;
engine._keyDown({code:'Escape',repeat:false,preventDefault(){prevented=true}});
assert.equal(prevented,true,'Escape suppresses native dialog cancellation');
assert.equal(pauses,1,'one Escape emits one pause callback');assert.equal(engine.paused,true);
engine._keyDown({code:'Escape',repeat:false,preventDefault(){}});
assert.equal(pauses,2);assert.equal(engine.paused,false);
for(const themeLevel of [SOLO_LEVELS[0],SOLO_LEVELS[14],SOLO_LEVELS[29],COOP_LEVELS[29]]){engine.load(themeLevel,{mode:themeLevel.switches.length?'coop':'solo'});engine.render();}
engine.load(SOLO_LEVELS[1],{mode:'race'});engine.players[1].x=1900;engine.render();
engine.setStarlight(true);assert.equal(engine.starlight,true);engine.render();engine.setStarlight(false);assert.equal(engine.starlight,false);
assert.equal(engine.canvas.style.imageRendering,'pixelated');engine.destroy();
console.log('Browser integration checks passed: idle demonstration,600×325 pixel presentation, Escape pause callback, three world themes, co-op and split-screen race.');
