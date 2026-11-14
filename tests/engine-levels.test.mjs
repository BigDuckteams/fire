import assert from 'node:assert/strict';
import { GameEngine, PHYSICS } from '../dist/engine.js';
import { SOLO_LEVELS, COOP_LEVELS } from '../dist/levels.js';

// Use the production simulation; only the browser/render/audio shell is stubbed.
function headless(level, mode = 'solo') {
  const game = Object.create(GameEngine.prototype);
  Object.assign(game, {
    keys: new Set(), cb: {}, taken: new Set(), lit: new Set(), openedGates: new Set(),
    switchActive: new Set(), particles: [], players: [], quality: 'low', ambientTime: 0,
    render() {}, _ensureLoop() {}, _burst() {}, _sound() {},
  });
  game.load(level, { mode });
  game.start();
  game.inputs = [{}, {}];
  game._input = i => game.inputs[i];
  return game;
}

function place(game, index, x, y) {
  Object.assign(game.players[index], {
    x, y, vx: 0, vy: 0, grounded: true, jumps: 0, deadTimer: 0,
    fuel: 100, dash: 0, dashReady: true, coyote: .12, jumpBuffer: 0,
    prevInput: {},
  });
}

function testJump(original, fromIndex, timeOffset = 0) {
  for (const secondAt of [.20, .29, .38, .47]) {
    for (const dashAt of [null, .15, .35, .55]) {
      const game = headless({
        ...original,
        platforms: [original.platforms[fromIndex], original.platforms[fromIndex + 1]],
        embers: [], candles: [], switches: [], gates: [],
        goal: { x: original.width + 100, y: 0 },
      });
      game.time = timeOffset;
      game.step(.00001);
      const [a, b] = game.platforms;
      place(game, 0, a.x + a.w - 23, a.y);
      game.players[0].vx = PHYSICS.speed;
      let previousVy = 0;
      for (let n = 0; n < 240; n++) {
        const t = n / 100, p = game.players[0];
        const targetX = b.x + Math.min(65, b.w / 2);
        const distance = targetX - p.x;
        game.inputs[0] = {
          right: distance > 18, left: distance < -18,
          jump: n !== Math.round(secondAt * 100),
          dash: dashAt !== null && n === Math.round(dashAt * 100) && distance > 60,
        };
        previousVy = p.vy;
        game.step(.01);
        if (p.deadTimer > 0) break;
        const inside = p.x > b.x + 2 && p.x < b.x + b.w - 2;
        if (inside && ((p.grounded && Math.abs(p.y - b.y) < 7) || (b.type === 'bounce' && previousVy >= 0 && p.vy < -600))) return true;
        if (p.y > 640) break;
      }
    }
  }
  return false;
}

let jumps = 0;
for (const [mode, levels] of [['solo', SOLO_LEVELS], ['coop', COOP_LEVELS]]) {
  for (const level of levels) {
    for (let i = 0; i < level.platforms.length - 1; i++) {
      assert.ok(testJump(level, i), `${mode} ${level.id}: actual engine cannot reach platform ${i + 1}`);
      jumps++;
      if ([level.platforms[i].type, level.platforms[i + 1].type].includes('moving')) {
        assert.ok(testJump(level, i, 2.3), `${mode} ${level.id}: moving platform ${i + 1} fails at alternate phase`);
      }
    }
  }
}

// Each collectible can be acquired by the actual collision/pickup code above
// a supporting ledge, including optional high sparks and moving/bounce ledges.
let sparks = 0;
for (const level of [...SOLO_LEVELS, ...COOP_LEVELS]) {
  for (const ember of level.embers) {
    const supportIndex = level.platforms.findIndex(p => ember.x >= p.x && ember.x <= p.x + p.w);
    assert.ok(supportIndex >= 0, `${level.id}: unsupported ember at ${ember.x}`);
    const support = level.platforms[supportIndex];
    assert.ok(ember.x - support.x >= 20 && support.x + support.w - ember.x >= 20, 'spark has horizontal landing room');
    const game = headless({ ...level, platforms: [support], embers: [ember], switches: [], gates: [], candles: [], goal: { x: level.width + 100, y: 0 } });
    game.step(.00001);
    place(game, 0, ember.x, game.platforms[0].y);
    for (let n = 0; n < 200 && !game.taken.has(0); n++) {
      game.inputs[0] = { jump: n !== 29 };
      game.step(.01);
    }
    assert.ok(game.taken.has(0), `${level.id}: actual engine cannot collect ember ${ember.x}/${ember.y}`);
    assert.equal(game.deaths, 0, `${level.id}: collecting ember requires a death`);
    sparks++;
  }
}

