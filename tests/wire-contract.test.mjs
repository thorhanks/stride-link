import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const browser = readFileSync(new URL('../wwwroot/app.js', import.meta.url), 'utf8');
const server = readFileSync(new URL('../Networking/ConnectionHub.cs', import.meta.url), 'utf8');

test('browser and server agree on camelCase movement fields', () => {
  assert.match(browser, /send\(\{ type: 'movement', active \}\)/);
  assert.match(browser, /send\(\{ type: 'heartbeat', active:/);
  assert.match(server, /JsonPropertyName\("type"\)/);
  assert.match(server, /JsonPropertyName\("active"\)/);
});
