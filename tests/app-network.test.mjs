import {test} from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import {readFile} from 'node:fs/promises';

const source=await readFile(new URL('../dist/app.js',import.meta.url),'utf8');
const region=source.slice(source.indexOf('let lastRemoteInputAt=0;'),source.indexOf('\nfunction showEasterEgg'));
const returnRegion=source.slice(source.indexOf('function returnToMap('),source.indexOf('\nfunction pauseGame'));

function fixture(role='guest'){
  const nodes=new Map();
  const calls={states:[],inputs:[],pauses:[],starts:[],maps:[],modals:[],sent:[]};
  const level={id:1,embers:[{},{}],candles:[{}],gates:[{id:'g1'}],switches:[{id:'s1'},{id:'s2'}]};
  const context={
    mode:'coop',selected:1,chapter:0,runId:'run-1',playing:true,networkTimer:null,captionTimer:null,dialogKind:'',progress:{},
    Date,Math,Number,Set,clearTimeout,clearInterval,setInterval:()=>17,
    session:{role,connected:true,code:'ABC234',send:p=>calls.sent.push(p),destroy(){this.destroyed=true}},
    game:{status:'playing',platforms:[{},{}],players:[],setRemoteInput:p=>calls.inputs.push(p),applyState:s=>calls.states.push(s),pause:p=>calls.pauses.push(p),getState:()=>({}),getInput:()=>({})},
    currentLevel:()=>level,
    beginLevel:options=>{calls.starts.push(options);if(options){context.runId=options.id;context.mode=options.remoteMode;context.selected=options.levelId}},
    showPause:()=>calls.pauses.push('modal'),
    closeModal:()=>{context.dialogKind=''},
    showModal:(html,kind)=>{context.dialogKind=kind;calls.modals.push({html,kind})},
    showDemo:()=>{context.playing=false},updateUI:()=>{},showCoop:()=>{},nextLevel:()=>1,raceUnlocked:()=>false,
    toast:()=>{},esc:String,icon:()=>'',navigator:{clipboard:{writeText:async()=>{}}},
    $:selector=>{if(!nodes.has(selector))nodes.set(selector,{textContent:'',disabled:false,value:'ABC234',focus(){},classList:{remove(){},add(){}}});return nodes.get(selector)},
    OnlineSession:class{
      constructor(callbacks){this.callbacks=callbacks;this.connected=false;this.code='ABC234';this.role='host'}
      create(){return new Promise(resolve=>{this.resolve=resolve})}
      join(){return Promise.resolve({code:this.code})}
      destroy(){this.destroyed=true}
    },
  };
  vm.createContext(context);vm.runInContext(region+'\n'+returnRegion,context);
  return {context,calls,nodes,level};
}

function state(){
  const player={x:100,y:500,vx:0,vy:0,fuel:100,dash:0,face:1,deadTimer:0,finished:false,finishTime:null};
  return {mode:'coop',levelId:1,time:10,deaths:0,status:'playing',paused:false,camera:0,players:[{...player},{...player,x:150}],taken:[0],lit:[0],openedGates:[],switchActive:['s1'],platforms:[{x:0,y:500,drop:0,broken:0},{x:400,y:500,drop:0,broken:0}]};
}

test('only validated matching-run snapshots reach the guest engine',()=>{
  const {context,calls}=fixture();
  context.receivePacket({type:'state',id:'old-run',state:state()});
  context.receivePacket({type:'state',id:'run-1',state:{...state(),players:[]}});
  context.receivePacket({type:'state',id:'run-1',state:{...state(),taken:{}}});
  context.receivePacket({type:'state',id:'run-1',state:{...state(),lit:[0,0]}});
  context.receivePacket({type:'state',id:'run-1',state:{...state(),time:NaN}});
  context.receivePacket({type:'state',id:'run-1',state:state()});
  assert.equal(calls.states.length,1);
  assert.equal(context.game.linkDistance,50);
});

