import {GameEngine} from './engine.js';
import {SOLO_LEVELS,COOP_LEVELS,CHAPTERS,EASTER_EGG} from './levels.js';
import {OnlineSession} from './online.js';
import {loadProgress,saveProgress,recordResult,totalStars,completedCount,canPlay,raceUnlocked,eggUnlocked} from './progress.js';

const $=s=>document.querySelector(s);
const $$=s=>[...document.querySelectorAll(s)];
const icon=(name,cls='')=>`<svg class="${cls}" aria-hidden="true"><use href="#i-${name}"/></svg>`;
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const fmt=t=>`${Math.floor(Math.max(0,t||0)/60)}:${String(Math.floor(Math.max(0,t||0)%60)).padStart(2,'0')}`;
const pad=n=>String(n).padStart(2,'0');
let progress=loadProgress(),mode='solo',chapter=0,selected=1,session=null,runId='',playing=false,networkTimer=null,toastTimer=null,captionTimer=null,dialogKind='',modalOnClose=null,eggWasShown=eggUnlocked(progress);
const modal=$('#modal'),content=$('#modal-content');
const levels=()=>mode==='coop'?COOP_LEVELS:SOLO_LEVELS;
const currentLevel=()=>levels().find(l=>l.id===selected)||levels()[0];
const game=new GameEngine($('#game'),{
  onHUD:updateHUD,
  onDeath:({reason})=>toast(`${reason}. Возвращаемся к последней свече.`),
  onComplete:finishLevel,
  onPause:value=>{if(session?.connected){if(session.role==='guest')session.send({type:'pause-request',paused:value,id:runId});else session.send({type:'pause',paused:value,id:runId})}if(value)showPause();else closeModal(false)},
});
game.setMuted(progress.settings.muted);game.setQuality(progress.settings.quality);game.setStarlight(eggUnlocked(progress));

function persist(){if(!saveProgress(progress)){const label=$('.save-status');label.lastChild.textContent=' Сохранение недоступно';toast('Браузер запретил сохранение. Не закрывай вкладку, чтобы сохранить текущий прогресс.')}}
function toast(message){$('#toast').textContent=message;$('#toast').classList.add('show');clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('#toast').classList.remove('show'),4300)}
function showModal(html,kind='',onClose=null){modalOnClose=null;content.innerHTML=html;dialogKind=kind;modalOnClose=onClose;if(!modal.open)modal.showModal()}
function closeModal(resume=true){const action=modalOnClose;modalOnClose=null;modal.close();dialogKind='';if(resume)action?.();$('#game').focus({preventScroll:true})}
$('#modal-close').onclick=()=>closeModal();
modal.addEventListener('cancel',e=>{e.preventDefault();closeModal()});
modal.addEventListener('click',e=>{if(e.target===modal){const r=modal.getBoundingClientRect();if(e.clientX<r.left||e.clientX>r.right||e.clientY<r.top||e.clientY>r.bottom)closeModal()}});

function updateUI(){
  const baseMode=mode==='race'?'coop':mode;
  $('#star-total').innerHTML=`${totalStars(progress,baseMode)}<span> / 90</span>`;
  $('#level-total').innerHTML=`${completedCount(progress,baseMode)}<span> / 30</span>`;
  $$('.nav-item').forEach(b=>b.classList.toggle('active',b.dataset.mode===mode));
  $('.nav-item[data-mode="race"] .tiny-icon use').setAttribute('href',raceUnlocked(progress)?'#i-clock':'#i-lock');
  const texts={solo:['Пока горит свет.','Найди дорогу сквозь темноту. И сохрани то, что согревает.','Твоя история','Каждый шаг зажигает что-то новое.'],coop:['Вместе — ярче.','Два огонька. Одна нить тепла. Не отпускайте друг друга.','Общая история','Парные печати, общие свечи и свет, который связывает.'],race:['Навстречу рассвету.','Один маршрут для двоих. Кто первым зажжёт последнюю свечу?','Гонка огоньков','30 знакомых маршрутов. Два личных времени. Один победитель.']};
  const t=texts[mode];$('#page-title').textContent=t[0];$('#page-subtitle').textContent=t[1];$('#levels-title').textContent=t[2];$('#levels-subtitle').textContent=t[3];
  $('#mode-options').innerHTML=icon('users')+(session?.connected?` Лобби ${esc(session.code)}`:' Играть с другом');
  $$('.chapter-tab').forEach((b,i)=>{const c=CHAPTERS[i],stars=totalStars(progress,baseMode);b.classList.toggle('active',i===chapter);b.querySelector('span:nth-child(2)').textContent=c.name;const n=Array.from({length:10},(_,k)=>progress[baseMode]?.[i*10+k+1]?.stars||0).reduce((a,b)=>a+b,0);$(`#chapter-stars-${i}`).innerHTML=stars>=c.requiredStars||mode==='race'?`${n} / 30 <span>★</span>`:`${c.requiredStars} ★ для входа`;b.setAttribute('aria-selected',i===chapter)});
  const c=CHAPTERS[chapter];$('#chapter-description').textContent=c.subtitle;$('#chapter-range').textContent=`УРОВНИ ${pad(chapter*10+1)} — ${chapter*10+10}`;
  renderLevels();updateSoundButton();
}

