const clamp=(n,a,b)=>Math.max(a,Math.min(b,n));
const lerp=(a,b,t)=>a+(b-a)*t;
const overlap=(a,b)=>a.x<b.x+b.w&&a.x+a.w>b.x&&a.y<b.y+b.h&&a.y+a.h>b.y;
const hash=(n)=>{const v=Math.sin(n*127.1+311.7)*43758.5453;return v-Math.floor(v)};
const emptyInput=()=>({left:false,right:false,jump:false,dash:false,interact:false});
const VW=1200,VH=650,PW=23,PH=34,GRAVITY=1450,SPEED=255,JUMP=515;

/** A deterministic, host-authoritative platform simulation with a Canvas renderer. */
export class GameEngine {
  constructor(canvas,callbacks={}) {
    this.canvas=canvas;this.screenCtx=canvas.getContext('2d',{alpha:false});this.surface=document.createElement('canvas');this.surface.width=600;this.surface.height=325;this.ctx=this.surface.getContext('2d',{alpha:false});this.cb=callbacks;
    this.keys=new Set();this.level=null;this.mode='solo';this.online=false;this.role='host';
    this.players=[];this.status='ready';this.paused=true;this.muted=false;this.quality='high';this.starlight=false;
    this.camera=0;this.time=0;this.deaths=0;this.particles=[];this.taken=new Set();this.lit=new Set();
    this.openedGates=new Set();this.switchActive=new Set();this.remoteInput=emptyInput();this.raf=0;
    this.shake=0;this.flash=0;this.ambientTime=0;this.hudClock=0;this.platforms=[];
    this._keyDown=e=>{
      if(!this.level||this.status==='stopped')return;
      if(['ArrowLeft','ArrowRight','ArrowUp','ArrowDown','Space','Tab'].includes(e.code)&&this.status==='playing')e.preventDefault();
      this.keys.add(e.code);
      if(e.repeat)return;
      if(e.code==='Escape'&&this.status==='playing'){e.preventDefault();this.pause(!this.paused);this.cb.onPause?.(this.paused)}
    };
    this._keyUp=e=>this.keys.delete(e.code);
    this._blur=()=>this.keys.clear();
    window.addEventListener('keydown',this._keyDown);window.addEventListener('keyup',this._keyUp);window.addEventListener('blur',this._blur);
    this.resizeObserver=new ResizeObserver(()=>this.resize());this.resizeObserver.observe(canvas);
    this.canvas.style.imageRendering='pixelated';
    this.resize();
  }

  resize(){
    const r=this.canvas.getBoundingClientRect();const dpr=Math.min(window.devicePixelRatio||1,this.quality==='low'?1:2);
    const w=Math.max(1,Math.round(r.width*dpr)),h=Math.max(1,Math.round(r.height*dpr));
    if(this.canvas.width!==w||this.canvas.height!==h){this.canvas.width=w;this.canvas.height=h}
    this.render();
  }
  load(level,{mode='solo',online=false,role='host'}={}){
    this.level=level;this.mode=mode;this.online=online;this.role=role;this.status='ready';this.paused=true;
    this.time=0;this.deaths=0;this.camera=0;this.taken.clear();this.lit.clear();this.openedGates.clear();
    this.switchActive.clear();this.particles=[];this.shake=0;this.flash=0;this.hudClock=0;
    this.platforms=(level.platforms||[]).map((p,i)=>({...p,id:p.id||`p${i}`,originX:p.x,originY:p.y,drop:0,broken:0}));
    const s=level.spawn||{x:100,y:500};
    this.players=Array.from({length:mode==='solo'?1:2},(_,i)=>({x:s.x+i*36,y:s.y,vx:0,vy:0,fuel:100,grounded:false,jumps:0,coyote:0,jumpBuffer:0,dash:0,dashReady:true,face:1,prevInput:emptyInput(),checkpoint:{x:s.x+i*36,y:s.y},checkpointIndex:-1,deadTimer:0,finished:false,finishTime:null,trail:[],portalCooldown:0}));
    this.remoteInput=emptyInput();this.keys.clear();this._emitHUD();this.render();this._ensureLoop();return this;
  }
  start(){if(!this.level)return;this.status='playing';this.paused=false;this._last=performance.now();this._ensureLoop();return this}
  stop(){cancelAnimationFrame(this.raf);this.raf=0;this.status='stopped';this.keys.clear()}
  destroy(){this.stop();this.resizeObserver.disconnect();window.removeEventListener('keydown',this._keyDown);window.removeEventListener('keyup',this._keyUp);window.removeEventListener('blur',this._blur)}
  pause(value=true){this.paused=!!value;this.keys.clear();this._emitHUD();return this.paused}
  restart(){if(!this.level)return;const l=this.level,o={mode:this.mode,online:this.online,role:this.role};this.load(l,o);this.start()}
  setMuted(value){this.muted=!!value}
  setStarlight(value){this.starlight=!!value;this.render()}
  setQuality(value){this.quality=value;this.resize()}
  setRemoteInput(input){this.remoteInput={...emptyInput(),...input}}
  getInput(){
    const k=this.keys,arrows=this.mode==='solo'||this.role==='guest';
    return{left:k.has('KeyA')||(arrows&&k.has('ArrowLeft')),right:k.has('KeyD')||(arrows&&k.has('ArrowRight')),jump:k.has('Space')||k.has('KeyW')||(arrows&&k.has('ArrowUp')),dash:k.has('ShiftLeft')||k.has('ShiftRight')||(arrows&&k.has('ControlRight')),interact:k.has('KeyE')};
  }
  _input(index){if(index===0)return this.getInput();if(this.online)return this.remoteInput;const k=this.keys;return{left:k.has('ArrowLeft'),right:k.has('ArrowRight'),jump:k.has('ArrowUp')||k.has('Numpad0'),dash:k.has('ControlRight')||k.has('Numpad1'),interact:k.has('Enter')}}
  getState(){return{levelId:this.level?.id,mode:this.mode,time:this.time,deaths:this.deaths,status:this.status,paused:this.paused,players:this.players.map(p=>({...p,trail:[]})),taken:[...this.taken],lit:[...this.lit],openedGates:[...this.openedGates],switchActive:[...this.switchActive],platforms:this.platforms.map(p=>({x:p.x,y:p.y,drop:p.drop,broken:p.broken})),camera:this.camera}}
  applyState(s){
    if(!s||!Array.isArray(s.players))return;
    const previousStatus=this.status;
    this.players=s.players.map((p,i)=>({...p,trail:this.players[i]?.trail||[]}));this.time=s.time||0;this.deaths=s.deaths||0;
    this.taken=new Set(s.taken||[]);this.lit=new Set(s.lit||[]);this.openedGates=new Set(s.openedGates||[]);this.switchActive=new Set(s.switchActive||[]);
    this.status=s.status;this.paused=!!s.paused;this.camera=s.camera||0;
    (s.platforms||[]).forEach((p,i)=>{if(this.platforms[i])Object.assign(this.platforms[i],p)});
    this._emitHUD();this._ensureLoop();
    if(s.status==='complete'&&previousStatus!=='complete')this._reportComplete();
  }
  _ensureLoop(){if(this.raf)return;this._last=performance.now();const frame=now=>{const dt=Math.min((now-this._last)/1000,.04);this._last=now;this.ambientTime+=dt;
      if(this.status==='playing'&&!this.paused&&!(this.online&&this.role==='guest')){let rest=dt;while(rest>0){const sub=Math.min(rest,1/100);this.step(sub);rest-=sub}}
      this._effects(dt);this.render();this.raf=requestAnimationFrame(frame)};this.raf=requestAnimationFrame(frame)}

