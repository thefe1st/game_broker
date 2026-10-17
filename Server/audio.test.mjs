import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createGateway } from './gateway.mjs';
import { validateWav } from './audio.mjs';

function wav(seconds = 0.2) {
  const rate = 16000, size = Math.round(rate * seconds) * 2, b = Buffer.alloc(44 + size);
  b.write('RIFF'); b.writeUInt32LE(36 + size, 4); b.write('WAVEfmt ', 8);
  b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22);
  b.writeUInt32LE(rate, 24); b.writeUInt32LE(rate * 2, 28); b.writeUInt16LE(2, 32);
  b.writeUInt16LE(16, 34); b.write('data', 36); b.writeUInt32LE(size, 40);
  return b;
}
async function fixture(t, extra = {}) {
  const server = createGateway({ apiKey: 'fake-key', voiceMode: 'ai', ...extra });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => { server.close(resolve); server.closeAllConnections(); }));
  return async (path, body, contentType = path === '/transcribe' ? 'audio/wav' : 'application/json') => fetch(
    `http://127.0.0.1:${server.address().port}${path}`, { method: 'POST', body,
      headers: { 'content-type': contentType } });
}
const speech = text => JSON.stringify({ text });

test('WAV validator accepts PCM16 and rejects malformed headers', () => {
  assert.equal(validateWav(wav()), true);
  for (const sample of [Buffer.alloc(44), wav().subarray(0, 100), wav(0.05), wav(21)])
    assert.equal(validateWav(sample), false);
  const wrongRate = wav(); wrongRate.writeUInt32LE(5, 24);
  assert.equal(validateWav(wrongRate), false);
  const floatFormat = wav(); floatFormat.writeUInt16LE(3, 20);
  assert.equal(validateWav(floatFormat), false);
});
test('voice disabled is explicit and never calls upstream', async t => {
  const post = await fixture(t, { voiceMode: 'disabled', fetchImpl: () => assert.fail('external call') });
  const r = await post('/transcribe', wav()); assert.equal(r.status, 503);
  assert.deepEqual(await r.json(), { error: 'voice_not_configured' });
});
test('voice mode needs key and validates mode', () => {
  assert.throws(() => createGateway({ voiceMode: 'ai' }));
  assert.throws(() => createGateway({ voiceMode: 'other' }));
});
test('transcription sends WAV as multipart and returns bounded text', async t => {
  const post = await fixture(t, { fetchImpl: async (url, options) => {
    assert.equal(url.pathname, '/v1/audio/transcriptions');
    assert.equal(options.headers.Authorization, 'Bearer fake-key');
    assert.equal(options.body.get('language'), 'ru');
    assert.equal(options.body.get('model'), 'whisper-1');
    assert.equal(options.body.get('file').type, 'audio/wav');
    return Response.json({ text: '  Какая у вас цель?  ' });
  } });
  const response = await post('/transcribe', wav());
  assert.equal(response.status, 200); assert.deepEqual(await response.json(), { text: 'Какая у вас цель?' });
});
test('invalid WAV rejected without upstream', async t => {
  const post = await fixture(t, { fetchImpl: () => assert.fail('external call') });
  assert.equal((await post('/transcribe', 'not-wav')).status, 400);
});
test('transcription rejects unsupported media type', async t => {
  const post = await fixture(t); assert.equal((await post('/transcribe', wav(), 'audio/mp3')).status, 415);
});
test('transcription rejects oversized upload', async t => {
  const post = await fixture(t); assert.equal((await post('/transcribe', Buffer.alloc(2000001))).status, 413);
});
test('oversized and empty transcriptions rejected', async t => {
  for (const text of ['', ' ', 'x'.repeat(501)]) {
    const post = await fixture(t, { fetchImpl: async () => Response.json({ text }) });
    assert.equal((await post('/transcribe', wav())).status, 502);
  }
});
test('speech requests provider WAV and streams validated bytes back', async t => {
  const expected = wav();
  const post = await fixture(t, { fetchImpl: async (url, options) => {
    assert.equal(url.pathname, '/v1/audio/speech');
    const body = JSON.parse(options.body);
    assert.equal(body.response_format, 'wav'); assert.equal(body.voice, 'alloy');
    assert.equal(body.input, 'Привет');
    return new Response(expected, { headers: { 'Content-Type': 'audio/wav' } });
  } });
  const response = await post('/speech', speech('Привет'));
  assert.equal(response.status, 200); assert.equal(response.headers.get('content-type'), 'audio/wav');
  assert.deepEqual(Buffer.from(await response.arrayBuffer()), expected);
});
test('speech rejects invalid or too long text', async t => {
  const post = await fixture(t, { fetchImpl: () => assert.fail('external call') });
  for (const body of ['{oops', '[]', speech(' '), speech('x'.repeat(401))])
    assert.equal((await post('/speech', body)).status, 400);
});
test('speech rejects bad provider audio', async t => {
  const post = await fixture(t, { fetchImpl: async () => new Response('not-wav') });
  assert.equal((await post('/speech', speech('Привет'))).status, 502);
});
test('speech failure never exposes provider detail', async t => {
  const post = await fixture(t, { fetchImpl: async () => new Response('sensitive details', { status: 403 }) });
  const response = await post('/speech', speech('Привет'));
  assert.equal(response.status, 502); assert.deepEqual(await response.json(), { error: 'upstream_failed' });
});
test('voice timeout cancels upstream', async t => {
  let aborted = false;
  const post = await fixture(t, { voiceTimeoutMs: 30, fetchImpl: (_url, { signal }) => new Promise((_r, reject) => {
    signal.addEventListener('abort', () => { aborted = true; reject(new Error('cancelled')); }, { once: true });
  }) });
  assert.equal((await post('/transcribe', wav())).status, 504); assert.equal(aborted, true);
});


test('speech normalizes unknown streaming WAV lengths before playback', async t => {
  const streamed = wav(); streamed.writeUInt32LE(0xffffffff, 4); streamed.writeUInt32LE(0xffffffff, 40);
  const post = await fixture(t, { fetchImpl: async () => new Response(streamed) });
  const response = await post('/speech', speech('Привет'));
  assert.equal(response.status, 200);
  assert.deepEqual(Buffer.from(await response.arrayBuffer()), wav());
});