function renderLevels(){
  const group=levels().slice(chapter*10,chapter*10+10);
  $('#level-grid').innerHTML=group.map(l=>{const data=progress[mode]?.[l.id],unlocked=canPlay(progress,mode,l.id);return `<button class="level-card ${unlocked?'':'locked'} ${l.id===selected?'selected':''}" data-level="${l.id}" aria-label="Уровень ${l.id}: ${esc(l.name)}. ${unlocked?(data?`${data.stars||0} звёзд`:'Доступен'):'Закрыт'}"><canvas width="240" height="90" class="level-art" data-thumb="${l.id}"></canvas>${!unlocked?`<span class="level-lock">${icon('lock')}</span>`:l.id===selected?'<span class="level-tag">ТВОЙ СЛЕДУЮЩИЙ ШАГ</span>':''}<div class="level-body"><div class="level-title"><span class="level-index">${pad(l.id)}</span>${esc(l.name)}</div><div class="level-bottom"><span class="level-stars">${[0,1,2].map(i=>icon('star',i<(data?.stars||0)?'earned':'')).join('')}</span><span class="level-time">${data?.time?fmt(data.time):fmt(l.parTime)} ${data?.time?'ЛУЧШЕЕ':'ЦЕЛЬ'}</span></div></div></button>`}).join('');
  $$('[data-thumb]').forEach(c=>drawThumbnail(c,levels().find(l=>l.id===+c.dataset.thumb)));
  $$('[data-level]').forEach(b=>b.onclick=()=>{const id=+b.dataset.level;if(!canPlay(progress,mode,id)){const required=CHAPTERS[Math.floor((id-1)/10)].requiredStars;toast(totalStars(progress,mode)<required?`Для этой главы нужно ${required} звёзд. Переиграй знакомые уровни на три звезды.`:'Сначала зажги свечу предыдущего уровня.');return}selected=id;if(mode!=='solo'&&!session?.connected){showCoop();return}beginLevel()});
}

function drawThumbnail(canvas,l){const c=canvas.getContext('2d');c.imageSmoothingEnabled=false;c.fillStyle=l.theme==='summit'?'#192a32':l.theme==='ruins'?'#1b2729':'#19352b';c.fillRect(0,0,240,90);let seed=l.id*947;const rnd=()=>{seed=(seed*16807)%2147483647;return seed/2147483647};for(let layer=0;layer<3;layer++){c.fillStyle=['#234335','#18372a','#10291e'][layer];for(let j=0;j<9;j++){const x=j*33+Math.floor(rnd()*15),w=4+layer*3;c.fillRect(x,0,w,90);c.fillRect(x-7,Math.floor(rnd()*40),17,3)}}c.fillStyle='#c8c79555';c.fillRect(175,10,9,9);c.fillStyle='#00000020';c.fillRect(0,0,240,90);const count=7,start=(l.id*2)%4;for(let j=0;j<count;j++){const p=l.platforms[(start+j)%l.platforms.length];const x=j*38-9,y=Math.floor(43+(p.y-400)*.17);c.fillStyle='#263c2b';c.fillRect(x,y,28,90-y);c.fillStyle=p.type==='ice'?'#68999c':p.type==='crumble'?'#8c8050':'#6b8050';c.fillRect(x,y,28,3);c.fillStyle='#49603b';c.fillRect(x+2,y+3,24,2)}const px=48,py=42;c.fillStyle='#ed8a3e';c.fillRect(px,py,7,9);c.fillRect(px+2,py-3,3,4);c.fillStyle='#ffe3a0';c.fillRect(px+2,py+3,3,5);if(mode==='coop'){c.fillStyle='#7cd6e1';c.fillRect(px+15,py,6,8);c.fillRect(px+17,py-3,2,4)}c.fillStyle='#e2c797';c.fillRect(209,33,4,12);c.fillStyle='#f0ad5a';c.fillRect(209,28,4,4);for(let i=0;i<8;i++){c.fillStyle=i%2?'#d7bc6f66':'#b8c19533';c.fillRect(Math.floor(rnd()*240),Math.floor(rnd()*65),1,1)}}