  step(dt){
    if(!this.level||this.status!=='playing'||this.paused)return;
    this.time+=dt;const l=this.level;
    for(const p of this.platforms){
      p.prevX=p.x;p.prevY=p.y;
      if(p.type==='moving'){const offset=Math.sin(this.time*(p.speed||1)+(p.phase||0))*(p.range||65);p.x=p.originX+(p.axis==='y'?0:offset);p.y=p.originY+(p.axis==='y'?offset:0)}
      if(p.broken>0){p.broken=Math.max(0,p.broken-dt);if(!p.broken)p.drop=0}
      else if(p.drop>0){p.drop+=dt;if(p.drop>.85){p.broken=3.1;this._burst(p.x+p.w/2,p.y,14,'#aaa28b',80)}}
    }
    const gates=(l.gates||[]).filter((g,i)=>!this.openedGates.has(g.id||i));
    for(let i=0;i<this.players.length;i++){
      const p=this.players[i];if(p.finished)continue;
      if(p.deadTimer>0){p.deadTimer-=dt;continue}
      p.portalCooldown=Math.max(0,(p.portalCooldown||0)-dt);
      const input=this._input(i),jumpPressed=input.jump&&!p.prevInput.jump,dashPressed=input.dash&&!p.prevInput.dash;
      if(jumpPressed)p.jumpBuffer=.14;else p.jumpBuffer=Math.max(0,p.jumpBuffer-dt);
      p.coyote=p.grounded?.12:Math.max(0,p.coyote-dt);
      const direction=(input.right?1:0)-(input.left?1:0);if(direction)p.face=direction;
      const onIce=p.grounded&&p.surface==='ice';
      if(p.dash>0){p.dash-=dt;p.vx=p.face*670;p.vy=0}
      else{
        const accel=onIce?950:2100,target=direction*SPEED;p.vx=lerp(p.vx,target,Math.min(1,dt*(direction?accel/SPEED:onIce?1.1:13)));
        p.vy+=GRAVITY*dt;
        if(p.jumpBuffer>0&&(p.grounded||p.coyote>0||p.jumps<2)){
          p.vy=-JUMP;p.jumps=p.grounded||p.coyote>0?1:p.jumps+1;p.grounded=false;p.coyote=0;p.jumpBuffer=0;
          this._burst(p.x,p.y,7,i?'#8de5ed':'#ffbb55',70);this._sound('jump');
        }
        if(!input.jump&&p.prevInput.jump&&p.vy<-210)p.vy*=.55;
        if(dashPressed&&p.dashReady){p.dash=.16;p.dashReady=false;p.vy=0;p.vx=p.face*670;this._burst(p.x,p.y-15,12,i?'#8de5ed':'#ffbb55',100);this._sound('dash')}
      }
      const prevY=p.y,prevX=p.x;p.x=clamp(p.x+p.vx*dt,PW/2,(l.width||3000)-PW/2);
      const a={x:p.x-PW/2,y:p.y-PH,w:PW,h:PH};
      for(const g of gates){if(overlap(a,g)){if(prevX<=g.x)p.x=g.x-PW/2;else p.x=g.x+g.w+PW/2;p.vx=0;a.x=p.x-PW/2}}
      p.y+=p.vy*dt;p.grounded=false;p.surface='stone';
      for(const plat of this.platforms){
        if(plat.broken>0)continue;
        if(p.x+PW/2>plat.x+1&&p.x-PW/2<plat.x+plat.w-1&&prevY<=plat.y+7&&p.y>=plat.y&&p.vy>=0){
          p.y=plat.y;p.vy=0;p.grounded=true;p.jumps=0;p.dashReady=true;p.surface=plat.type;
          if(plat.type==='moving'){p.x+=plat.x-(plat.prevX??plat.x);p.y+=Math.max(0,plat.y-(plat.prevY??plat.y))}
          if(plat.type==='crumble'&&!plat.drop)plat.drop=.001;
          if(plat.type==='bounce'){p.vy=-720;p.grounded=false;p.jumps=1;this._burst(p.x,p.y,12,'#b7f39b',100);this._sound('bounce')}
          break;
        }
      }
      // Solid sides of tall ledges; one-way thin floating platforms remain forgiving.
      for(const plat of this.platforms){if(plat.broken||plat.h<55)continue;const box={x:p.x-PW/2,y:p.y-PH+3,w:PW,h:PH-5};
        if(overlap(box,plat)&&prevY>plat.y+9){if(prevX+PW/2<=plat.x+5){p.x=plat.x-PW/2;p.vx=0}else if(prevX-PW/2>=plat.x+plat.w-5){p.x=plat.x+plat.w+PW/2;p.vx=0}}
      }
      p.fuel-=dt*100/(l.timeLimit||90);
      for(const h of l.hazards||[]){
        if(h.period&&Math.sin(this.time*Math.PI*2/h.period+(h.phase||0))<-.25)continue;
        if(!overlap({x:p.x-PW/2,y:p.y-PH,w:PW,h:PH},h))continue;
        if(h.type==='wind'){p.vx+=(h.force||-170)*dt;p.x+=(h.force||-170)*dt*.55;p.fuel-=dt*3}
        else if(h.type==='rain'){p.fuel-=dt*14;this._burst(p.x,p.y-20,1,'#b9c8d4',20)}
        else{this._die(i,h.type==='water'?'Вода погасила огонёк':'Острые камни');break}
      }
      if(p.deadTimer>0){p.prevInput={...input};continue}
      (l.embers||[]).forEach((e,j)=>{if(!this.taken.has(j)&&Math.hypot(p.x-e.x,p.y-17-e.y)<35){this.taken.add(j);p.fuel=Math.min(100,p.fuel+9);this._burst(e.x,e.y,14,'#ffd888',120);this._sound('collect')}});
      (l.candles||[]).forEach((c,j)=>{if(Math.hypot(p.x-c.x,p.y-c.y)<68&&j>=p.checkpointIndex){
        if(j>p.checkpointIndex){p.checkpointIndex=j;p.checkpoint={x:c.x+(i?22:-12),y:c.y-1};p.fuel=100;
          if(!this.lit.has(j)){this.lit.add(j);this._burst(c.x,c.y-30,28,'#ffcc72',160);this._sound('candle')}
        }else p.fuel=Math.min(100,p.fuel+dt*27);
      }});
      for(const portal of l.portals||[]){if(!p.portalCooldown&&Math.hypot(p.x-portal.x,p.y-portal.y)<40){p.x=portal.toX??portal.target?.x??p.x;p.y=portal.toY??portal.target?.y??p.y;p.portalCooldown=.7;this._burst(p.x,p.y,25,'#bd9dff',130)}}
      if(p.y>(l.height||650)+130)this._die(i,'Падение в темноту');
      else if(p.fuel<=0)this._die(i,'Огонёк погас — держись ближе к свечам');
      p.fuel=clamp(p.fuel,0,100);p.prevInput={...input};
    }
    // Pressure switches are simultaneous. Once a gate is opened it stays open.
    this.switchActive.clear();
    for(const s of l.switches||[]){if(this.players.some(p=>!p.deadTimer&&Math.abs(p.x-s.x)<33&&Math.abs(p.y-s.y)<48))this.switchActive.add(s.id)}
    (l.gates||[]).forEach((g,j)=>{const id=g.id||j;if(!this.openedGates.has(id)&&(g.requires||[]).every(s=>this.switchActive.has(s))){this.openedGates.add(id);this._burst(g.x+g.w/2,g.y+g.h/2,25,'#9de2c0',170);this._sound('gate')}});
    if(this.mode==='coop'&&this.players.length===2){const a=this.players[0],b=this.players[1];const distance=Math.hypot(a.x-b.x,a.y-b.y);this.linkDistance=distance;
      if(distance>280){const drain=dt*(distance-280)/32;a.fuel-=drain;b.fuel-=drain}
      if(distance>700&&!a.deadTimer&&!b.deadTimer){this._die(0,'Нить тепла порвалась — идите вместе');this._die(1,'Нить тепла порвалась — идите вместе',true)}
    }else this.linkDistance=0;
    const goal=l.goal||{x:(l.width||3000)-120,y:500};
    for(const p of this.players){if(!p.deadTimer&&!p.finished&&Math.hypot(p.x-goal.x,p.y-goal.y)<72){if(this.mode==='race'){p.finished=true;p.finishTime=this.time;p.vx=0;p.vy=0;this._burst(goal.x,goal.y-35,25,'#ffdb91',160)}}}
    const sealsOpened=this.mode!=='coop'||(l.gates||[]).every((gate,i)=>this.openedGates.has(gate.id||i));
    const done=this.mode==='race'?this.players.every(p=>p.finished):sealsOpened&&this.players.every(p=>!p.deadTimer&&Math.hypot(p.x-goal.x,p.y-goal.y)<76);
    if(done){this.status='complete';this.paused=true;this._burst(goal.x,goal.y-30,70,'#ffdc94',240);this._sound('complete');this._reportComplete()}
    const target=this.players.reduce((sum,p)=>sum+p.x,0)/this.players.length-VW*.37;this.camera=lerp(this.camera,clamp(target,0,Math.max(0,(l.width||3000)-VW)),Math.min(1,dt*4.5));
    this.hudClock+=dt;if(this.hudClock>.08){this.hudClock=0;this._emitHUD()}
  }
  _die(index,reason,silent=false){
    const p=this.players[index];if(!p||p.deadTimer>0||p.finished)return;
    this._burst(p.x,p.y-17,30,index?'#77d9e7':'#f8ac5b',180);p.x=p.checkpoint.x;p.y=p.checkpoint.y;p.vx=0;p.vy=0;p.fuel=100;p.deadTimer=.45;p.dashReady=true;p.jumps=0;p.prevInput=emptyInput();
    if(!silent){this.deaths++;this.shake=.2;this.flash=.2;this._sound('death');this.cb.onDeath?.({reason,deaths:this.deaths,player:index+1})}
    if(this.mode==='coop'&&!silent){const other=this.players[1-index];if(other&&Math.hypot(other.x-p.x,other.y-p.y)>500){other.x=p.x+(index?-30:30);other.y=p.y;other.vx=other.vy=0;other.fuel=100;other.deadTimer=.45}}
  }
  _reportComplete(){const speed=this.time<=(this.level.parTime||60),perfect=this.taken.size===(this.level.embers||[]).length&&this.deaths===0;
    this.cb.onComplete?.({time:this.time,stars:1+Number(speed)+Number(perfect),conditions:[true,speed,perfect],embers:this.taken.size,totalEmbers:(this.level.embers||[]).length,deaths:this.deaths,mode:this.mode,levelId:this.level.id,raceTimes:this.players.map(p=>p.finishTime??this.time)});this._emitHUD()}
  _emitHUD(){if(!this.level)return;const fuel=Math.min(...this.players.map(p=>p.fuel));this.cb.onHUD?.({time:this.time,fuel:Number.isFinite(fuel)?fuel:100,fuels:this.players.map(p=>p.fuel),remaining:Math.max(0,(fuel/100)*(this.level.timeLimit||90)),embers:this.taken.size,totalEmbers:(this.level.embers||[]).length,deaths:this.deaths,checkpoint:this.lit.size,totalCheckpoints:(this.level.candles||[]).length,linkDistance:this.linkDistance||0,paused:this.paused,status:this.status,mode:this.mode,levelId:this.level.id,players:this.players.map(p=>({finished:p.finished,finishTime:p.finishTime}))})}
  _sound(kind){if(this.muted)return;try{if(!this.audio)this.audio=new(window.AudioContext||window.webkitAudioContext)();if(this.audio.state==='suspended')this.audio.resume();const ac=this.audio,osc=ac.createOscillator(),gain=ac.createGain(),t=ac.currentTime;
    const data={jump:[260,440,.09],dash:[180,650,.12],bounce:[230,780,.2],collect:[880,1320,.14],candle:[440,880,.35],gate:[210,550,.35],death:[150,45,.25],complete:[523,1046,.65]}[kind]||[440,880,.1];
    osc.type=kind==='death'?'triangle':'sine';osc.frequency.setValueAtTime(data[0],t);osc.frequency.exponentialRampToValueAtTime(data[1],t+data[2]);gain.gain.setValueAtTime(.0001,t);gain.gain.exponentialRampToValueAtTime(.035,t+.012);gain.gain.exponentialRampToValueAtTime(.0001,t+data[2]);osc.connect(gain);gain.connect(ac.destination);osc.start(t);osc.stop(t+data[2]+.02)
  }catch{}}
  _burst(x,y,count,color,power){if(this.quality==='low')count=Math.ceil(count*.4);for(let j=0;j<count;j++){const a=Math.random()*Math.PI*2,v=Math.random()*power;this.particles.push({x,y,vx:Math.cos(a)*v,vy:Math.sin(a)*v-25,life:.4+Math.random()*.7,max:1.1,color,size:1+Math.random()*2.6})}if(this.particles.length>400)this.particles.splice(0,this.particles.length-400)}
  _effects(dt){this.shake=Math.max(0,this.shake-dt);this.flash=Math.max(0,this.flash-dt);for(const p of this.particles){p.life-=dt;p.x+=p.vx*dt;p.y+=p.vy*dt;p.vy+=80*dt}this.particles=this.particles.filter(p=>p.life>0);
    if(this.level&&Math.random()<dt*26){for(let i=0;i<this.players.length;i++){const p=this.players[i];if(!p.deadTimer&&this.status!=='stopped')this.particles.push({x:p.x+(Math.random()-.5)*10,y:p.y-20,vx:(Math.random()-.5)*22,vy:-20-Math.random()*30,life:.5+Math.random()*.5,max:1,color:i?'#7cdee9':'#ffae52',size:1+Math.random()*1.5})}}
  }

