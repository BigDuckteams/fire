/* Real two-player WebRTC networking. PeerJS 1.5.5 is vendored in /vendor. */
const ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const PREFIX = 'emberbound-';
const PROTOCOL = 1;
const SIGNAL_TIMEOUT = 18000;
const CONNECT_TIMEOUT = 25000;

function makeCode() {
  const bytes = new Uint8Array(6);
  globalThis.crypto.getRandomValues(bytes);
  return Array.from(bytes, n => ALPHABET[n & 31]).join('');
}

export function normalizeLobbyCode(value) {
  return String(value || '').trim().toUpperCase().replace(/[\s-]/g, '');
}

function failure(error) {
  if (error?.name === 'AbortError') return error;
  const messages = {
    'peer-unavailable': 'Лобби не найдено. Проверьте код и попросите друга оставить лобби открытым.',
    'unavailable-id': 'Этот код уже занят. Создайте новое лобби.',
    'browser-incompatible': 'Этот браузер не поддерживает WebRTC. Откройте игру в актуальном Chrome, Edge или Firefox.',
    'network': 'Не удалось связаться с сервером лобби. Проверьте интернет и повторите попытку.',
    'socket-error': 'Сервер лобби недоступен. Проверьте интернет и повторите попытку.',
    'socket-closed': 'Связь с сервером лобби потеряна. Создайте лобби заново.',
    'server-error': 'Сервер лобби временно недоступен. Попробуйте ещё раз.',
    'webrtc': 'Прямое соединение заблокировано сетью. Попробуйте другую сеть или режим на одном ПК.',
    'timeout': 'Друг не ответил. Проверьте код. Если сеть блокирует WebRTC, попробуйте другую сеть.',
    'lobby-full': 'В этом лобби уже два огонька. Попросите друга создать новое.',
  };
  const result = new Error(messages[error?.type] || error?.message || 'Не удалось установить соединение. Попробуйте ещё раз.');
  result.type = error?.type || 'connection';
  return result;
}

function abortError() {
  const error = new Error('Подключение отменено.');
  error.name = 'AbortError';
  return error;
}

let peerLibrary;
async function loadPeerLibrary() {
  if (globalThis.Peer) return globalThis.Peer;
  if (peerLibrary) return peerLibrary;
  peerLibrary = new Promise((resolve, reject) => {
    if (typeof document === 'undefined') {
      reject(new Error('WebRTC доступен только в браузере.'));
      return;
    }
    const script = document.createElement('script');
    const timer = setTimeout(() => { script.remove(); reject(new Error('Не удалось загрузить сетевой модуль. Обновите страницу.')); }, 12000);
    script.src = new URL('./vendor/peerjs.min.js', import.meta.url).href;
    script.onload = () => {
      clearTimeout(timer);
      if (globalThis.Peer) resolve(globalThis.Peer);
      else reject(new Error('Сетевой модуль не запустился. Обновите страницу.'));
    };
    script.onerror = () => {
      clearTimeout(timer);
      script.remove();
      reject(new Error('Не удалось загрузить сетевой модуль. Обновите страницу.'));
    };
    document.head.appendChild(script);
  }).catch(error => { peerLibrary = null; throw error; });
  return peerLibrary;
}

/**
 * One host, one guest, reliable encrypted WebRTC data channel.
 * The host simulates the game; callers choose input/snapshot packet schemas.
 * create() resolves after the six-character code is registered, join() after
 * the remote data channel opens. Closing cancels any in-flight operation.
 */
export class OnlineSession {
  constructor({ onStatus = () => {}, onConnection = () => {}, onData = () => {}, onDisconnect = () => {} } = {}) {
    this.onStatus = onStatus;
    this.onConnection = onConnection;
    this.onData = onData;
    this.onDisconnect = onDisconnect;
    this.peer = null;
    this.connection = null;
    this.role = null;
    this.code = '';
    this.connected = false;
    this._epoch = 0;
    this._pending = new Set();
    this._heartbeat = null;
    this._lastSeen = 0;
    this._notified = false;
    this._reconnectAttempts = 0;
    this._beforeUnload = () => this.close();
    if (typeof window !== 'undefined') window.addEventListener('pagehide', this._beforeUnload);
  }