function showDemo(){playing=false;$('#game-shell').classList.remove('playing');$('#intro').classList.remove('hidden');$('#hud').classList.add('hidden');$('#scene-bottom').classList.remove('hidden');$('#link-indicator').classList.add('hidden');$('#touch-controls').classList.add('hidden');$('#story-caption').classList.add('hidden');
  const source=currentLevel();game.load({...source,width:1400,height:650,spawn:{x:817,y:475},platforms:[{x:0,y:570,w:300,h:110,type:'stone'},{x:330,y:550,w:190,h:120,type:'stone'},{x:625,y:518,w:300,h:150,type:'stone'},{x:1005,y:425,w:300,h:250,type:'stone'}],embers:[{x:960,y:400}],hazards:[],switches:[],gates:[],candles:[],goal:{x:1155,y:425}},{mode:mode==='solo'?'solo':'coop'});game.players[0].y=518;if(game.players[1]){game.players[1].x=865;game.players[1].y=518}
  $('#intro-chapter').textContent=mode==='solo'?'ГЛАВА I · НАЧАЛО ПУТИ':mode==='coop'?'ДВА ОГОНЬКА · ОДНА ИСТОРИЯ':'ИСПЫТАНИЕ · СКОРОСТЬ СВЕТА';
  $('#intro-title').innerHTML=mode==='solo'?'Всё начинается<br>с одной искры.':mode==='coop'?'Там, где ты,<br>всегда теплее.':'Кто первым<br>встретит рассвет?';
  $('#intro-copy').innerHTML=mode==='solo'?'Лес забыл, как выглядит рассвет.<br>Только маленький огонёк ещё помнит.<br>Донеси его до последней свечи.':mode==='coop'?'Свет одного хранит тепло другого.<br>Решайте загадки и держитесь рядом.<br>Вместе вы зажжёте целый мир.':'Одинаковый паркур. Две свечи надежды.<br>Прыгай, делай рывки, сокращай путь.<br>Пусть победит самый быстрый огонёк.';
  $('#play-btn span').textContent=mode==='solo'?(completedCount(progress,'solo')?'Продолжить историю':'Зажечь историю'):mode==='coop'?'Зажечь вместе':'Начать забег';
  $('#scene-location').textContent=CHAPTERS[chapter].name.toUpperCase();
}

function switchMode(value){if(playing){toast('Закончи уровень или вернись к карте через паузу.');return}if(session?.connected&&session.role==='guest'){toast('Создатель лобби выбирает режим и уровень.');return}if(session?.connected&&value==='solo'){closeOnline()}if(value==='race'&&!raceUnlocked(progress)){showModal(`<div class="modal-eyebrow">СЛЕДУЮЩАЯ ИСКРА</div><h2>Сначала — вместе.</h2><p>Режим забегов откроется после всех 30 уровней совместной истории. В нём вы пройдёте один и тот же паркур на скорость, с отдельным секундомером для каждого.</p><div class="race-lock">Совместная история: <b>${completedCount(progress,'coop')} / 30</b><br>Нить тепла в гонке отключена — выбирайте собственный темп.</div><div class="modal-actions"><button class="button primary" id="goto-coop">К общей истории ${icon('arrow')}</button></div>`,'race-lock');$('#goto-coop').onclick=()=>{closeModal();switchMode('coop')};return}mode=value;chapter=0;selected=nextLevel();updateUI();showDemo()}
function nextLevel(){const found=levels().find(l=>canPlay(progress,mode,l.id)&&!progress[mode]?.[l.id]);return found?.id||1}
$$('[data-mode]').forEach(b=>b.onclick=()=>switchMode(b.dataset.mode));
$$('[data-chapter]').forEach(b=>b.onclick=()=>{chapter=+b.dataset.chapter;updateUI()});
$('#play-btn').onclick=()=>{if(mode==='solo')beginLevel();else if(session?.connected)beginLevel();else showCoop()};
$('#quick-coop').onclick=()=>{switchMode('coop');showCoop()};
$('#mode-options').onclick=()=>{if(mode==='solo')switchMode('coop');showCoop()};

function beginLevel({remote=false,remoteMode,levelId,id}={}){
  if(!remote&&session?.connected&&mode==='solo')closeOnline();
  if(remote){mode=remoteMode;selected=levelId;chapter=Math.floor((selected-1)/10);runId=id;updateUI()}
  const l=currentLevel();if(!remote&&!canPlay(progress,mode,l.id)){toast('Заверши предыдущие уровни и собери звёзды для этой главы.');return}
  if(session?.connected&&session.role==='guest'&&!remote){toast('Уровень запускает создатель лобби.');return}
  closeModal(false);clearTimeout(captionTimer);playing=true;$('#game-shell').classList.add('playing');$('#intro').classList.add('hidden');$('#hud').classList.remove('hidden');$('#scene-bottom').classList.add('hidden');$('#link-indicator').classList.toggle('hidden',mode!=='coop');
  $('#touch-controls').classList.toggle('hidden',!matchMedia('(pointer:coarse)').matches);$('#hud-level').textContent=pad(l.id);$('#hud-name').textContent=l.name;$('#scene-location').textContent=CHAPTERS[l.chapter].name.toUpperCase();
  if(!remote)runId=crypto.randomUUID?.()||String(Date.now());
  game.load(l,{mode,online:!!session?.connected,role:session?.role||'host'});game.start();game.setMuted(progress.settings.muted);
  if(session?.connected&&!remote)session.send({type:'start',mode,levelId:l.id,id:runId});
  $('#story-caption').textContent=l.story+' '+l.hint;$('#story-caption').classList.remove('hidden');captionTimer=setTimeout(()=>$('#story-caption').classList.add('hidden'),7000);
  $('#game').focus({preventScroll:true});if(window.scrollY>180)$('#game-shell').scrollIntoView({behavior:'smooth',block:'center'});
}

