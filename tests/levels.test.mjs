import assert from 'node:assert/strict';
import { SOLO_LEVELS, COOP_LEVELS, CHAPTERS, getLevel } from '../dist/levels.js';

// Numerically integrate the engine's jump parameters, including the second
// jump. This checks physical route continuity, not just positive coordinates.
function maxJumpDistance(rise) {
  let best = 0;
  for (const secondAt of [0.16, 0.22, 0.28, 0.34, 0.40, 0.46, 0.52]) {
    let y = 0, vy = -515, secondUsed = false;
    for (let t = 0; t < 1.8; t += 1 / 240) {
      if (!secondUsed && t >= secondAt) { vy = -515; secondUsed = true; }
      const before = y;
      vy += 1450 / 240;
      y += vy / 240;
      if (secondUsed && vy > 0 && before <= -rise && y >= -rise) {
        best = Math.max(best, t * 255);
        break;
      }
    }
  }
  return best;
}

for (const [mode, levels] of [['solo', SOLO_LEVELS], ['coop', COOP_LEVELS]]) {
  assert.equal(levels.length, 30);
  assert.equal(new Set(levels.map(l => l.name)).size, 30);
  assert.equal(new Set(levels.map(l => JSON.stringify(l.platforms))).size, 30);
  for (const [i, level] of levels.entries()) {
    assert.equal(level.id, i + 1);
    assert.equal(level.chapter, Math.floor(i / 10));
    assert.ok(level.timeLimit > level.parTime);
    assert.equal(getLevel(mode, level.id), level);
    for (let n = 1; n < level.platforms.length; n++) {
      const a = level.platforms[n - 1], b = level.platforms[n];
      const extraGap = (a.axis === 'x' ? a.range : 0) + (b.axis === 'x' ? b.range : 0);
      const gap = b.x - (a.x + a.w) + extraGap;
      const extraRise = (a.axis === 'y' ? a.range : 0) + (b.axis === 'y' ? b.range : 0);
      const rise = a.y - b.y + extraRise;
      assert.ok(gap + 24 <= maxJumpDistance(rise), `${mode} ${level.id}: unreachable ledge ${n}`);
    }
    for (const candle of [...level.candles, level.goal]) {
      const support = level.platforms.find(p => p.type === 'stone' && candle.x >= p.x + 12 && candle.x <= p.x + p.w - 12 && candle.y === p.y);
      assert.ok(support, `${mode} ${level.id}: candle lacks stable support`);
    }
    for (const gate of level.gates) {
      assert.equal(gate.y, 0, 'puzzle cannot be skipped by jumping overhead');
      assert.equal(gate.requires.length, 2);
      const switches = gate.requires.map(id => level.switches.find(s => s.id === id));
      assert.ok(switches.every(Boolean));
      assert.ok(Math.abs(switches[0].x - switches[1].x) > 80, 'one flame cannot press both switches');
      assert.ok(Math.abs(switches[0].x - switches[1].x) < 280, 'switches preserve a healthy light link');
      assert.ok(switches.every(s => s.x < gate.x));
    }
    if (mode === 'coop') assert.ok(level.gates.length >= 1, 'co-op level needs a genuine joint puzzle');
  }
}
assert.deepEqual(CHAPTERS.map(c => c.requiredStars), [0, 18, 40]);
assert.equal(getLevel('solo', 31), null);
console.log('60 unique levels: schema, double-jump reachability, stable checkpoints and co-op seals verified.');
