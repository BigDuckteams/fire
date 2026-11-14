import { test } from 'node:test';
import assert from 'node:assert/strict';
import { PROGRESS_KEY, loadProgress, saveProgress, newProgress, recordResult, totalStars, completedCount, canPlay, raceUnlocked, eggUnlocked } from '../dist/progress.js';

const memory = () => {
  const data = new Map();
  return { getItem: key => data.get(key) ?? null, setItem: (key, value) => data.set(key, value) };
};
const result = (stars = 2, time = 25) => ({ stars, time, embers: 3, deaths: 0 });

test('fresh saves are independent, safe without storage and use requested defaults', () => {
  const first = newProgress();
  first.solo[1] = result();
  assert.deepEqual(newProgress(), { version: 1, solo: {}, coop: {}, race: {}, settings: { muted: true, quality: 'high' } });
  assert.equal(canPlay(newProgress(), 'solo', 1), true);
  assert.equal(canPlay(newProgress(), 'solo', 2), false);
  assert.equal(canPlay(newProgress(), 'coop', 1), true);
  assert.equal(saveProgress(newProgress(), undefined), false);
});

test('recordResult mutates the save and preserves independently earned best stars/time', () => {
  const progress = newProgress();
  assert.equal(recordResult(progress, 'solo', 1, result(3, 50)), progress);
  recordResult(progress, 'solo', 1, { stars: 1, time: 20, embers: 1, deaths: 2 });
  assert.deepEqual(progress.solo[1], { stars: 3, time: 20, embers: 3, deaths: 0 });
  assert.equal(totalStars(progress, 'solo'), 3);
  assert.equal(completedCount(progress, 'solo'), 1);
  assert.equal(canPlay(progress, 'solo', 2), true);
  recordResult(progress, 'solo', 31, result());
  recordResult(progress, 'invalid', 1, result());
  assert.equal(Object.keys(progress.solo).length, 1);
});

test('campaign chapter gates require both previous completion and 18/40 stars', () => {
  const progress = newProgress();
  for (let id = 1; id <= 10; id++) recordResult(progress, 'solo', id, result(1));
  assert.equal(canPlay(progress, 'solo', 11), false);
  for (let id = 1; id <= 4; id++) recordResult(progress, 'solo', id, result(3));
  assert.equal(totalStars(progress, 'solo'), 18);
  assert.equal(canPlay(progress, 'solo', 11), true);
  assert.equal(canPlay(progress, 'coop', 11), false);
  for (let id = 11; id <= 20; id++) recordResult(progress, 'solo', id, result(2));
  assert.equal(canPlay(progress, 'solo', 21), false);
  recordResult(progress, 'solo', 5, result(3));
  assert.equal(totalStars(progress, 'solo'), 40);
  assert.equal(canPlay(progress, 'solo', 21), true);
  delete progress.solo[20];
  assert.equal(canPlay(progress, 'solo', 21), false);
});

test('completing coop unlocks all race routes; both campaigns unlock persistent egg', () => {
  const progress = newProgress();
  assert.equal(raceUnlocked(progress), false);
  assert.equal(eggUnlocked(progress), false);
  for (let id = 1; id <= 30; id++) recordResult(progress, 'coop', id, result());
  assert.equal(raceUnlocked(progress), true);
  assert.equal(canPlay(progress, 'race', 30), true);
  assert.equal(eggUnlocked(progress), false);
  for (let id = 1; id <= 30; id++) recordResult(progress, 'solo', id, result());
  const storage = memory();
  assert.equal(saveProgress(progress, storage), true);
  assert.equal(eggUnlocked(loadProgress(storage)), true);
  assert.equal(completedCount(loadProgress(storage), 'solo'), 30);
});

test('race records preserve player times belonging to fastest combined result', () => {
  const progress = newProgress();
  recordResult(progress, 'race', 1, { time: 50, raceTimes: [40, 50] });
  recordResult(progress, 'race', 1, { time: 60, raceTimes: [30, 60] });
  assert.deepEqual(progress.race[1], { time: 50, raceTimes: [40, 50] });
  recordResult(progress, 'race', 1, { time: 45, raceTimes: [41, 45] });
  assert.deepEqual(progress.race[1], { time: 45, raceTimes: [41, 45] });
});

test('corrupt values are clamped and unsupported keys/versions never enter the save', () => {
  const storage = memory();
  storage.setItem(PROGRESS_KEY, JSON.stringify({
    version: 1,
    solo: { 1: { stars: 99, time: -5, embers: -10, deaths: 9.8 }, 2: { stars: '3' }, 31: result(), bogus: result() },
    coop: [], race: { 1: { time: 30, raceTimes: [25, -4, 55] }, 2: { time: -4 } },
    settings: { muted: false, quality: 'low', arbitrary: true },
  }));
  const progress = loadProgress(storage);
  assert.deepEqual(progress.solo, { 1: { stars: 3, time: null, embers: 0, deaths: 9 } });
  assert.deepEqual(progress.race, { 1: { time: 30, raceTimes: [25, null] } });
  assert.deepEqual(progress.settings, { muted: false, quality: 'low' });
  storage.setItem(PROGRESS_KEY, '{broken');
  assert.deepEqual(loadProgress(storage), newProgress());
  storage.setItem(PROGRESS_KEY, JSON.stringify({ version: 200, solo: { 1: result() } }));
  assert.deepEqual(loadProgress(storage), newProgress());
});

test('blocked or full storage fails softly without losing the in-memory save', () => {
  const storage = { getItem() { throw new Error('blocked'); }, setItem() { throw new Error('quota'); } };
  assert.deepEqual(loadProgress(storage), newProgress());
  const progress = newProgress();
  recordResult(progress, 'solo', 1, result());
  assert.equal(saveProgress(progress, storage), false);
  assert.equal(progress.solo[1].stars, 2);
  for (const invalid of [0, -1, 31, NaN, Infinity, null, 'abc', 1.5]) assert.equal(canPlay(progress, 'solo', invalid), false);
});
