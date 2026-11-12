export class WalkingDetector {
  constructor({ threshold = 1.25, stopDelay = 850 } = {}) {
    this.threshold = threshold;
    this.stopDelay = stopDelay;
    this.reset();
  }

  reset() {
    this.smoothed = 0;
    this.previous = 0;
    this.previousTime = 0;
    this.lastPeak = -Infinity;
    this.peaks = [];
    this.walking = false;
    this.cadence = 0;
  }

  setThreshold(value) { this.threshold = Number(value); }
  setStopDelay(value) { this.stopDelay = Number(value); }

  addSample(magnitude, now) {
    this.smoothed = this.smoothed * 0.62 + magnitude * 0.38;
    const isPeak = this.previous > this.threshold &&
      this.previous >= this.smoothed &&
      now - this.lastPeak >= 280;

    if (isPeak) {
      const peakTime = this.previousTime;
      this.lastPeak = peakTime;
      this.peaks.push(peakTime);
      this.peaks = this.peaks.filter(time => peakTime - time <= 2400);

      const intervals = this.peaks.slice(1).map((time, index) => time - this.peaks[index]);
      const plausible = intervals.filter(interval => interval >= 280 && interval <= 1200);
      if (this.peaks.length >= 3 && plausible.length >= 2) {
        this.walking = true;
        const sorted = [...plausible].sort((a, b) => a - b);
        const median = sorted[Math.floor(sorted.length / 2)];
        this.cadence = Math.round(60000 / median);
      }
    }

    this.previous = this.smoothed;
    this.previousTime = now;
    this.tick(now);
    return this.snapshot();
  }

  tick(now) {
    if (this.walking && now - this.lastPeak > this.stopDelay) {
      this.walking = false;
      this.cadence = 0;
      this.peaks = [];
    }
    return this.snapshot();
  }

  snapshot() {
    return { walking: this.walking, cadence: this.cadence, motion: this.smoothed };
  }
}

export function motionMagnitude(event, state) {
  const acceleration = event.acceleration;
  if (acceleration && [acceleration.x, acceleration.y, acceleration.z].every(Number.isFinite)) {
    return Math.hypot(acceleration.x, acceleration.y, acceleration.z);
  }

  const withGravity = event.accelerationIncludingGravity;
  if (!withGravity || ![withGravity.x, withGravity.y, withGravity.z].every(Number.isFinite))
    return 0;

  const total = Math.hypot(withGravity.x, withGravity.y, withGravity.z);
  state.gravity = state.gravity == null ? total : state.gravity * 0.94 + total * 0.06;
  return Math.abs(total - state.gravity);
}
