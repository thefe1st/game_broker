// Small bounded PCM16 WAV validator shared by upload and provider output.
export function validateWav(bytes, maxSeconds = 20) {
  if (!Buffer.isBuffer(bytes) || bytes.length < 44 || bytes.length > 2000000) return false;
  if (bytes.toString('ascii', 0, 4) !== 'RIFF' || bytes.toString('ascii', 8, 12) !== 'WAVE') return false;
  if (bytes.readUInt32LE(4) + 8 !== bytes.length) return false;
  let format, dataSize;
  for (let offset = 12; offset + 8 <= bytes.length;) {
    const type = bytes.toString('ascii', offset, offset + 4), length = bytes.readUInt32LE(offset + 4);
    const start = offset + 8;
    if (length > bytes.length - start) return false;
    if (type === 'fmt ') {
      if (format || length < 16) return false;
      format = { pcm: bytes.readUInt16LE(start), channels: bytes.readUInt16LE(start + 2),
        rate: bytes.readUInt32LE(start + 4), byteRate: bytes.readUInt32LE(start + 8),
        align: bytes.readUInt16LE(start + 12), bits: bytes.readUInt16LE(start + 14) };
    }
    if (type === 'data') { if (dataSize !== undefined) return false; dataSize = length; }
    offset = start + length + (length % 2);
  }
  if (!format || !dataSize || format.pcm !== 1 || format.bits !== 16 || ![1, 2].includes(format.channels)) return false;
  if (format.rate < 8000 || format.rate > 48000 || format.align !== format.channels * 2
    || format.byteRate !== format.rate * format.align || dataSize % format.align !== 0) return false;
  const seconds = dataSize / format.byteRate;
  return seconds >= 0.1 && seconds <= maxSeconds;
}

// Some streaming WAV providers use unknown RIFF/data sizes (0xffffffff).
// We already buffered a bounded complete body, so replace only these sentinels.
export function normalizeStreamingWav(bytes) {
  if (!Buffer.isBuffer(bytes) || bytes.length < 44 || bytes.length > 2000000
    || bytes.toString('ascii', 0, 4) !== 'RIFF' || bytes.toString('ascii', 8, 12) !== 'WAVE') return bytes;
  const result = Buffer.from(bytes);
  if (result.readUInt32LE(4) === 0xffffffff) result.writeUInt32LE(result.length - 8, 4);
  for (let offset = 12; offset + 8 <= result.length;) {
    const tag = result.toString('ascii', offset, offset + 4), start = offset + 8;
    const size = result.readUInt32LE(offset + 4);
    if (tag === 'data' && size === 0xffffffff) {
      result.writeUInt32LE(result.length - start, offset + 4);
      break;
    }
    if (size > result.length - start) break;
    offset = start + size + (size % 2);
  }
  return result;
}

export async function readBoundedBytes(response, limit) {
  if (!response.body) throw new Error('empty upstream body');
  const reader = response.body.getReader(), chunks = [];
  let size = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > limit) { await reader.cancel(); throw new Error('upstream body too large'); }
      chunks.push(Buffer.from(value));
    }
  } finally { reader.releaseLock(); }
  return Buffer.concat(chunks);
}

export async function handleVoice({ path, bytes, send, sendAudio, fetchImpl, base,
  apiKey, signal, sttModel, ttsModel, ttsVoice }) {
  const headers = { Authorization: `Bearer ${apiKey}` };
  if (path === '/transcribe') {
    if (!validateWav(bytes)) { send(400, { error: 'invalid_wav' }); return; }
    const form = new FormData();
    form.append('model', sttModel); form.append('language', 'ru');
    form.append('response_format', 'json');
    form.append('file', new Blob([bytes], { type: 'audio/wav' }), 'speech.wav');
    const response = await fetchImpl(new URL('audio/transcriptions', base), {
      method: 'POST', headers, body: form, signal, redirect: 'error'
    });
    if (!response.ok) { await response.body?.cancel(); send(502, { error: 'upstream_failed' }); return; }
    const result = JSON.parse((await readBoundedBytes(response, 8192)).toString('utf8'));
    if (typeof result.text !== 'string' || !result.text.trim() || result.text.length > 500) {
      send(502, { error: 'invalid_transcription' }); return;
    }
    send(200, { text: result.text.trim() });
  } else {
    let data;
    try { data = JSON.parse(bytes.toString('utf8')); }
    catch { send(400, { error: 'invalid_json' }); return; }
    if (!data || Array.isArray(data) || typeof data.text !== 'string' || !data.text.trim() || data.text.length > 400) {
      send(400, { error: 'invalid_speech_text' }); return;
    }
    const response = await fetchImpl(new URL('audio/speech', base), {
      method: 'POST', headers: { ...headers, 'Content-Type': 'application/json' }, signal, redirect: 'error',
      body: JSON.stringify({ model: ttsModel, voice: ttsVoice, input: data.text.trim(), response_format: 'wav' })
    });
    if (!response.ok) { await response.body?.cancel(); send(502, { error: 'upstream_failed' }); return; }
    const audio = normalizeStreamingWav(await readBoundedBytes(response, 2000000));
    if (!validateWav(audio, 30)) { send(502, { error: 'invalid_speech_audio' }); return; }
    sendAudio(audio);
  }
}