function updateHUD(h){if(!h)return;$('#fuel-value').textContent=`${Math.max(0,Math.ceil(h.remaining))} с`;$('#fuel-value').title=`Запас тепла: ${Math.ceil(h.fuel)}%`;$('#fuel-bar').style.width=`${h.fuel}%`;$('#fuel-bar').style.background=h.fuel<25?'#e78255':'#e9ba67';$('#hud-time').textContent=fmt(h.time);$('#hud-embers').textContent=`${h.embers}/${h.totalEmbers}`;$('#hud-checkpoint').textContent=h.checkpoint?`Свеча зажжена · ${h.checkpoint}/${h.totalCheckpoints}`:'Найди первую свечу';if(mode==='coop'){const distance=Math.max(0,h.linkDistance||0);$('#link-bar').style.width=`${Math.max(0,100-distance/7)}%`;$('#link-bar').style.background=distance>400?'#e78b65':'#c9bd79';$('#link-text').textContent=distance>400?'Свет гаснет — ближе друг к другу!':distance>280?'Нить тепла слабеет':'Ваш свет связан'}}

function finishLevel(result){if(!playing)return;recordResult(progress,mode,selected,result);game.setStarlight(eggUnlocked(progress));persist();updateUI();const l=currentLevel(),race=mode==='race',conditions=result.conditions||[true,result.time<=l.parTime,result.embers===result.totalEmbers&&result.deaths===0];
  let html=`<div class="result-header"><div class="modal-eyebrow">${race?'ЗАБЕГ ЗАВЕРШЁН':'ЕЩЁ ОДНА СВЕЧА В ТЕМНОТЕ'}</div><h2>${race?'Скорость света.':'Твой свет добрался.'}</h2><p>Уровень ${pad(l.id)} · ${esc(l.name)}</p></div>`;
  if(race){const times=result.raceTimes||[result.time,result.time];html+=`<div class="result-stats"><div>${fmt(times[0])}<small>Янтарный огонёк</small></div><div>${fmt(times[1])}<small>Голубой огонёк</small></div></div><p class="result-header">${Math.abs(times[0]-times[1])<.1?'Рассвет встретили вместе!':times[0]<times[1]?'Янтарный огонёк первым зажёг свечу.':'Голубой огонёк первым зажёг свечу.'}</p>`}
  else html+=`<div class="result-stars">${[0,1,2].map(i=>icon('star',i<result.stars?'earned':'')).join('')}</div><div class="result-conditions">${[['Дойти до последней свечи','Свет сохранён'],['Уложиться во время',`${fmt(result.time)} / ${fmt(l.parTime)}`],['Все искры, без затуханий',`${result.embers}/${result.totalEmbers} искр · ${result.deaths} затуханий`]].map((row,i)=>`<div class="condition-row ${conditions[i]?'success':''}"><span>${icon(conditions[i]?'check':'star')}${row[0]}</span><span>${row[1]}</span></div>`).join('')}</div>`;
  const next=selected+1,nextOpen=next<=30&&canPlay(progress,mode,next),host=!session?.connected||session.role==='host';
  if(selected===30)html+=`<p>${mode==='solo'?'Маяк снова горит. Лес встретил свой первый рассвет.':mode==='coop'?'Два огонька зажгли Сердце леса. Теперь вам открыты совместные забеги на время.':'Последний маршрут позади. Всегда можно стать ещё быстрее.'}</p>`;
  else if(!nextOpen)html+=`<p>Следующая глава ждёт больше звёзд. Пройди знакомые тропинки быстрее и собери все искры.</p>`;
  html+=`<div class="modal-actions"><button class="button secondary" id="result-map">К уровням</button>${host?`<button class="button primary" id="result-next">${nextOpen?'Следующий уровень':'Ещё одна попытка'} ${icon('arrow')}</button>`:'<span class="network-status">Ждём выбор создателя лобби</span>'}</div>`;
  showModal(html,'results',()=>returnToMap());$('#result-map').onclick=()=>returnToMap();if($('#result-next'))$('#result-next').onclick=()=>{if(nextOpen){selected=next;chapter=Math.floor((next-1)/10)}beginLevel()};
  if(eggUnlocked(progress)&&!eggWasShown){eggWasShown=true;setTimeout(()=>showEasterEgg(),800)}
}

