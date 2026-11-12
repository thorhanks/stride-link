import test from 'node:test';
import assert from 'node:assert/strict';
import { WalkingDetector, motionMagnitude } from '../wwwroot/motion-detector.js';

test('detects a plausible walking rhythm and stops after the delay', () => {
  const detector = new WalkingDetector({ threshold: 0.8, stopDelay: 700 });
  let snapshot;
  for (let time = 0; time <= 2400; time += 100) {
    const phase = (time % 600) / 600;
    const value = phase < 0.18 ? 3 : 0.05;
    snapshot = detector.addSample(value, time);
  }
  assert.equal(snapshot.walking, true);
  assert.ok(snapshot.cadence >= 90 && snapshot.cadence <= 110);
  assert.equal(detector.tick(3300).walking, false);
});

test('does not treat isolated motion as walking', () => {
  const detector = new WalkingDetector({ threshold: 0.8 });
  const values = [0, 3, 0, 0, 0, 2.5, 0];
  let snapshot;
  values.forEach((value, index) => { snapshot = detector.addSample(value, index * 900); });
  assert.equal(snapshot.walking, false);
});

test('removes the gravity baseline when linear acceleration is unavailable', () => {
  const state = { gravity: null };
  const event = { acceleration: null, accelerationIncludingGravity: { x: 0, y: 0, z: 9.81 } };
  assert.equal(motionMagnitude(event, state), 0);
  assert.ok(motionMagnitude({ ...event, accelerationIncludingGravity: { x: 0, y: 0, z: 11 } }, state) > 1);
});
