import assert from 'node:assert/strict';
import { GameEngine } from '../dist/engine.js';

const fixture = (extra = {}) => ({id:1,name:'Test',width:2600,height:650,timeLimit:80,parTime:40,spawn:{x:80,y:538},platforms:[{x:0,y:540,w:2600,h:100,type:'stone'}],hazards:[],embers:[],candles:[],goal:{x:2400,y:540},switches:[],gates:[],...extra});
function game(level=fixture(),options={}) {
  // Physics runs independently of DOM, audio, and rendering.
  const g=Object.create(GameEngine.prototype);
  Object.assign(g,{cb:{},keys:new Set(),taken:new Set(),lit:new Set(),openedGates:new Set(),switchActive:new Set(),particles:[],remoteInput:{},quality:'low',muted:true});
  g.render=g._ensureLoop=g._sound=g._burst=()=>{};
  g.load(level,options);g.start();g.input={left:false,right:false,jump:false,dash:false};
  g._input=()=>({...g.input});
  return g;
}
function tick(g,seconds){for(let t=0;t<seconds;t+=.01)g.step(.01)}

{
  const g=game();tick(g,.2);assert.equal(g.players[0].y,540,'feet land on the surface');
  g.input.right=true;tick(g,1);assert.ok(g.players[0].x>300,'horizontal movement accelerates');
  g.input.jump=true;tick(g,.2);assert.ok(g.players[0].y<465,'first jump clears a ledge');
  g.input.jump=false;tick(g,.03);g.input.jump=true;tick(g,.12);assert.equal(g.players[0].jumps,2,'second jump works in air');
  assert.ok(g.players[0].vy<0,'second jump adds upward momentum');
}
{
  const g=game(fixture({candles:[{x:500,y:540}],embers:[{x:500,y:510}]}));
  g.players[0].x=500;g.players[0].fuel=25;tick(g,.02);
  assert.equal(g.taken.size,1);assert.equal(g.lit.size,1);assert.ok(g.players[0].fuel>99,'candle restores flame');
  g.players[0].y=900;tick(g,.01);assert.equal(g.deaths,1);assert.equal(g.players[0].checkpointIndex,0);
  assert.ok(Math.abs(g.players[0].x-500)<30,'fall respawns at the lit candle');
}
{
  const g=game(fixture({switches:[{id:'a',x:150,y:540},{id:'b',x:280,y:540}],gates:[{id:'g',x:350,y:0,w:28,h:540,requires:['a','b']}]}),{mode:'coop'});
  g.players[0].x=150;g.players[1].x=280;tick(g,.02);assert.ok(g.openedGates.has('g'),'simultaneous pressure switches open the gate');
  g.players.forEach(p=>p.x+=300);tick(g,.02);assert.ok(g.openedGates.has('g'),'opened gates stay latched');
  g.players[0].x=100;g.players[1].x=500;const before=g.players[0].fuel;tick(g,.1);assert.ok(before-g.players[0].fuel>.3,'distance drains linked flames');
  g.players[1].x=950;tick(g,.01);assert.equal(g.deaths,1,'breaking the tether returns the pair safely');
}
{
  const g=game(fixture({goal:{x:2100,y:540}}),{mode:'race'});let result;
  g.cb.onComplete=r=>{result=r};g.players[0].x=2100;tick(g,.02);
  assert.equal(g.players[0].finished,true);assert.equal(g.status,'playing','race waits for both finishers');
  tick(g,1);g.players[1].x=2100;tick(g,.02);assert.equal(g.status,'complete');
  assert.ok(result.raceTimes[1]>result.raceTimes[0],'race records independent finish times');
  assert.doesNotThrow(()=>JSON.parse(JSON.stringify(g.getState())),'online snapshot is serializable');
}
console.log('Engine checks passed: landings, double jump, checkpoints, fuel, switch latches, tether, race and network serialization.');