function returnToMap(notify=true){
  if(notify&&session?.connected&&runId)session.send({type:'map',id:runId});
  closeModal(false);clearTimeout(captionTimer);runId='';playing=false;game.setRemoteInput({});
  updateUI();showDemo();
  if(session?.connected&&session.role==='guest'){
    showModal('<div class="modal-eyebrow">ЛОББИ</div><h2>Новый путь впереди.</h2><p>Создатель лобби выбирает следующий уровень. Вы можете оставаться здесь или покинуть лобби.</p><button class="button secondary" id="leave-lobby">Покинуть лобби</button>','guest-wait');
    $('#leave-lobby').onclick=()=>{closeOnline();closeModal(false);updateUI();showDemo()};
  }
}
function pauseGame(value){game.pause(value);if(session?.connected)session.send({type:session.role==='guest'?'pause-request':'pause',paused:value,id:runId});if(value)showPause();else closeModal(false)}
function showPause(){if(!playing||game.status==='complete')return;showModal(`<div class="modal-eyebrow">ПЕРЕДОХНИ У ОГНЯ</div><h2>Свет подождёт.</h2><p>Уровень ${pad(selected)} · ${esc(currentLevel().name)}<br>${esc(currentLevel().hint)}</p><div class="modal-actions"><button class="button primary" id="resume-btn">${icon('play')} Продолжить</button><button class="button secondary" id="pause-map">К уровням</button></div><button class="button text-button" id="pause-help">${icon('help')} Управление и механики</button>`,'pause',()=>pauseGame(false));$('#resume-btn').onclick=()=>pauseGame(false);$('#pause-map').onclick=()=>returnToMap();$('#pause-help').onclick=showHelp}
$('#pause-btn').onclick=()=>pauseGame(true);
$('#restart-btn').onclick=()=>restartLevel();
function restartLevel(){if(!playing)return;if(session?.connected&&session.role==='guest'){session.send({type:'restart-request',id:runId});toast('Запрос на перезапуск отправлен.');return}beginLevel()}
window.addEventListener('keydown',e=>{if(e.code==='KeyR'&&playing&&!modal.open){e.preventDefault();restartLevel()}if(e.code==='KeyF'&&!modal.open&&!['INPUT','TEXTAREA'].includes(e.target.tagName)){e.preventDefault();toggleFullscreen()}});

function updateSoundButton(){const muted=progress.settings.muted;$('#sound-btn use').setAttribute('href',muted?'#i-mute':'#i-sound');$('#sound-btn').title=muted?'Включить звук':'Выключить звук';$('#sound-btn').setAttribute('aria-label',$('#sound-btn').title);$('#quality-btn').textContent=progress.settings.quality==='low'?'ЛЁГКАЯ ГРАФИКА':'ПИКСЕЛЬНАЯ ГРАФИКА'}
$('#sound-btn').onclick=()=>{progress.settings.muted=!progress.settings.muted;game.setMuted(progress.settings.muted);if(!progress.settings.muted)game._sound('candle');persist();updateSoundButton()};
$('#quality-btn').onclick=()=>{progress.settings.quality=progress.settings.quality==='high'?'low':'high';game.setQuality(progress.settings.quality);persist();updateSoundButton();toast(progress.settings.quality==='low'?'Меньше частиц. Тот же уютный пиксельный мир.':'Все пиксельные эффекты включены.')};
async function toggleFullscreen(){try{if(document.fullscreenElement)await document.exitFullscreen();else await $('#game-shell').requestFullscreen()}catch{toast('Этот браузер не разрешил полноэкранный режим.')}}
$('#fullscreen-btn').onclick=toggleFullscreen;
document.addEventListener('fullscreenchange',()=>game.resize());
$$('[data-touch]').forEach(b=>{const keys={left:'KeyA',right:'KeyD',jump:'Space',dash:'ShiftLeft'},key=keys[b.dataset.touch];b.onpointerdown=e=>{e.preventDefault();b.setPointerCapture(e.pointerId);game.keys.add(key)};const release=()=>game.keys.delete(key);b.onpointerup=release;b.onpointercancel=release;b.onlostpointercapture=release});

function showHelp(){const wasPlaying=playing&&game.status==='playing';if(wasPlaying)pauseGame(true);showModal(`<div class="modal-eyebrow">НЕБОЛЬШОЙ ПУТЕВОДИТЕЛЬ</div><h2>Как сохранить огонёк</h2><p>Дойди до большой свечи, пока не иссякло тепло. Маленькие свечи сохраняют позицию и восстанавливают огонь. После затухания ты вернёшься к последней из них.</p><h3>Твой огонёк</h3><div class="help-keys"><kbd>A</kbd><kbd>D</kbd><span>бежать</span><kbd>W</kbd><kbd>ПРОБЕЛ</kbd><span>двойной прыжок</span></div><div class="help-keys"><kbd>SHIFT</kbd><span>рывок</span><kbd>R</kbd><span>заново</span><kbd>ESC</kbd><span>пауза</span></div><h3>Друг на одном ПК</h3><div class="help-keys"><kbd>←</kbd><kbd>→</kbd><span>бежать</span><kbd>↑</kbd><span>прыжок ×2</span><kbd>R CTRL</kbd><span>рывок</span></div><div class="help-row">${icon('star')}<span><b>Три звезды:</b> дойти до свечи, уложиться в целевое время, собрать все искры без единого затухания. Главы 2 и 3 требуют 18 и 40 звёзд в своей кампании.</span></div><div class="help-row">${icon('leaf')}<span><b>Смотри под ноги:</b> мох — безопасный камень; золотые трещины — осыпающиеся плиты; голубой лёд скользит; зелёные грибы подбрасывают. Платформы с золотой меткой движутся.</span></div><div class="help-row">${icon('flame')}<span><b>Береги тепло:</b> вода и шипы гасят огонь, ветер сносит, дождь быстро забирает тепло. Золотые искры дают топливо, свечи восстанавливают его полностью.</span></div><div class="help-row">${icon('link')}<span><b>Вдвоём:</b> встаньте на две печати одновременно, чтобы открыть врата. Они останутся открыты. Чем длиннее нить между вами, тем быстрее гаснет свет. Слишком большое расстояние рвёт нить.</span></div><div class="help-row">${icon('clock')}<span><b>После 30 совместных уровней</b> откроются забеги на одинаковых маршрутах с отдельными финишными временами. После обеих историй лес откроет свой секрет.</span></div><div class="modal-actions"><button class="button primary" id="help-done">Всё понятно ${icon('check')}</button></div>`,'help',()=>{if(wasPlaying)pauseGame(false)});$('#help-done').onclick=()=>closeModal()}
$('#help-btn').onclick=showHelp;