  async create() {
    this.close();
    const epoch = this._epoch;
    this.role = 'host';
    this.onStatus('Создаём лобби…');
    try {
      const Peer = await loadPeerLibrary();
      if (epoch !== this._epoch) throw abortError();
      for (let attempt = 0; attempt < 4; attempt++) {
        this.code = makeCode();
        try {
          await this._openPeer(Peer, PREFIX + this.code, epoch);
          if (epoch !== this._epoch) throw abortError();
          this.onStatus('Лобби открыто. Отправьте код другу.');
          return { code: this.code };
        } catch (error) {
          if (epoch !== this._epoch) throw abortError();
          this._destroyPeer();
          if (error.type !== 'unavailable-id' || attempt === 3) throw error;
        }
      }
    } catch (error) {
      if (epoch === this._epoch) this._destroyPeer();
      throw failure(error);
    }
  }

  async join(value) {
    const code = normalizeLobbyCode(value);
    if (!/^[A-Z2-9]{6}$/.test(code)) throw new Error('Введите код лобби из 6 букв и цифр.');
    this.close();
    const epoch = this._epoch;
    this.role = 'guest';
    this.code = code;
    this.onStatus('Ищем огонёк друга…');
    try {
      const Peer = await loadPeerLibrary();
      if (epoch !== this._epoch) throw abortError();
      await this._openPeer(Peer, undefined, epoch);
      if (epoch !== this._epoch) throw abortError();
      this.onStatus('Устанавливаем прямое соединение…');
      const connection = this.peer.connect(PREFIX + code, {
        label: 'emberbound-v1',
        metadata: { app: 'emberbound', protocol: PROTOCOL },
        serialization: 'json',
        reliable: true,
      });
      await this._attachConnection(connection, epoch);
      return { code };
    } catch (error) {
      if (epoch === this._epoch) this._destroyPeer();
      throw failure(error);
    }
  }

  _openPeer(Peer, id, epoch) {
    return new Promise((resolve, reject) => {
      let settled = false;
      let peer;
      const finish = (error) => {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        this._pending.delete(cancel);
        if (error) reject(failure(error));
        else resolve(peer);
      };
      const cancel = () => finish(abortError());
      const timer = setTimeout(() => finish({ type: 'network' }), SIGNAL_TIMEOUT);
      this._pending.add(cancel);
      try {
        peer = new Peer(id, { secure: true, debug: 0, pingInterval: 5000 });
        this.peer = peer;
      } catch (error) {
        finish(error);
        return;
      }
      peer.on('open', () => {
        if (epoch !== this._epoch) { finish(abortError()); return; }
        this._reconnectAttempts = 0;
        finish();
      });
      peer.on('connection', connection => {
        if (epoch !== this._epoch || this.role !== 'host') { connection.close(); return; }
        if (connection.metadata?.app !== 'emberbound' || connection.metadata?.protocol !== PROTOCOL) {
          connection.close();
          return;
        }
        if (this.connection) {
          connection.on('open', () => {
            connection.send({ _emberbound: 'full' });
            setTimeout(() => connection.close(), 150);
          });
          return;
        }
        this._attachConnection(connection, epoch).catch(error => {
          if (epoch === this._epoch) {
            this.connection = null;
            this.onStatus(failure(error).message + ' Лобби остаётся открытым.');
          }
        });
      });
      peer.on('error', error => {
        if (epoch !== this._epoch) return;
        if (!settled) { finish(error); return; }
        // An established WebRTC channel survives a signalling outage.
        if (this.connected && ['network', 'socket-error', 'socket-closed', 'disconnected'].includes(error.type)) return;
        for (const cancelPending of [...this._pending]) cancelPending(error);
        if (!this.connected) this.onStatus(failure(error).message);
        else this._lost(failure(error).message, epoch);
      });
      peer.on('disconnected', () => {
        if (epoch !== this._epoch || peer.destroyed) return;
        if (!this.connected) this.onStatus('Восстанавливаем связь с сервером лобби…');
        if (++this._reconnectAttempts <= 3) {
          try { peer.reconnect(); } catch { /* The next signalling error supplies a readable reason. */ }
        } else if (!this.connected) {
          this.onStatus('Связь с сервером лобби потеряна. Создайте лобби заново.');
        }
      });
    });
  }