test('host accepts only boolean input for the current run and ignores guest snapshots',()=>{
  const {context,calls}=fixture('host');
  const input={left:false,right:true,jump:false,dash:false,interact:false};
  context.receivePacket({type:'input',id:'old-run',input});
  context.receivePacket({type:'input',id:'run-1',input:{...input,right:'yes'}});
  context.receivePacket({type:'state',id:'run-1',state:state()});
  context.receivePacket({type:'input',id:'run-1',input:{...input,unexpected:'ignored'}});
  assert.equal(calls.inputs.length,1);
  assert.deepEqual(JSON.parse(JSON.stringify(calls.inputs[0])),input);
  assert.equal(calls.states.length,0);
});

test('late pause/restart requests cannot change a new run or completion modal',()=>{
  const {context,calls}=fixture('host');
  context.receivePacket({type:'pause-request',id:'old-run',paused:true});
  context.receivePacket({type:'restart-request',id:'old-run'});
  context.receivePacket({type:'pause-request',id:'run-1',paused:'false'});
  assert.equal(calls.pauses.length,0);
  assert.equal(calls.starts.length,0);
  context.receivePacket({type:'pause-request',id:'run-1',paused:true});
  assert.equal(calls.pauses[0],true);
  assert.equal(calls.sent[0].id,'run-1');
  context.game.status='complete';context.dialogKind='results';
  context.receivePacket({type:'pause-request',id:'run-1',paused:false});
  assert.equal(context.dialogKind,'results');
});

test('guest start requires host authority, valid mode/id and unique run',()=>{
  const {context,calls}=fixture();
  context.receivePacket({type:'start',id:undefined,mode:'coop',levelId:1});
  context.receivePacket({type:'start',id:'new-run',mode:'solo',levelId:1});
  context.receivePacket({type:'start',id:'new-run',mode:'coop',levelId:31});
  context.receivePacket({type:'start',id:'new-run',mode:'coop',levelId:2});
  context.receivePacket({type:'start',id:'new-run',mode:'coop',levelId:2});
  assert.equal(calls.starts.length,1);
  const host=fixture('host');host.context.receivePacket({type:'start',id:'new-run',mode:'coop',levelId:2});
  assert.equal(host.calls.starts.length,0);
});

test('returning to map preserves the lobby and clears run/input on both roles',()=>{
  const host=fixture('host');host.context.returnToMap();
  assert.equal(host.calls.sent[0].type,'map');
  assert.equal(host.calls.sent[0].id,'run-1');
  assert.equal(host.context.runId,'');
  assert.equal(host.context.playing,false);
  assert.equal(host.context.session.connected,true);
  const guest=fixture('guest');guest.context.returnToMap(false);
  assert.equal(guest.context.dialogKind,'guest-wait');
  assert.equal(guest.context.session.connected,true);
  assert.equal(guest.calls.sent.length,0);
});

test('disconnect tears down stale guest simulation and exposes a safe retry/map',()=>{
  const {context,calls}=fixture();
  const old=context.session;
  const session=context.makeSession();
  assert.equal(old.destroyed,true);
  context.playing=true;session.connected=true;
  session.callbacks.onDisconnect('Связь потеряна');
  assert.equal(context.session,null);
  assert.equal(context.playing,false);
  assert.equal(session.destroyed,true);
  assert.equal(context.dialogKind,'disconnected');
  assert.equal(calls.modals.at(-1).kind,'disconnected');
});

test('connection acceptance during create cannot overwrite the connected modal',async()=>{
  const {context,nodes}=fixture();
  context.session=null;context.playing=false;
  const creating=context.createLobby();
  const session=context.session;
  session.connected=true;session.callbacks.onConnection();
  assert.equal(context.dialogKind,'connected');
  session.resolve({code:'ABC234'});
  await creating;
  assert.equal(context.dialogKind,'connected');
  assert.equal(nodes.has('#lobby-code'),false);
  context.closeOnline();
});