function showCoop(){if(playing&&game.status==='playing')pauseGame(true);if(session?.connected){showConnected();return}showModal(`<div class="modal-eyebrow">С ДРУГОМ ТЕПЛЕЕ</div><h2>Два огонька. Один путь.</h2><p>30 уровней с общими загадками. Ваш свет связан: держитесь рядом, чтобы не погаснуть.</p><button class="mode-choice" id="local-coop">${icon('users')}<span><strong>За одной клавиатурой</strong><small>Первый: A / D / пробел / Shift<br>Второй: стрелки / ↑ / правый Ctrl</small></span>${icon('arrow','choice-arrow')}</button><button class="mode-choice" id="create-lobby">${icon('link')}<span><strong>Создать лобби</strong><small>Пригласи друга кодом из шести символов</small></span>${icon('arrow','choice-arrow')}</button><button class="mode-choice" id="join-lobby">${icon('arrow')}<span><strong>У меня есть код</strong><small>Подключиться к огоньку друга через интернет</small></span>${icon('arrow','choice-arrow')}</button>`,'coop');$('#local-coop').onclick=()=>{closeOnline();if(mode==='solo')mode='coop';selected=nextLevel();chapter=Math.floor((selected-1)/10);updateUI();beginLevel()};$('#create-lobby').onclick=createLobby;$('#join-lobby').onclick=joinLobbyForm}

