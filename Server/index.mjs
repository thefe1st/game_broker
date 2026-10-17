import { createGateway } from './gateway.mjs';
const port = Number(process.env.PORT || 8787);
if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('Invalid PORT');
const server = createGateway({
  mode: process.env.MODE || 'mock',
  apiKey: process.env.API_KEY || '',
  model: process.env.MODEL || '',
  baseUrl: process.env.API_BASE_URL || 'https://api.openai.com/v1/'
});
server.listen(port, '127.0.0.1', () => {
  console.log(`Broker gateway: http://127.0.0.1:${port} (${process.env.MODE || 'mock'})`);
});
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => {
  server.close(() => process.exit(0));
  setTimeout(() => process.exit(1), 3000).unref();
});