  render(){
    const ctx=this.ctx;if(!ctx)return;ctx.imageSmoothingEnabled=false;ctx.setTransform(this.surface.width/VW,0,0,this.surface.height/VH,0,0);ctx.clearRect(0,0,VW,VH);
    this._background(ctx);if(!this.level){this._present();return}
    if(this.mode==='race'&&this.players.length===2&&Math.abs(this.players[0].x-this.players[1].x)>780){
      const camera=this.camera;
      this.players.forEach((p,i)=>{ctx.save();ctx.beginPath();ctx.rect(i*VW/2,0,VW/2,VH);ctx.clip();ctx.translate(i*VW/2,0);this.camera=clamp(p.x-VW/4,0,Math.max(0,(this.level.width||3000)-VW/2));this._renderWorld(ctx);ctx.restore()});this.camera=camera;
      ctx.fillStyle='#101d20';ctx.fillRect(VW/2-2,0,4,VH);ctx.fillStyle='rgba(226,204,153,.4)';ctx.fillRect(VW/2,0,2,VH);
      ctx.font='600 12px monospace';ctx.textAlign='left';ctx.fillStyle='#ffcf88';ctx.fillText('01',18,28);ctx.fillStyle='#8ae0ed';ctx.fillText('02',VW/2+18,28);
    }else this._renderWorld(ctx);
    const v=ctx.createRadialGradient(VW*.5,VH*.4,180,VW*.5,VH*.5,740);v.addColorStop(0,'rgba(2,9,10,0)');v.addColorStop(1,'rgba(0,5,7,.6)');ctx.fillStyle=v;ctx.fillRect(0,0,VW,VH);
    if(this.flash){ctx.fillStyle=`rgba(210,71,35,${this.flash*.6})`;ctx.fillRect(0,0,VW,VH)}this._present();
  }
  _renderWorld(ctx){
    ctx.save();const sx=this.shake?(Math.random()-.5)*5:0;ctx.translate(-this.camera+sx,0);
    this._scenery(ctx);
    for(const h of this.level.hazards||[])this._hazard(ctx,h);
    for(const p of this.platforms)this._platform(ctx,p);
    for(const s of this.level.switches||[])this._switch(ctx,s);
    (this.level.gates||[]).forEach((g,i)=>{if(!this.openedGates.has(g.id||i))this._gate(ctx,g)});
    (this.level.candles||[]).forEach((c,i)=>this._candle(ctx,c.x,c.y,this.lit.has(i),false));
    if(this.level.goal)this._candle(ctx,this.level.goal.x,this.level.goal.y,this.status==='complete',true);
    (this.level.embers||[]).forEach((e,i)=>{if(!this.taken.has(i))this._ember(ctx,e.x,e.y,i)});
    if(this.mode==='coop'&&this.players.length===2)this._link(ctx);
    this.players.forEach((p,i)=>this._flame(ctx,p,i));
    for(const p of this.particles){ctx.globalAlpha=clamp(p.life/p.max,0,1);ctx.fillStyle=p.color;const size=Math.max(2,Math.round(p.size/2)*2);ctx.fillRect(Math.round(p.x/2)*2,Math.round(p.y/2)*2,size,size)}ctx.globalAlpha=1;
    // Atmospheric foreground is deliberately translucent so every landing stays readable.
    this._foreground(ctx);ctx.restore();
  }
  _present(){const ctx=this.screenCtx;ctx.setTransform(1,0,0,1,0,0);ctx.imageSmoothingEnabled=false;ctx.drawImage(this.surface,0,0,this.canvas.width,this.canvas.height)}
  _background(ctx){
    const theme=this.level?.theme||'forest',t=this.ambientTime;
    const bg=ctx.createLinearGradient(0,0,0,VH);bg.addColorStop(0,theme==='summit'?'#142132':theme==='ruins'?'#10181c':'#0b1b1d');bg.addColorStop(.58,'#263d36');bg.addColorStop(1,'#071210');ctx.fillStyle=bg;ctx.fillRect(0,0,VW,VH);
    // Low-resolution, independently scrolling silhouettes form a completely pixel-drawn forest.
    ctx.fillStyle='rgba(212,223,168,.18)';ctx.fillRect(920,38,42,4);ctx.fillRect(912,42,58,32);ctx.fillRect(920,74,42,4);
    for(let layer=0;layer<3;layer++){
      ctx.fillStyle=layer===0?'rgba(76,105,91,.17)':layer===1?'rgba(20,42,39,.31)':'rgba(4,22,22,.52)';
      const spacing=layer===2?260:150,offset=this.camera*(.06+layer*.08);
      const trunkColor=ctx.fillStyle;
      for(let j=-2;j<11;j++){
        const seed=j+layer*21,x=Math.round((j*spacing-offset%spacing+hash(seed)*65)/4)*4,w=Math.round((18+layer*10+hash(seed+4)*36)/4)*4;
        ctx.fillStyle=trunkColor;ctx.fillRect(x-w*.5,-10,w,340);ctx.fillRect(x-w*.5-4,330,w+8,220);ctx.fillRect(x-w*.5-10,546,w+24,104);
        // Branches taper in deliberate square steps instead of smooth curves.
        for(let b=0;b<4;b++){const side=b%2?-1:1,by=76+b*51+hash(seed+b)*32;for(let k=0;k<4;k++){const bx=x+side*(w*.4+k*17),yy=Math.round((by-k*13)/4)*4;ctx.fillRect(Math.round(bx/4)*4,yy,side>0?22:-22,Math.max(4,16-k*3))}}
        ctx.fillStyle=layer===0?'rgba(136,157,121,.075)':layer===1?'rgba(102,137,105,.09)':'rgba(104,141,99,.09)';
        for(let k=0;k<9;k++){const px=x-w*.3+Math.floor(hash(seed+k*4)*w*.5/4)*4,py=36+Math.floor(hash(seed+k*9)*550/8)*8;ctx.fillRect(px,py,4,12+Math.floor(hash(seed+k)*5)*4)}
        // Broad, stepped crown shapes leave the path and the middle of the sky readable.
        const crownY=-38+hash(seed+15)*82,crownW=100+hash(seed+16)*95;
        ctx.fillStyle=layer===0?'rgba(76,105,76,.22)':layer===1?'rgba(31,65,43,.43)':'rgba(9,35,26,.62)';
        ctx.fillRect(x-crownW*.5,crownY,crownW,38);ctx.fillRect(x-crownW*.42-12,crownY+28,crownW*.84+24,27);ctx.fillRect(x-crownW*.29,crownY+49,crownW*.58,18);
        ctx.fillStyle=layer===0?'rgba(141,162,108,.13)':layer===1?'rgba(99,134,77,.2)':'rgba(65,103,56,.23)';
        for(let k=0;k<8;k++){const px=Math.floor((x-crownW*.5+hash(seed+k*12)*crownW)/8)*8,py=Math.floor((crownY+hash(seed+k*8)*52)/6)*6;ctx.fillRect(px,py,8+Math.floor(hash(seed+k*2)*3)*6,4+Math.floor(hash(seed+k*3)*2)*4)}
      }
    }
    // Near canopy and sparse dangling vines frame the forest, clear of landing zones.
    for(let j=-1;j<13;j++){
      const x=Math.round((j*112-this.camera*.19%112)/4)*4,depth=18+Math.floor(hash(j+117)*11)*4;
      ctx.fillStyle='#0c221a';ctx.fillRect(x,0,116,depth);ctx.fillRect(x+12,depth,92,12);ctx.fillRect(x+30,depth+12,50,8);
      ctx.fillStyle='rgba(66,100,48,.5)';for(let k=0;k<4;k++)ctx.fillRect(x+8+k*25,depth-8-Math.floor(hash(j+k)*3)*4,12+Math.floor(hash(j+k+3)*2)*8,4);
      if(j%3===0){const vx=x+76,vy=depth+9,length=50+hash(j+64)*72;ctx.fillStyle='rgba(41,70,41,.8)';for(let k=0;k<length/7;k++){const dx=Math.floor(Math.sin(k*.25+j)*3)*2;ctx.fillRect(vx+dx,vy+k*7,2,9);if(k%3===0){ctx.fillStyle='rgba(68,99,49,.75)';ctx.fillRect(vx+dx-6,vy+k*7,6,4);ctx.fillRect(vx+dx+2,vy+k*7+6,6,4);ctx.fillStyle='rgba(41,70,41,.8)'}}}
    }
    ctx.save();ctx.globalCompositeOperation='screen';
    for(let j=0;j<4;j++){const x=650+j*95+Math.sin(t*.07)*22;const beam=ctx.createLinearGradient(x,0,x-230,600);beam.addColorStop(0,'rgba(213,221,172,.09)');beam.addColorStop(1,'rgba(130,162,125,0)');ctx.fillStyle=beam;ctx.beginPath();ctx.moveTo(x,0);ctx.lineTo(x+32,0);ctx.lineTo(x-180,610);ctx.lineTo(x-400,610);ctx.closePath();ctx.fill()}
    ctx.restore();
    const fog=ctx.createLinearGradient(0,280,0,610);fog.addColorStop(0,'rgba(97,130,108,0)');fog.addColorStop(.5,'rgba(117,145,116,.075)');fog.addColorStop(1,'rgba(8,24,22,.03)');ctx.fillStyle=fog;ctx.fillRect(0,220,VW,430);
    for(let j=0;j<(this.quality==='low'?12:30);j++){const x=Math.round(((hash(j+2)*VW+t*(2+hash(j+22)*4))%VW)/2)*2,y=Math.round((90+hash(j+80)*470+Math.sin(t*.5+j)*10)/2)*2;ctx.globalAlpha=.19+Math.sin(t+j)*.14;ctx.fillStyle=j%3?'#b8c68c':'#f4cf87';ctx.fillRect(x,y,2,2);if(j%5===0){ctx.globalAlpha*=.4;ctx.fillRect(x-2,y,6,2);ctx.fillRect(x,y-2,2,6)}}ctx.globalAlpha=1;
    if(this.starlight){
      // The permanent completion reward: two tiny flames joined in the sky.
      const points=[[737,153],[758,133],[781,143],[804,119],[828,141],[853,129],[877,151]],twinkle=.65+Math.sin(t*.9)*.12;
      ctx.save();ctx.globalAlpha=.23;ctx.strokeStyle='#cfddbd';ctx.lineWidth=1;ctx.beginPath();points.forEach(([x,y],i)=>i?ctx.lineTo(x,y):ctx.moveTo(x,y));ctx.stroke();
      points.forEach(([x,y],i)=>{ctx.globalAlpha=twinkle+Math.sin(t*1.7+i)*.16;ctx.fillStyle=i%2?'#91d9e5':'#f1d28e';ctx.fillRect(x-2,y-2,4,4);ctx.globalAlpha*=.4;ctx.fillRect(x-5,y-1,10,2);ctx.fillRect(x-1,y-5,2,10)});
      for(let side=0;side<2;side++){
        const x=side?877:737,y=side?151:153;ctx.globalAlpha=.7;ctx.fillStyle=side?'#6cbacb':'#dab26c';
        [[0,-15,2,6],[-2,-11,6,8],[-6,-5,12,10],[-4,5,8,2]].forEach(([dx,dy,w,h])=>ctx.fillRect(x+dx,y+dy,w,h));
        ctx.fillStyle=side?'#c2f3ed':'#fff1b8';ctx.fillRect(x-2,y-2,4,6);
      }
      ctx.restore();
    }
  }
  _scenery(ctx){
    const l=this.level,t=this.ambientTime;
    if(l.theme==='ruins')for(let j=0;j<Math.ceil((l.width||3000)/440);j++){const x=j*440+120;ctx.fillStyle='rgba(58,67,59,.36)';ctx.fillRect(x,260,46,300);ctx.fillRect(x-9,252,64,19);ctx.fillRect(x+160,210,40,350);ctx.beginPath();ctx.arc(x+103,260,103,Math.PI,0);ctx.lineTo(x+178,260);ctx.arc(x+103,260,75,0,Math.PI,true);ctx.fill();ctx.fillStyle='rgba(28,46,35,.45)';ctx.fillRect(x+4,260,4,260)}
    if(l.theme==='summit')for(let j=-1;j<10;j++){const x=j*420;ctx.fillStyle='rgba(110,131,143,.19)';ctx.beginPath();ctx.moveTo(x,650);ctx.lineTo(x+230,170+hash(j)*120);ctx.lineTo(x+520,650);ctx.fill();ctx.fillStyle='rgba(202,211,207,.11)';ctx.beginPath();ctx.moveTo(x+230,170+hash(j)*120);ctx.lineTo(x+155,340);ctx.lineTo(x+235,300);ctx.lineTo(x+315,350);ctx.fill()}
    // Quiet ferns beneath the routes.
    for(let j=0;j<Math.ceil((l.width||3000)/85);j++){const x=j*85+hash(j)*60,y=613+hash(j+2)*20;this._fern(ctx,x,y,30+hash(j+4)*30,'rgba(17,45,31,.7)',t)}
  }
  _platform(ctx,p){
    if(p.x+p.w<this.camera-60||p.x>this.camera+VW+60)return;
    if(p.broken>0){ctx.fillStyle='rgba(131,139,112,.1)';ctx.fillRect(p.x,p.y,p.w,3);return}
    const x=p.x,y=p.y+(p.type==='crumble'&&p.drop?Math.sin(this.ambientTime*50)*1.8:0),w=p.w,h=p.h||42;
    ctx.save();
    ctx.shadowBlur=18;ctx.shadowColor='rgba(0,0,0,.5)';ctx.shadowOffsetY=12;
    const grad=ctx.createLinearGradient(x,y,x,y+h);grad.addColorStop(0,p.type==='ice'?'#769c9e':'#777668');grad.addColorStop(.15,p.type==='ice'?'#3a6672':'#444b42');grad.addColorStop(1,p.type==='ice'?'#1e3a48':'#172420');ctx.fillStyle=grad;
    ctx.beginPath();ctx.moveTo(x+5,y);ctx.lineTo(x+w-4,y);ctx.lineTo(x+w,y+9);ctx.lineTo(x+w-4,y+h-5);ctx.lineTo(x+w-18,y+h+4);ctx.lineTo(x+12,y+h);ctx.lineTo(x,y+h-10);ctx.lineTo(x,y+7);ctx.closePath();ctx.fill();ctx.shadowBlur=0;ctx.shadowOffsetY=0;
    // Mineral grain, vertical seams, and an irregular illuminated moss edge.
    for(let j=0;j<Math.floor(w/9);j++){const seed=Math.floor(p.originX||p.x)+j*3,sx=x+hash(seed)*w,sy=y+5+hash(seed+7)*Math.max(10,h-8);ctx.fillStyle=`rgba(${hash(seed+3)>.5?'185,181,150':'5,19,16'},${.07+hash(seed+1)*.11})`;ctx.fillRect(sx,sy,2+hash(seed+4)*10,1+hash(seed+5)*4)}
    ctx.strokeStyle='rgba(4,15,13,.4)';ctx.lineWidth=1;for(let j=0;j<w/55;j++){const sx=x+23+j*57;ctx.beginPath();ctx.moveTo(sx,y+7);ctx.lineTo(sx+8,y+h*.5);ctx.lineTo(sx-2,y+h);ctx.stroke()}
    if(p.type==='ice'){ctx.strokeStyle='rgba(191,231,233,.6)';ctx.beginPath();ctx.moveTo(x+5,y+2);ctx.lineTo(x+w-5,y+2);ctx.stroke();for(let j=0;j<w/19;j++){ctx.fillStyle='rgba(159,212,221,.28)';ctx.beginPath();ctx.moveTo(x+j*19,y);ctx.lineTo(x+j*19+8,y+20+hash(j)*12);ctx.lineTo(x+j*19+13,y);ctx.fill()}}
    else{
      const moss=p.type==='crumble'?'#8a8058':p.type==='bounce'?'#5a9664':'#617547';ctx.fillStyle=moss;ctx.beginPath();ctx.moveTo(x,y+6);for(let j=0;j<=w/9;j++)ctx.lineTo(x+Math.min(w,j*9),y-2-hash(j+(p.originX||p.x))*5);ctx.lineTo(x+w,y+7);ctx.closePath();ctx.fill();
      ctx.strokeStyle='rgba(168,173,107,.45)';ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(x+5,y-1);ctx.lineTo(x+w-6,y-1);ctx.stroke();
      for(let j=0;j<Math.floor(w/19);j++){const sx=x+7+j*19;ctx.strokeStyle=j%2?'#66824d':'#3f633e';ctx.beginPath();ctx.moveTo(sx,y+1);ctx.lineTo(sx-3,y-5-hash(j)*8);ctx.lineTo(sx+2,y-1);ctx.lineTo(sx+6,y-8);ctx.stroke()}
    }
    if(p.type==='moving'){ctx.strokeStyle='rgba(224,192,126,.4)';ctx.setLineDash([3,9]);ctx.beginPath();ctx.moveTo(x+w/2,y+h);ctx.lineTo(x+w/2,y+h+18);ctx.stroke();ctx.setLineDash([]);ctx.fillStyle='#c2a56e';ctx.fillRect(x+w/2-9,y+12,18,2)}
    if(p.type==='crumble'){ctx.strokeStyle='#c0ac78';ctx.beginPath();ctx.moveTo(x+w*.43,y+3);ctx.lineTo(x+w*.52,y+15);ctx.lineTo(x+w*.46,y+23);ctx.lineTo(x+w*.56,y+h);ctx.stroke()}
    if(p.type==='bounce'){for(let j=0;j<Math.floor(w/28);j++){ctx.fillStyle='#8cba7b';ctx.beginPath();ctx.ellipse(x+17+j*28,y-4,12,5,0,0,Math.PI*2);ctx.fill();ctx.fillStyle='#283f2c';ctx.fillRect(x+15+j*28,y-2,4,8)}}
    if(w>180&&p.type!=='ice'){this._fern(ctx,x+22,y,18,'#46663b',this.ambientTime);this._fern(ctx,x+w-27,y,22,'#4e703f',this.ambientTime)}
    ctx.restore();
  }
  _fern(ctx,x,y,size,color,t){ctx.save();ctx.strokeStyle=color;ctx.lineWidth=1.3;const sway=Math.sin(t*.5+x)*3;for(let side=-1;side<=1;side+=2){ctx.beginPath();ctx.moveTo(x,y);ctx.quadraticCurveTo(x+side*size*.3+sway,y-size*.6,x+side*size*.65+sway,y-size);ctx.stroke();for(let j=1;j<6;j++){const r=j/6,px=x+side*size*.6*r+sway*r,py=y-size*r;ctx.beginPath();ctx.moveTo(px,py);ctx.lineTo(px-side*size*.27*(1-r),py-size*.18);ctx.moveTo(px,py);ctx.lineTo(px+side*size*.25*(1-r),py+size*.04);ctx.stroke()}}ctx.restore()}
  _hazard(ctx,h){
    if(h.x+h.w<this.camera-50||h.x>this.camera+VW+50)return;
    const t=this.ambientTime,active=!h.period||Math.sin(this.time*Math.PI*2/h.period+(h.phase||0))>=-.25;
    ctx.save();ctx.globalAlpha=active?1:.25;
    if(h.type==='water'){
      const g=ctx.createLinearGradient(0,h.y,0,h.y+h.h);g.addColorStop(0,'rgba(67,122,132,.52)');g.addColorStop(.25,'rgba(16,57,66,.7)');g.addColorStop(1,'#071c24');ctx.fillStyle=g;ctx.fillRect(h.x,h.y,h.w,h.h);
      ctx.strokeStyle='rgba(140,190,190,.5)';ctx.lineWidth=1.4;ctx.beginPath();for(let j=0;j<=h.w;j+=6){const y=h.y+Math.sin(j*.042+t*1.7)*2.2;j?ctx.lineTo(h.x+j,y):ctx.moveTo(h.x,y)}ctx.stroke();
      for(let j=0;j<h.w/25;j++){const x=h.x+hash(j)*h.w,y=h.y+7+hash(j+8)*h.h;ctx.fillStyle='rgba(135,186,179,.15)';ctx.fillRect(x,y,10+hash(j+4)*14,1)}
    }else if(h.type==='spikes'){
      for(let j=0;j<h.w/19;j++){const x=h.x+j*19;const g=ctx.createLinearGradient(x,h.y,x+14,h.y+h.h);g.addColorStop(0,'#a3a99b');g.addColorStop(1,'#24372e');ctx.fillStyle=g;ctx.beginPath();ctx.moveTo(x,h.y+h.h);ctx.lineTo(x+9,h.y);ctx.lineTo(x+18,h.y+h.h);ctx.fill();ctx.strokeStyle='rgba(183,186,156,.35)';ctx.beginPath();ctx.moveTo(x+9,h.y+2);ctx.lineTo(x+7,h.y+h.h);ctx.stroke()}
    }else if(h.type==='rain'){
      const grad=ctx.createLinearGradient(0,h.y,0,h.y+h.h);grad.addColorStop(0,'rgba(128,169,183,.02)');grad.addColorStop(1,'rgba(128,169,183,.08)');ctx.fillStyle=grad;ctx.fillRect(h.x,h.y,h.w,h.h);ctx.strokeStyle='rgba(168,200,209,.45)';ctx.lineWidth=1;
      for(let j=0;j<(this.quality==='low'?12:32);j++){const x=h.x+hash(j+13)*h.w,y=h.y+((hash(j+8)*h.h+t*(180+hash(j)*160))%h.h);ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x-5,y+17);ctx.stroke()}
    }else if(h.type==='wind'){
      const direction=(h.force||-170)<0?-1:1;ctx.strokeStyle='rgba(193,211,181,.23)';ctx.lineWidth=1;for(let j=0;j<10;j++){const x=h.x+((hash(j)*h.w+t*90*direction)%h.w+h.w)%h.w,y=h.y+hash(j+7)*h.h+Math.sin(t+j)*7;ctx.beginPath();ctx.moveTo(x,y);ctx.quadraticCurveTo(x+20*direction,y-5,x+48*direction,y);ctx.stroke()}
      ctx.font='12px system-ui';ctx.fillStyle='rgba(200,217,186,.55)';ctx.fillText(direction<0?'‹ ‹ ‹':'› › ›',h.x+h.w*.5-15,h.y+14);
    }ctx.restore();
  }
  _candle(ctx,x,y,lit,goal){
    const t=this.ambientTime,w=goal?30:19,h=goal?60:38;
    ctx.save();
    if(lit||goal){this._glow(ctx,x,y-h-12,goal?140:90,goal?'255,180,62':'255,167,57',goal?.2:.16)}
    ctx.fillStyle='#5b5a46';ctx.beginPath();ctx.ellipse(x,y+1,w*.9,5,0,0,Math.PI*2);ctx.fill();
    const wax=ctx.createLinearGradient(x-w/2,0,x+w/2,0);wax.addColorStop(0,'#8e8060');wax.addColorStop(.35,'#e4d2a0');wax.addColorStop(.68,'#c2ad7d');wax.addColorStop(1,'#776b51');ctx.fillStyle=wax;ctx.fillRect(x-w/2,y-h,w,h);ctx.beginPath();ctx.ellipse(x,y-h,w/2,4,0,0,Math.PI*2);ctx.fill();
    ctx.strokeStyle='#e1cea0';ctx.lineWidth=goal?4:3;ctx.lineCap='round';ctx.beginPath();ctx.moveTo(x-w*.3,y-h+2);ctx.lineTo(x-w*.3,y-h+15);ctx.moveTo(x+w*.2,y-h+2);ctx.lineTo(x+w*.2,y-h+24);ctx.stroke();
    ctx.strokeStyle='#211d12';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(x,y-h+1);ctx.lineTo(x+1,y-h-7);ctx.stroke();
    if(lit){ctx.shadowBlur=15;ctx.shadowColor='#ffae4d';ctx.fillStyle='#ffb94f';ctx.beginPath();ctx.moveTo(x,y-h-6);ctx.bezierCurveTo(x-10,y-h-13,x-4,y-h-25,x+Math.sin(t*6)*3,y-h-32);ctx.bezierCurveTo(x+2,y-h-20,x+11,y-h-10,x,y-h-6);ctx.fill();ctx.fillStyle='#fff5c0';ctx.beginPath();ctx.ellipse(x,y-h-12,3,6,0,0,Math.PI*2);ctx.fill()}
    else if(goal){const pulse=.6+Math.sin(t*2)*.2;ctx.globalAlpha=pulse;ctx.strokeStyle='#d4b77b';ctx.lineWidth=1;ctx.setLineDash([3,7]);ctx.beginPath();ctx.ellipse(x,y-h*.55,42,57,0,0,Math.PI*2);ctx.stroke();ctx.setLineDash([]);ctx.globalAlpha=1}
    ctx.shadowBlur=0;
    if(goal){ctx.font='600 10px system-ui';ctx.textAlign='center';ctx.letterSpacing='2px';ctx.fillStyle='#e4cf9d';ctx.fillText('МАЯК',x,y-h-47);ctx.letterSpacing='0px'}
    ctx.restore();
  }
  _ember(ctx,x,y,index){const t=this.ambientTime,fy=y+Math.sin(t*2+index)*5;ctx.save();this._glow(ctx,x,fy,38,'255,175,63',.14);ctx.translate(x,fy);ctx.rotate(Math.sin(t+index)*.12);ctx.shadowColor='#ffc86c';ctx.shadowBlur=13;ctx.fillStyle='#f5cc7e';ctx.beginPath();ctx.moveTo(0,-8);ctx.quadraticCurveTo(2,-2,6,0);ctx.quadraticCurveTo(2,2,0,8);ctx.quadraticCurveTo(-2,2,-6,0);ctx.quadraticCurveTo(-2,-2,0,-8);ctx.fill();ctx.fillStyle='#fff1bc';ctx.fillRect(-1,-2,2,4);ctx.restore()}
  _glow(ctx,x,y,r,color,alpha){const g=ctx.createRadialGradient(x,y,0,x,y,r);g.addColorStop(0,`rgba(${color},${alpha})`);g.addColorStop(.35,`rgba(${color},${alpha*.45})`);g.addColorStop(1,`rgba(${color},0)`);ctx.fillStyle=g;ctx.fillRect(x-r,y-r,r*2,r*2)}
  _flame(ctx,p,index){
    if(p.deadTimer>0&&Math.floor(p.deadTimer*16)%2)return;
    const t=this.ambientTime,cyan=index===1,tetherHeat=this.mode==='coop'&&this.players.length===2?clamp(1-(Math.hypot(this.players[0].x-this.players[1].x,this.players[0].y-this.players[1].y)-220)/650,.18,1):1,heat=clamp(p.fuel/100,.2,1)*tetherHeat,x=Math.round(p.x/2)*2,y=Math.round(p.y/2)*2;
    ctx.save();this._glow(ctx,x,y-19,105*heat,cyan?'74,208,232':'255,150,34',.25*heat);
    ctx.fillStyle='rgba(0,0,0,.32)';ctx.fillRect(x-14,y+2,28,4);ctx.fillRect(x-10,y+6,20,2);
    if(p.dash>0){ctx.globalAlpha=.28;ctx.fillStyle=cyan?'#81e7ef':'#ffd07d';for(let j=1;j<5;j++)ctx.fillRect(x-p.face*j*12-8,y-30+j*2,14,20-j*3);ctx.globalAlpha=1}
    const colors=cyan?['','#176d8a','#36b8cf','#85e4e7','#dcfff2']:['','#bc4c25','#ed882c','#ffbd4c','#fff0a6'];
    const sprite=['.....1....','.....11...','....122...','...1221...','.1.12321..','.11123321.','112233321.','1223333221','1234443321','1234443321','.12333321.','..122221..','...1111...'];
    const bob=p.grounded?Math.round(Math.sin(t*5))*2:0,lean=Math.round(clamp(p.vx/100,-2,2));
    for(let row=0;row<sprite.length;row++)for(let col=0;col<sprite[row].length;col++){const digit=Number(sprite[row][col]);if(digit){ctx.fillStyle=colors[digit];const flick=row<4?Math.round(Math.sin(t*10+row)*.6)*2:0;ctx.fillRect(x-20+col*4+(row<6?-lean*2:0)+flick,y-52+row*4+bob,4,4)}}
    ctx.fillStyle=cyan?'#174451':'#683916';ctx.fillRect(x-8+p.face*2,y-16+bob,4,6);ctx.fillRect(x+4+p.face*2,y-16+bob,4,6);ctx.fillStyle='#fff8d4';ctx.fillRect(x-8+p.face*2,y-16+bob,2,2);ctx.fillRect(x+4+p.face*2,y-16+bob,2,2);
    if(heat<.35){ctx.fillStyle='rgba(5,17,22,.45)';ctx.fillRect(x-20,y-53,40,54)}
    ctx.restore();
    if(this.players.length>1){ctx.save();ctx.font='600 10px monospace';ctx.textAlign='center';ctx.fillStyle=cyan?'#a9eaf1':'#ffe0a2';ctx.fillText(index?'02':'01',x,y-65);ctx.restore()}
  }
  _link(ctx){const[a,b]=this.players;if(a.deadTimer||b.deadTimer)return;const dist=Math.hypot(a.x-b.x,a.y-b.y),danger=clamp((dist-280)/420,0,1),midX=(a.x+b.x)/2,midY=(a.y+b.y)/2+Math.min(90,dist*.17);ctx.save();ctx.strokeStyle=danger>.4?`rgba(255,123,82,${.5+danger*.3})`:'rgba(234,203,133,.4)';ctx.shadowBlur=8;ctx.shadowColor='#ffc86c';ctx.lineWidth=1.2;ctx.setLineDash(danger>.6?[4,5]:[]);ctx.beginPath();ctx.moveTo(a.x,a.y-18);ctx.quadraticCurveTo(midX,midY,b.x,b.y-18);ctx.stroke();ctx.restore()}
  _switch(ctx,s){const active=this.switchActive.has(s.id);ctx.save();if(active)this._glow(ctx,s.x,s.y-5,42,'113,225,183',.2);ctx.fillStyle='#414b3c';ctx.fillRect(s.x-22,s.y-6,44,7);ctx.fillStyle=active?'#b3e1b4':'#b38e55';ctx.fillRect(s.x-17,s.y-(active?4:9),34,4);ctx.strokeStyle=active?'#b1efc2':'#d8b97c';ctx.beginPath();ctx.moveTo(s.x-5,s.y-18);ctx.lineTo(s.x,s.y-23);ctx.lineTo(s.x+5,s.y-18);ctx.stroke();ctx.restore()}
  _gate(ctx,g){ctx.save();const grad=ctx.createLinearGradient(g.x,g.y,g.x+g.w,g.y);grad.addColorStop(0,'rgba(27,51,43,.8)');grad.addColorStop(.5,'rgba(103,139,110,.65)');grad.addColorStop(1,'rgba(24,48,40,.8)');ctx.fillStyle=grad;ctx.fillRect(g.x,g.y,g.w,g.h);ctx.strokeStyle='#7e9876';ctx.lineWidth=2;for(let j=0;j<g.w;j+=15){ctx.beginPath();ctx.moveTo(g.x+j,g.y);ctx.bezierCurveTo(g.x+j+12,g.y+g.h*.35,g.x+j-12,g.y+g.h*.6,g.x+j,g.y+g.h);ctx.stroke()}ctx.strokeStyle='rgba(197,202,139,.6)';ctx.lineWidth=1;ctx.strokeRect(g.x+2,g.y+2,g.w-4,g.h-4);ctx.restore()}
  _foreground(ctx){
    const from=Math.floor(this.camera/240)-1,to=Math.ceil((this.camera+VW)/240)+1;
    for(let j=from;j<to;j++){if(j%3===1){const x=j*240+95;ctx.fillStyle='rgba(2,13,11,.73)';ctx.beginPath();ctx.moveTo(x,650);ctx.quadraticCurveTo(x-23,620,x-12,585);ctx.quadraticCurveTo(x+16,615,x+44,650);ctx.fill();this._fern(ctx,x+34,650,48,'rgba(11,35,23,.8)',this.ambientTime)}}
    const fog=ctx.createLinearGradient(0,590,0,650);fog.addColorStop(0,'rgba(4,16,15,0)');fog.addColorStop(1,'rgba(3,12,12,.7)');ctx.fillStyle=fog;ctx.fillRect(this.camera,590,VW,60);
  }
}

export const PHYSICS=Object.freeze({viewportWidth:VW,viewportHeight:VH,playerWidth:PW,playerHeight:PH,gravity:GRAVITY,speed:SPEED,jump:JUMP});