let lastRemoteInputAt=0;
function makeSession(){
  closeOnline();
  if(playing){playing=false;showDemo()}
  const s=new OnlineSession({
    onStatus:message=>{if(session!==s)return;const el=$('#network-status');if(el){el.textContent=message;el.classList.remove('error')}},
    onConnection:()=>{if(session===s){startNetworkLoop();updateUI();showConnected()}},
    onData:packet=>{if(session===s)receivePacket(packet)},
    onDisconnect:reason=>{
      if(session!==s)return;
      closeOnline();playing=false;game.pause(true);updateUI();showDemo();
      showModal(`<div class="modal-eyebrow">НИТЬ СВЕТА ПРЕРВАЛАСЬ</div><h2>Огонёк друга далеко.</h2><p>${esc(reason||'Соединение потеряно. Проверьте интернет и создайте лобби заново.')}</p><div class="modal-actions"><button class="button primary" id="reconnect-btn">Создать новое лобби</button><button class="button secondary" id="network-map">К уровням</button></div>`,'disconnected');
      $('#reconnect-btn').onclick=createLobby;
      $('#network-map').onclick=()=>{closeModal(false);updateUI();showDemo()};
    },
  });
  session=s;return s;
}
function closeOnline(){
  clearInterval(networkTimer);networkTimer=null;lastRemoteInputAt=0;
  const old=session;session=null;runId='';game.setRemoteInput({});
  if(old){if(old.destroy)old.destroy();else old.close()}
}
async function createLobby(){
  if(session?.connected){showConnected();return}
  if(mode==='solo')mode='coop';
  const s=makeSession();updateUI();
  showModal(`<div class="modal-eyebrow">ОНЛАЙН · СОЗДАТЕЛЬ ЛОББИ</div><h2>Позови второй огонёк.</h2><p>Друг открывает игру, выбирает «У меня есть код» и вводит эти шесть символов.</p><div class="lobby-code" id="lobby-code">······</div><div class="network-status" id="network-status">Создаём лобби…</div><div class="modal-actions"><button class="button secondary" id="copy-code" disabled>Копировать код</button><button class="button secondary" id="cancel-lobby">Отмена</button></div><p class="network-note">Для игры на разных ПК нужен интернет. Создатель оставляет эту вкладку открытой. Если сеть блокирует прямое соединение, попробуйте другую сеть.</p>`,'lobby',()=>{if(!s.connected&&session===s){closeOnline();updateUI();showDemo()}});
  $('#cancel-lobby').onclick=()=>{closeOnline();closeModal(false);updateUI();showDemo()};
  $('#copy-code').onclick=async()=>{if(session!==s||!s.code)return;try{await navigator.clipboard.writeText(s.code);toast('Код скопирован. Отправь его другу.')}catch{toast(`Код лобби: ${s.code}`)}};
  try{
    const {code}=await s.create();
    if(session!==s||s.connected||dialogKind!=='lobby')return;
    const codeElement=$('#lobby-code'),copyButton=$('#copy-code');
    if(codeElement&&copyButton){codeElement.textContent=code;copyButton.disabled=false}
  }catch(error){if(error.name!=='AbortError'&&session===s)showNetworkError(error)}
}
function joinLobbyForm(){
  if(session?.connected){showConnected();return}
  closeOnline();if(mode==='solo')mode='coop';updateUI();
  showModal(`<div class="modal-eyebrow">ОНЛАЙН · ВТОРОЙ ОГОНЁК</div><h2>Найди свет друга.</h2><p>Попроси друга создать лобби и прислать код.</p><form id="join-form"><label class="hidden" for="lobby-input">Код лобби</label><input class="lobby-input" id="lobby-input" maxlength="8" placeholder="ABC234" autocomplete="off" spellcheck="false" aria-label="Код лобби"><div class="network-status hidden" id="network-status"></div><div class="modal-actions"><button type="submit" class="button primary" id="join-submit">Найти лобби ${icon('arrow')}</button><button type="button" class="button secondary" id="join-back">Назад</button></div></form><p class="network-note">Код состоит из шести букв и цифр. В онлайн-игре управляй своим огоньком через A / D / пробел / Shift.</p>`,'join',()=>{if(session&&!session.connected){closeOnline();updateUI();showDemo()}});
  $('#join-back').onclick=()=>{closeOnline();showCoop()};$('#lobby-input').focus();
  $('#join-form').onsubmit=async e=>{
    e.preventDefault();const code=$('#lobby-input').value.trim().toUpperCase().replace(/[\s-]/g,'');
    if(!/^[A-Z2-9]{6}$/.test(code)){toast('Нужен код из шести букв и цифр.');return}
    const s=makeSession();$('#network-status').classList.remove('hidden');$('#join-submit').disabled=true;
    try{await s.join(code)}catch(error){if(error.name==='AbortError'||session!==s)return;showNetworkError(error);if($('#join-submit'))$('#join-submit').disabled=false}
  };
}
function showNetworkError(e){const el=$('#network-status');if(el){el.textContent=e.message||String(e);el.classList.remove('hidden');el.classList.add('error')}else toast(e.message||'Ошибка соединения')}
function showConnected(){const s=session;if(!s?.connected)return;const host=s.role==='host';showModal(`<div class="modal-eyebrow">ДВА ОГОНЬКА НАШЛИ ДРУГ ДРУГА</div><h2>Теперь вы вместе.</h2><div class="online-strip">${icon('link')} Лобби ${esc(s.code)} · два игрока подключены</div><p>${host?'Выбери путь и зажгите первую свечу. Ты управляешь янтарным огоньком, друг — голубым.':'Создатель лобби запускает уровень. Твой голубой огонёк управляется через A / D / пробел / Shift.'}</p>${host?`<div class="modal-actions"><button class="button primary" id="online-story">${icon('users')} ${mode==='race'?'Начать забег':'Начать историю'}</button>${raceUnlocked(progress)&&mode!=='race'?'<button class="button secondary" id="online-race">На время</button>':''}</div>`:'<div class="network-status">Ждём начала путешествия…</div>'}<button class="button text-button" id="leave-connected">Покинуть лобби</button>`,'connected');if(host){$('#online-story').onclick=()=>{if(mode==='solo')mode='coop';selected=nextLevel();chapter=Math.floor((selected-1)/10);updateUI();beginLevel()};if($('#online-race'))$('#online-race').onclick=()=>{mode='race';selected=1;chapter=0;updateUI();beginLevel()}}$('#leave-connected').onclick=()=>{closeOnline();closeModal(false);playing=false;updateUI();showDemo()}}
function startNetworkLoop(){
  clearInterval(networkTimer);lastRemoteInputAt=Date.now();
  networkTimer=setInterval(()=>{
    if(!session?.connected||!playing||!runId)return;
    if(session.role==='host'){
      if(Date.now()-lastRemoteInputAt>500)game.setRemoteInput({});
      session.send({type:'state',id:runId,state:game.getState()});
    }else session.send({type:'input',id:runId,input:game.getInput()});
  },50);
}
function validRemoteState(state){
  if(!state||typeof state!=='object'||state.mode!==mode||state.levelId!==selected)return false;
  if(!['playing','complete'].includes(state.status)||typeof state.paused!=='boolean')return false;
  if(!Number.isFinite(state.time)||state.time<0||state.time>86400||!Number.isInteger(state.deaths)||state.deaths<0||state.deaths>100000)return false;
  if(!Number.isFinite(state.camera)||Math.abs(state.camera)>100000||!Array.isArray(state.players)||state.players.length!==2)return false;
  for(const player of state.players){
    if(!player||typeof player!=='object')return false;
    if(!['x','y','vx','vy','fuel','dash','face','deadTimer'].every(k=>Number.isFinite(player[k])&&Math.abs(player[k])<100000))return false;
    if(player.fuel<0||player.fuel>100||![-1,1].includes(player.face)||typeof player.finished!=='boolean')return false;
    if(player.finishTime!==null&&(!Number.isFinite(player.finishTime)||player.finishTime<0||player.finishTime>state.time+.1))return false;
  }
  const level=currentLevel();
  for(const [key,items] of [['taken',level.embers],['lit',level.candles]]){
    if(!Array.isArray(state[key])||state[key].length>(items||[]).length||!state[key].every(i=>Number.isInteger(i)&&i>=0&&i<(items||[]).length)||new Set(state[key]).size!==state[key].length)return false;
  }
  for(const [key,items] of [['openedGates',(level.gates||[]).map((g,i)=>g.id||i)],['switchActive',(level.switches||[]).map(s=>s.id)]]){
    if(!Array.isArray(state[key])||state[key].length>items.length||!state[key].every(id=>items.includes(id))||new Set(state[key]).size!==state[key].length)return false;
  }
  if(!Array.isArray(state.platforms)||state.platforms.length!==game.platforms.length)return false;
  return state.platforms.every(p=>p&&['x','y','drop','broken'].every(k=>Number.isFinite(p[k])&&Math.abs(p[k])<100000));
}
function receivePacket(p){
  if(!session?.connected||!p||typeof p!=='object'||Array.isArray(p)||typeof p.type!=='string')return;
  const host=session.role==='host',validRun=typeof p.id==='string'&&p.id.length>0&&p.id.length<=120;
  if(p.type==='start'){
    if(!host&&validRun&&p.id!==runId&&['coop','race'].includes(p.mode)&&Number.isInteger(p.levelId)&&p.levelId>=1&&p.levelId<=30)beginLevel({remote:true,remoteMode:p.mode,levelId:p.levelId,id:p.id});
    return;
  }
  if(!validRun||p.id!==runId||!playing)return;
  if(p.type==='state'&&!host){
    if(validRemoteState(p.state)){
      if(mode==='coop'){game.linkDistance=Math.hypot(p.state.players[0].x-p.state.players[1].x,p.state.players[0].y-p.state.players[1].y)}
      game.applyState(p.state);
    }
    return;
  }
  if(p.type==='input'&&host){
    if(!p.input||typeof p.input!=='object'||Array.isArray(p.input))return;
    const input={};for(const key of ['left','right','jump','dash','interact']){if(typeof p.input[key]!=='boolean')return;input[key]=p.input[key]}
    lastRemoteInputAt=Date.now();game.setRemoteInput(input);return;
  }
  if(p.type==='map'){returnToMap(false);return}
  if(p.type==='restart-request'&&host){beginLevel();return}
  if(game.status!=='playing'||typeof p.paused!=='boolean')return;
  if((p.type==='pause-request'&&host)||(p.type==='pause'&&!host)){
    game.pause(p.paused);if(host)session.send({type:'pause',id:runId,paused:p.paused});
    if(p.paused){if(!['pause','help','connected','coop','egg'].includes(dialogKind))showPause()}
    else if(['pause','help'].includes(dialogKind))closeModal(false);
  }
}

