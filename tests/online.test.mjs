import { test } from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { OnlineSession, normalizeLobbyCode } from '../dist/online.js';

const peers = new Map();
let serial = 0;
class MockConnection extends EventEmitter {
  constructor(metadata) {
    super();
    this.metadata = metadata;
    this.open = false;
    this.bufferSize = 0;
    this.dataChannel = { bufferedAmount: 0 };
  }
  send(packet) {
    if (!this.open) throw new Error('closed');
    queueMicrotask(() => { if (this.other?.open) this.other.emit('data', structuredClone(packet)); });
  }
  close() {
    if (this.closed) return;
    this.closed = true;
    this.open = false;
    this.emit('close');
    this.other?.close();
  }
}
class MockPeer extends EventEmitter {
  constructor(id) {
    super();
    this.id = id || `guest-${++serial}`;
    this.destroyed = false;
    this.channels = [];
    if (peers.has(this.id)) {
      queueMicrotask(() => this.emit('error', { type: 'unavailable-id' }));
    } else {
      peers.set(this.id, this);
      queueMicrotask(() => { if (!this.destroyed) this.emit('open', this.id); });
    }
  }
  connect(id, options) {
    const local = new MockConnection(options.metadata);
    this.channels.push(local);
    queueMicrotask(() => {
      const host = peers.get(id);
      if (!host) { this.emit('error', { type: 'peer-unavailable' }); return; }
      const remote = new MockConnection(options.metadata);
      local.other = remote;
      remote.other = local;
      host.channels.push(remote);
      host.emit('connection', remote);
      local.open = true;
      remote.open = true;
      local.emit('open');
      remote.emit('open');
    });
    return local;
  }
  destroy() {
    this.destroyed = true;
    if (peers.get(this.id) === this) peers.delete(this.id);
    for (const channel of this.channels) channel.close();
  }
  reconnect() {}
}
globalThis.Peer = MockPeer;
const tick = () => new Promise(resolve => setImmediate(resolve));

test('normalizes a human-readable code and rejects malformed codes', async () => {
  assert.equal(normalizeLobbyCode(' ab3-d9x '), 'AB3D9X');
  const session = new OnlineSession();
  await assert.rejects(session.join('<bad>'), /6 букв/);
  session.destroy();
});

test('host/guest handshake, bidirectional data, disconnect and cleanup', async () => {
  const receivedByHost = [];
  const receivedByGuest = [];
  const hostConnections = [];
  const guestConnections = [];
  const disconnected = [];
  const host = new OnlineSession({ onData: p => receivedByHost.push(p), onConnection: p => hostConnections.push(p), onDisconnect: p => disconnected.push(p) });
  const guest = new OnlineSession({ onData: p => receivedByGuest.push(p), onConnection: p => guestConnections.push(p) });
  try {
    const { code } = await host.create();
    assert.match(code, /^[A-Z2-9]{6}$/);
    assert.equal(host.connected, false);
    await guest.join(code.toLowerCase());
    assert.equal(host.connected, true);
    assert.equal(guest.connected, true);
    assert.deepEqual(hostConnections, [{ role: 'host', code }]);
    assert.deepEqual(guestConnections, [{ role: 'guest', code }]);
    assert.equal(host.send({ type: 'start', levelId: 3 }), true);
    assert.equal(guest.send({ type: 'input', left: true }), true);
    await tick();
    assert.deepEqual(receivedByGuest, [{ type: 'start', levelId: 3 }]);
    assert.deepEqual(receivedByHost, [{ type: 'input', left: true }]);
    guest.close();
    assert.equal(host.connected, false);
    assert.equal(disconnected.length, 1);
    assert.equal(host.send({ type: 'state' }), false);
  } finally { guest.destroy(); host.destroy(); }
  assert.equal(peers.size, 0);
});

test('an occupied lobby rejects a third player without activating them', async () => {
  const host = new OnlineSession();
  const guest = new OnlineSession();
  let intruderActivated = false;
  const intruder = new OnlineSession({ onConnection: () => { intruderActivated = true; } });
  try {
    const { code } = await host.create();
    await guest.join(code);
    await assert.rejects(intruder.join(code), /уже два/);
    assert.equal(intruderActivated, false);
    assert.equal(host.connected, true);
    assert.equal(guest.connected, true);
  } finally { intruder.destroy(); guest.destroy(); host.destroy(); }
});

test('missing room reports a readable error and releases peer resources', async () => {
  const session = new OnlineSession();
  await assert.rejects(session.join('AAAAAA'), /Лобби не найдено/);
  assert.equal(session.connected, false);
  assert.equal(session.peer, null);
  session.destroy();
  assert.equal(peers.size, 0);
});

test('network backpressure discards stale frames', async () => {
  const host = new OnlineSession();
  const guest = new OnlineSession();
  try {
    const { code } = await host.create();
    await guest.join(code);
    host.connection.dataChannel.bufferedAmount = 300000;
    assert.equal(host.send({ type: 'state' }), false);
    host.connection.dataChannel.bufferedAmount = 0;
    assert.equal(host.send({ type: 'state' }), true);
  } finally { guest.destroy(); host.destroy(); }
});

test('close cancels an in-flight connection', async () => {
  const session = new OnlineSession();
  const promise = session.create();
  session.close();
  await assert.rejects(promise, error => error.name === 'AbortError');
  assert.equal(session.peer, null);
  session.destroy();
});