// Paired switches must require two flames and latch every seal permanently.
let seals = 0;
for (const level of COOP_LEVELS) {
  for (const gate of level.gates) {
    const game = headless(level, 'coop');
    const [a, b] = gate.requires.map(id => level.switches.find(s => s.id === id));
    place(game, 0, a.x, a.y);
    place(game, 1, a.x + 2, a.y);
    game.step(.01);
    assert.ok(!game.openedGates.has(gate.id), 'both flames on one seal cannot open gate');
    place(game, 1, b.x, b.y);
    game.step(.01);
    assert.ok(game.openedGates.has(gate.id), `${level.id}: simultaneous pair failed`);
    place(game, 0, gate.x + gate.w + 20, a.y);
    place(game, 1, gate.x + gate.w + 40, a.y);
    game.step(.01);
    assert.ok(game.openedGates.has(gate.id), 'gate closes after stepping off seals');
    seals++;
  }
}

const first = SOLO_LEVELS[0];

// Complete the tutorial continuously with production acceleration, jumps,
// pickups and checkpoint rules. Failed control searches restore a snapshot;
// the retained run never teleports or modifies player physics.
const tutorial = headless(first);
for (let i = 0; i < first.platforms.length - 1; i++) {
  const source = tutorial.platforms[i], target = tutorial.platforms[i + 1];
  for (let n = 0; n < 240 && tutorial.players[0].x < source.x + source.w - 24; n++) {
    tutorial.inputs[0] = { right: true };
    tutorial.step(.01);
  }
  const saved = structuredClone(tutorial.getState());
  let success = false;
  for (const secondAt of [.20, .29, .38, .47]) {
    if (success) break;
    for (const dashAt of [null, .15, .35, .55]) {
      tutorial.applyState(structuredClone(saved));
      for (let n = 0; n < 240; n++) {
        const p = tutorial.players[0], distance = target.x + target.w / 2 - p.x;
        tutorial.inputs[0] = { right: distance > 18, left: distance < -18, jump: n !== Math.round(secondAt * 100), dash: dashAt !== null && n === Math.round(dashAt * 100) && distance > 60 };
        tutorial.step(.01);
        if (p.deadTimer) break;
        if (tutorial.status === 'complete' || (p.grounded && p.x > target.x + 3 && p.x < target.x + target.w - 3 && Math.abs(p.y - target.y) < 4)) {
          success = true;
          break;
        }
      }
      if (success) break;
    }
  }
  assert.ok(success, `tutorial continuous run failed at platform ${i + 1}`);
}
for (let n = 0; n < 100 && tutorial.status !== 'complete'; n++) {
  tutorial.inputs[0] = { right: true, jump: n === 0 || n > 28 };
  tutorial.step(.01);
}
assert.equal(tutorial.status, 'complete', 'tutorial can actually be finished');
assert.equal(tutorial.deaths, 0, 'tutorial has a continuous no-death route');
assert.ok(tutorial.time < first.timeLimit, 'tutorial can be completed before time expires');
assert.ok(tutorial.players[0].fuel > 0, 'tutorial reaches the candle while still alight');
assert.ok(tutorial.time <= first.parTime, 'tutorial speed star is achievable');
assert.equal(tutorial.taken.size, first.embers.length, 'tutorial has a continuous three-star route');

const checkpointGame = headless(first);
const candle = first.candles[0];
place(checkpointGame, 0, candle.x, candle.y);
checkpointGame.players[0].fuel = 5;
checkpointGame.step(.01);
assert.equal(checkpointGame.players[0].fuel, 100, 'candle replenishes the timer');
assert.equal(checkpointGame.players[0].checkpointIndex, 0);
place(checkpointGame, 0, 900, 645);
checkpointGame.step(.01);
assert.equal(checkpointGame.deaths, 1);
assert.ok(Math.abs(checkpointGame.players[0].x - candle.x) < 30, 'death returns to lit candle');
assert.ok(checkpointGame.players[0].y < candle.y + 1);

const fuelGame = headless(first);
for (let n = 0; n < 2000; n++) fuelGame.step(.01);
assert.equal(fuelGame.deaths, 0, 'first level gives at least20 seconds before fuel exhaustion');
assert.ok(fuelGame.players[0].fuel > 65, 'first level affords a generous learning window');

const linkGame = headless(COOP_LEVELS[0], 'coop');
place(linkGame, 0, 90, 540); place(linkGame, 1, 160, 540);
linkGame.step(.01);
const normalDrain = 100 - linkGame.players[0].fuel;
place(linkGame, 0, 90, 540); place(linkGame, 1, 550, 485);
linkGame.step(.01);
assert.ok(100 - linkGame.players[0].fuel > normalDrain + .04, 'distance weakens both flames');
place(linkGame, 0, 90, 540); place(linkGame, 1, 900, 540);
linkGame.step(.01);
assert.ok(linkGame.deaths > 0, 'breaking the link extinguishes flames');

console.log(`${jumps} actual-engine jumps, ${sparks} collectible paths and ${seals} paired seals passed; tutorial completed in ${tutorial.time.toFixed(2)}s with ${tutorial.taken.size}/${first.embers.length} sparks; checkpoints, fuel and link verified.`);