function showEasterEgg(){const e=EASTER_EGG;showModal(`<div class="modal-eyebrow">ТЫ ОСВЕТИЛ КАЖДЫЙ УГОЛОК</div><h2>${esc(e.title)}</h2><div class="pixel-egg"><svg viewBox="0 0 400 110" class="egg-sky" aria-label="Созвездие двух огоньков"><path d="M65 66 120 29 180 53 229 20 283 46 340 28" fill="none" stroke="#7f8e75" stroke-width="1" stroke-dasharray="3 5"/><g fill="#f4d490"><path d="M60 49h6V37h5v15h5v18H58V56h2zm54-23h12v8h-12zm60 22h12v9h-12zm50-32h10v9h-10z"/><rect x="43" y="18" width="3" height="3"/><rect x="167" y="16" width="3" height="3"/><rect x="207" y="87" width="3" height="3"/><rect x="352" y="73" width="3" height="3"/></g><g fill="#8dd8e1"><path d="M335 11h5V2h5v12h4v19h-16V18h2z"/><rect x="278" y="42" width="10" height="9"/></g></svg></div><p>${esc(e.story)} ${esc(e.message)}</p><p>${esc(e.reward)}</p><div class="modal-actions"><button class="button primary" id="egg-close">Пусть светит ${icon('flame')}</button></div>`,'egg');$('#egg-close').onclick=()=>closeModal()}
$('.footer-brand').onclick=e=>{e.preventDefault();if(eggUnlocked(progress))showEasterEgg();else toast('В лесу есть один секрет. Зажги все 60 свечей — и он откроется.')};
window.addEventListener('pagehide',()=>{persist();closeOnline()});
window.addEventListener('blur',()=>{if(playing&&!session?.connected&&game.status==='playing'&&!game.paused)pauseGame(true)});
updateUI();selected=nextLevel();chapter=Math.floor((selected-1)/10);updateUI();showDemo();
// Read-only state helps diagnose a saved journey without exposing lobby internals.
window.emberbound={version:'1.0.0',get status(){return {mode,selected,playing,online:!!session?.connected,stars:totalStars(progress,mode==='race'?'coop':mode)}}};
