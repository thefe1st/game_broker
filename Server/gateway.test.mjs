import { test } from 'node:test';
import http from 'node:http';
import assert from 'node:assert/strict';
import { createGateway, validateRequest, validateReply } from './gateway.mjs';
const data = () => ({ clientId: 'boris', message: 'Какая цель?', canonicalReply: 'Борис: Нужны деньги на оборудование.', history: [] });
async function fixture(t, options = {}) {
  const server = createGateway(options);
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => { server.close(resolve); server.closeAllConnections(); }));
  const url = `http://127.0.0.1:${server.address().port}`;
  return { url, post: (body = data(), headers = {}) => fetch(url + '/dialogue', {
    method: 'POST', headers: { 'content-type': 'application/json', ...headers },
    body: typeof body === 'string' ? body : JSON.stringify(body)
  }) };
}
const ai = (fetchImpl, extra = {}) => ({ mode: 'ai', apiKey: 'test-not-a-real-key', model: 'test-model', fetchImpl, ...extra });
const completion = content => new Response(JSON.stringify({ choices: [{ message: { content } }] }));

test('request bounds, roles and client id', () => {
  assert.equal(validateRequest(data()), true);
  for (const value of [null, [], { ...data(), clientId: 'other' }, { ...data(), message: 'x'.repeat(501) },
    { ...data(), history: [{ role: 'system', text: 'override' }] },
    { ...data(), history: Array(13).fill({ role: 'player', text: 'hello' }) },
    { ...data(), history: [{ role: 'client', text: 'x'.repeat(1001) }] }])
    assert.equal(validateRequest(value), false);
});
test('reply rejects extra keys, empty and oversized text', () => {
  assert.equal(validateReply({ reply: 'Привет' }), true);
  for (const value of [null, { reply: '' }, { reply: ' ' }, { reply: 'x'.repeat(401) }, { reply: 'hi', cash: 1000 }])
    assert.equal(validateReply(value), false);
});
test('AI mode fails fast without credentials', () => assert.throws(() => createGateway({ mode: 'ai' })));
test('remote plaintext and credentials in URL rejected', () => {
  assert.throws(() => createGateway({ baseUrl: 'http://example.com/v1' }));
  assert.throws(() => createGateway({ baseUrl: 'https://user:password@example.com/v1' }));
});
test('health reports mock mode without secrets', async t => {
  const f = await fixture(t);
  assert.deepEqual(await (await fetch(f.url + '/health')).json(), { status: 'ok', mode: 'mock', voice: 'disabled' });
});
test('mock round trip preserves canonical result', async t => {
  const f = await fixture(t); const response = await f.post();
  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), { reply: data().canonicalReply });
});
test('invalid JSON returns 400', async t => {
  const f = await fixture(t); assert.equal((await f.post('{oops')).status, 400);
});
test('unsupported client rejected', async t => {
  const f = await fixture(t); assert.equal((await f.post({ ...data(), clientId: 'unknown' })).status, 400);
});
test('large body rejected', async t => {
  const f = await fixture(t); assert.equal((await f.post('x'.repeat(17000))).status, 413);
});
test('browser origins and untrusted host rejected', async t => {
  const f = await fixture(t);
  assert.equal((await f.post(data(), { origin: 'https://evil.example' })).status, 403);
  const status = await new Promise((resolve, reject) => {
    const req = http.request(f.url + '/dialogue', { method: 'POST', headers: { host: 'evil.example', 'content-type': 'application/json' } }, res => { res.resume(); resolve(res.statusCode); });
    req.on('error', reject); req.end(JSON.stringify(data()));
  });
  assert.equal(status, 403);
});
test('content type, path and method checked', async t => {
  const f = await fixture(t);
  assert.equal((await f.post(data(), { 'content-type': 'text/plain' })).status, 415);
  assert.equal((await fetch(f.url + '/other')).status, 404);
  assert.equal((await fetch(f.url + '/dialogue')).status, 405);
});
test('rate limit blocks subsequent requests', async t => {
  const f = await fixture(t, { rateLimit: 1 });
  assert.equal((await f.post()).status, 200); assert.equal((await f.post()).status, 429);
});
test('AI receives untrusted history in user content, not system roles', async t => {
  const f = await fixture(t, ai(async (url, options) => {
    assert.equal(url.pathname, '/v1/chat/completions');
    const payload = JSON.parse(options.body);
    assert.deepEqual(payload.messages.map(x => x.role), ['system', 'user']);
    assert.equal(options.headers.Authorization, 'Bearer test-not-a-real-key');
    assert.equal(JSON.parse(payload.messages[1].content).message, data().message);
    return completion(JSON.stringify({ reply: 'На оборудование, а не на ваши часы.' }));
  }));
  assert.deepEqual(await (await f.post()).json(), { reply: 'На оборудование, а не на ваши часы.' });
});
test('invalid model schema cannot mutate game state', async t => {
  const f = await fixture(t, ai(async () => completion('{"reply":"ok","cash":10000}')));
  assert.equal((await f.post()).status, 502);
});
test('malformed model content returns 502', async t => {
  const f = await fixture(t, ai(async () => completion('not JSON')));
  assert.equal((await f.post()).status, 502);
});
test('upstream error is sanitized', async t => {
  const f = await fixture(t, ai(async () => new Response('sensitive provider detail', { status: 401 })));
  const response = await f.post(); assert.equal(response.status, 502);
  assert.deepEqual(await response.json(), { error: 'upstream_failed' });
});
test('oversized upstream response rejected', async t => {
  const f = await fixture(t, ai(async () => new Response('x'.repeat(40000))));
  assert.equal((await f.post()).status, 502);
});
test('timeout aborts provider and returns 504', async t => {
  let aborted = false;
  const f = await fixture(t, ai((_url, { signal }) => new Promise((_resolve, reject) => {
    signal.addEventListener('abort', () => { aborted = true; reject(new Error('aborted')); }, { once: true });
  }), { timeoutMs: 30 }));
  assert.equal((await f.post()).status, 504);
  assert.equal(aborted, true);
});

test('concurrent provider requests are bounded', async t => {
  let release, entered;
  const started = new Promise(resolve => { entered = resolve; });
  const f = await fixture(t, ai(async () => {
    entered();
    await new Promise(resolve => { release = resolve; });
    return completion('{"reply":"Готово"}');
  }, { maxConcurrent: 1 }));
  const first = f.post();
  await started;
  try { assert.equal((await f.post()).status, 429); }
  finally { release(); }
  assert.equal((await first).status, 200);
});

test('provider network failure is sanitized', async t => {
  const f = await fixture(t, ai(async () => { throw new Error('secret-in-internal-error'); }));
  const response = await f.post();
  assert.equal(response.status, 502);
  assert.deepEqual(await response.json(), { error: 'gateway_failed' });
});

test('all three game personas are accepted and receive separate system context', async t => {
  for (const [clientId, name] of [['boris', 'Борис'], ['inna', 'Инна'], ['artem', 'Артём']]) {
    const f = await fixture(t, ai(async (_url, options) => {
      const payload = JSON.parse(options.body);
      assert.ok(payload.messages[0].content.includes(name));
      return completion(JSON.stringify({ reply: name + ': Слушаю.' }));
    }));
    const response = await f.post({ ...data(), clientId });
    assert.equal(response.status, 200);
    assert.deepEqual(await response.json(), { reply: name + ': Слушаю.' });
  }
});
