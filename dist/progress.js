export const PROGRESS_KEY = 'emberbound.progress.v1';
const MODES = ['solo', 'coop', 'race'];
const MAX_LEVEL = 30;

function defaultStorage() {
  try { return globalThis.localStorage; } catch { return undefined; }
}

export function newProgress() {
  return { version: 1, solo: {}, coop: {}, race: {}, settings: { muted: true, quality: 'high' } };
}

const object = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const finite = value => typeof value === 'number' && Number.isFinite(value);
const integer = (value, minimum, maximum, fallback = minimum) => finite(value) ? Math.min(maximum, Math.max(minimum, Math.trunc(value))) : fallback;
const validId = id => Number.isInteger(Number(id)) && Number(id) >= 1 && Number(id) <= MAX_LEVEL;
const validTime = value => finite(value) && value >= 0 && value <= 86400;

function sanitizeRecord(record, mode) {
  if (!object(record)) return null;
  if (mode === 'race') {
    if (!validTime(record.time)) return null;
    return {
      time: record.time,
      raceTimes: Array.isArray(record.raceTimes) ? record.raceTimes.slice(0, 2).map(value => validTime(value) ? value : null) : [],
    };
  }
  const stars = integer(record.stars, 0, 3);
  if (stars < 1) return null;
  return {
    stars,
    time: validTime(record.time) ? record.time : null,
    embers: integer(record.embers, 0, 10000),
    deaths: integer(record.deaths, 0, 100000),
  };
}

function sanitizeProgress(raw) {
  const clean = newProgress();
  if (!object(raw) || raw.version !== 1) return clean;
  for (const mode of MODES) {
    if (!object(raw[mode])) continue;
    for (const [key, value] of Object.entries(raw[mode])) {
      if (!/^(?:[1-9]|[12]\d|30)$/.test(key)) continue;
      const record = sanitizeRecord(value, mode);
      if (record) clean[mode][key] = record;
    }
  }
  if (object(raw.settings)) {
    if (typeof raw.settings.muted === 'boolean') clean.settings.muted = raw.settings.muted;
    if (raw.settings.quality === 'low' || raw.settings.quality === 'high') clean.settings.quality = raw.settings.quality;
  }
  return clean;
}

/** Corrupt, blocked or unavailable storage always yields a playable fresh save. */
export function loadProgress(storage = defaultStorage()) {
  try {
    const raw = storage?.getItem(PROGRESS_KEY);
    return raw ? sanitizeProgress(JSON.parse(raw)) : newProgress();
  } catch { return newProgress(); }
}

/** Save only supported fields; false means browser persistence is unavailable. */
export function saveProgress(progress, storage = defaultStorage()) {
  try {
    if (!storage || typeof storage.setItem !== 'function') return false;
    storage.setItem(PROGRESS_KEY, JSON.stringify(sanitizeProgress(progress)));
    return true;
  } catch { return false; }
}

/** Merge a successful result into the same progress object and return it. */
export function recordResult(progress, mode, levelId, result) {
  if (!object(progress) || !MODES.includes(mode) || !validId(levelId)) return progress;
  const next = sanitizeRecord(result, mode);
  if (!next) return progress;
  const key = String(Number(levelId));
  if (!object(progress[mode])) progress[mode] = {};
  const previous = sanitizeRecord(progress[mode][key], mode);
  if (!previous) {
    progress[mode][key] = next;
    return progress;
  }
  if (mode === 'race') {
    if (next.time < previous.time) progress.race[key] = next;
    else progress.race[key] = previous;
  } else {
    progress[mode][key] = {
      stars: Math.max(previous.stars, next.stars),
      time: previous.time === null ? next.time : next.time === null ? previous.time : Math.min(previous.time, next.time),
      embers: Math.max(previous.embers, next.embers),
      deaths: Math.min(previous.deaths, next.deaths),
    };
  }
  return progress;
}

export function totalStars(progress, mode) {
  if (!['solo', 'coop'].includes(mode) || !object(progress?.[mode])) return 0;
  let total = 0;
  for (let id = 1; id <= MAX_LEVEL; id++) total += integer(progress[mode][id]?.stars, 0, 3);
  return total;
}

export function completedCount(progress, mode) {
  if (!MODES.includes(mode) || !object(progress?.[mode])) return 0;
  let total = 0;
  for (let id = 1; id <= MAX_LEVEL; id++) {
    if (mode === 'race' ? validTime(progress[mode][id]?.time) : integer(progress[mode][id]?.stars, 0, 3) >= 1) total++;
  }
  return total;
}

export function raceUnlocked(progress) {
  return completedCount(progress, 'coop') === MAX_LEVEL;
}

export function eggUnlocked(progress) {
  return completedCount(progress, 'solo') === MAX_LEVEL && completedCount(progress, 'coop') === MAX_LEVEL;
}

export function canPlay(progress, mode, id) {
  if (!MODES.includes(mode) || !validId(id)) return false;
  id = Number(id);
  if (mode === 'race') return raceUnlocked(progress);
  if (id === 1) return true;
  const previousFinished = integer(progress?.[mode]?.[id - 1]?.stars, 0, 3) >= 1;
  const requiredStars = id >= 21 ? 40 : id >= 11 ? 18 : 0;
  return previousFinished && totalStars(progress, mode) >= requiredStars;
}
