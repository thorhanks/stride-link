import { WalkingDetector, motionMagnitude } from './motion-detector.js';

const $ = id => document.getElementById(id);
const token = new URLSearchParams(location.search).get('token');
const detector = new WalkingDetector();
const magnitudeState = { gravity: null };
let socket;
let reconnectTimer;
let wakeLock;
let sensorsEnabled = false;
let manualActive = false;
let sentActive = false;
let sampleCount = 0;
let sampleWindowStart = performance.now();

function connect() {
  if (!token) {
    showMessage('This link is missing its private session token. Scan the QR code again.');
    return;
  }
  const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
  socket = new WebSocket(`${protocol}//${location.host}/ws?token=${encodeURIComponent(token)}`);
  socket.addEventListener('open', () => {
    $('connection').textContent = 'Connected';
    $('connection').className = 'pill online';
    sendState(true);
  });
  socket.addEventListener('close', () => {
    $('connection').textContent = 'Offline';
    $('connection').className = 'pill offline';
    reconnectTimer = setTimeout(connect, 1000);
  });
  socket.addEventListener('error', () => socket.close());
}

function send(payload) {
  if (socket?.readyState === WebSocket.OPEN)
    socket.send(JSON.stringify(payload));
}

function sendState(force = false) {
  const active = manualActive || detector.walking;
  if (force || active !== sentActive) {
    sentActive = active;
    send({ type: 'movement', active });
  }
}

function render(snapshot) {
  $('motion').textContent = snapshot.motion.toFixed(2);
  $('cadence').textContent = snapshot.cadence || '—';
  const active = manualActive || snapshot.walking;
  $('hero').className = `hero ${active ? 'walking' : 'idle'}`;
  $('state').textContent = active ? (manualActive ? 'TESTING' : 'WALKING') : (sensorsEnabled ? 'STANDING' : 'READY');
  $('stateHint').textContent = active ? 'Forward movement is active.' : (sensorsEnabled ? 'Walk in place to move forward.' : 'Enable motion, then put the phone in your pocket.');
}

function onMotion(event) {
  const magnitude = motionMagnitude(event, magnitudeState);
  const snapshot = detector.addSample(magnitude, performance.now());
  sampleCount++;
  render(snapshot);
  sendState();
}

async function enableMotion() {
  try {
    if (!window.isSecureContext)
      throw new Error('Motion sensors require the secure HTTPS controller. Return to the setup page and install the certificate.');
    if (!('DeviceMotionEvent' in window))
      throw new Error('This browser does not expose motion sensors. Try Safari on iPhone or Chrome on Android.');
    if (typeof DeviceMotionEvent.requestPermission === 'function') {
      const permission = await DeviceMotionEvent.requestPermission();
      if (permission !== 'granted') throw new Error('Motion permission was not granted.');
    }
    window.addEventListener('devicemotion', onMotion);
    sensorsEnabled = true;
    $('enable').textContent = 'Motion enabled';
    $('enable').disabled = true;
    showMessage('Motion is live. Keep this page open and the screen on.');
    await requestWakeLock();
    render(detector.snapshot());
  } catch (error) {
    showMessage(error.message || 'Could not enable motion sensors.');
  }
}

async function requestWakeLock() {
  try {
    if ('wakeLock' in navigator) wakeLock = await navigator.wakeLock.request('screen');
  } catch { /* The page still works if wake lock is unavailable. */ }
}

function setManual(active) {
  manualActive = active;
  $('manual').classList.toggle('active', active);
  $('manual').textContent = active ? 'W is held — release to stop' : 'Hold to test W';
  render(detector.snapshot());
  sendState(true);
}

function showMessage(text) { $('message').textContent = text; }

$('enable').addEventListener('click', enableMotion);
$('manual').addEventListener('pointerdown', event => { event.preventDefault(); setManual(true); });
for (const eventName of ['pointerup', 'pointercancel', 'pointerleave'])
  $('manual').addEventListener(eventName, () => setManual(false));

$('sensitivity').addEventListener('input', event => {
  detector.setThreshold(event.target.value);
  $('sensitivityValue').textContent = Number(event.target.value).toFixed(2);
});
$('stopDelay').addEventListener('input', event => {
  detector.setStopDelay(event.target.value);
  $('stopDelayValue').textContent = `${event.target.value} ms`;
});

setInterval(() => {
  const snapshot = detector.tick(performance.now());
  render(snapshot);
  sendState();
}, 100);

setInterval(() => send({ type: 'heartbeat', active: manualActive || detector.walking }), 500);

setInterval(() => {
  const now = performance.now();
  const elapsed = (now - sampleWindowStart) / 1000;
  $('sampleRate').textContent = Math.round(sampleCount / elapsed);
  sampleCount = 0;
  sampleWindowStart = now;
}, 1000);

document.addEventListener('visibilitychange', async () => {
  if (document.hidden) {
    manualActive = false;
    detector.reset();
    sendState(true);
  } else if (sensorsEnabled) {
    await requestWakeLock();
  }
});

window.addEventListener('pagehide', () => {
  manualActive = false;
  detector.reset();
  sendState(true);
  socket?.close();
  clearTimeout(reconnectTimer);
});

connect();
render(detector.snapshot());