  _attachConnection(connection, epoch) {
    this.connection = connection;
    return new Promise((resolve, reject) => {
      let settled = false;
      const finish = (error) => {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        this._pending.delete(cancel);
        if (error) reject(failure(error));
        else resolve();
      };
      const cancel = (error) => finish(error || abortError());
      const timer = setTimeout(() => { finish({ type: 'timeout' }); connection.close(); }, CONNECT_TIMEOUT);
      this._pending.add(cancel);
      const activate = () => {
        if (this.connected || epoch !== this._epoch) return;
        this.connected = true;
        this._notified = false;
        this._lastSeen = Date.now();
        this._startHeartbeat(epoch);
        finish();
        this.onStatus('Два огонька соединены. Можно начинать.');
        this.onConnection({ role: this.role, code: this.code });
      };
      connection.on('open', () => {
        if (epoch !== this._epoch) { finish(abortError()); connection.close(); return; }
        if (this.role === 'host') {
          connection.send({ _emberbound: 'accepted', protocol: PROTOCOL });
          activate();
        }
      });
      connection.on('data', data => {
        if (epoch !== this._epoch || connection !== this.connection) return;
        this._lastSeen = Date.now();
        if (data?._emberbound === 'ping') {
          this.send({ _emberbound: 'pong' });
          return;
        }
        if (data?._emberbound === 'pong') return;
        if (data?._emberbound === 'accepted') {
          if (this.role === 'guest' && data.protocol === PROTOCOL) activate();
          return;
        }
        if (data?._emberbound === 'full') {
          const message = failure({ type: 'lobby-full' }).message;
          finish({ type: 'lobby-full' });
          this._lost(message, epoch);
          return;
        }
        if (data && typeof data === 'object' && !Array.isArray(data)) this.onData(data);
      });
      connection.on('close', () => {
        finish(new Error('Друг отключился от лобби.'));
        if (!this.connected && !this._notified) {
          if (this.connection === connection) this.connection = null;
          return;
        }
        this._lost('Связь с другом потеряна. Создайте лобби и подключитесь снова.', epoch);
      });
      connection.on('error', error => {
        finish(error);
        this._lost(failure(error).message, epoch);
      });
    });
  }

  _startHeartbeat(epoch) {
    clearInterval(this._heartbeat);
    this._heartbeat = setInterval(() => {
      if (epoch !== this._epoch || !this.connected) return;
      if (Date.now() - this._lastSeen > 45000) {
        this._lost('Друг не отвечает. Соединение прервано; можно подключиться снова.', epoch);
        return;
      }
      this.send({ _emberbound: 'ping' });
    }, 5000);
  }

  _lost(reason, epoch) {
    if (epoch !== this._epoch || this._notified) return;
    this._notified = true;
    this.connected = false;
    clearInterval(this._heartbeat);
    this._heartbeat = null;
    const connection = this.connection;
    this.connection = null;
    if (connection) { try { connection.close(); } catch {} }
    this.onStatus(reason);
    this.onDisconnect(reason);
  }

  /** Returns false if disconnected or if network backpressure drops a stale frame. */
  send(data) {
    if (!this.connected || !this.connection?.open) return false;
    if (this.connection.dataChannel?.bufferedAmount > 262144 || this.connection.bufferSize > 32) return false;
    try { this.connection.send(data); return true; }
    catch { return false; }
  }

  _destroyPeer() {
    const connection = this.connection;
    this.connection = null;
    if (connection) { try { connection.close(); } catch {} }
    if (this.peer) { try { this.peer.destroy(); } catch {} }
    this.peer = null;
    this.connected = false;
    clearInterval(this._heartbeat);
    this._heartbeat = null;
  }

  /** Intentional close is silent; callers already know they are leaving. */
  close() {
    this._epoch++;
    for (const cancel of [...this._pending]) cancel();
    this._pending.clear();
    this._destroyPeer();
    this.role = null;
    this.code = '';
    this._notified = false;
    this._reconnectAttempts = 0;
  }

  /** Release the optional browser lifecycle listener when discarding a session. */
  destroy() {
    this.close();
    if (typeof window !== 'undefined') window.removeEventListener('pagehide', this._beforeUnload);
  }
}
