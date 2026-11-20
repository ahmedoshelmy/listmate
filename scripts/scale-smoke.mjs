// Scale-out proof against `docker compose -f compose.yaml -f compose.scale.yaml up`:
// a token issued by replica B works on replica A (shared Data Protection keys), and a write through replica B
// reaches a SignalR client connected to replica A (Redis backplane). Needs `npm ci` in src/Web for the client.
import { createRequire } from 'node:module'
import { randomUUID } from 'node:crypto'

const require = createRequire(new URL('../src/Web/package.json', import.meta.url))
const signalR = require('@microsoft/signalr')

const A = process.env.API_A ?? 'http://localhost:5180'
const B = process.env.API_B ?? 'http://localhost:5181'
const password = 'Smoke-test-1'
const run = Date.now()

async function call(base, path, body, token) {
  const response = await fetch(base + path, {
    method: body === undefined ? 'GET' : 'POST',
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) throw new Error(`${base}${path} -> ${response.status}`)
  const text = await response.text()
  return text ? JSON.parse(text) : undefined
}

async function account(name, registerOn, loginOn) {
  const email = `${name}-${run}@example.com`
  await call(registerOn, '/api/auth/register', { email, password })
  return (await call(loginOn, '/api/auth/login', { email, password })).accessToken
}

const writer = await account('scale-writer', A, B)
const reader = await account('scale-reader', B, A)
await call(A, '/api/lists', undefined, writer)
console.log('token issued by replica B accepted by replica A')

const list = await call(B, '/api/lists', { name: 'Beach trip packing' }, writer)
await call(A, '/api/lists/join', { inviteCode: list.inviteCode }, reader)

const hub = new signalR.HubConnectionBuilder()
  .withUrl(`${A}/hubs/lists`, {
    accessTokenFactory: () => reader,
    skipNegotiation: true,
    transport: signalR.HttpTransportType.WebSockets,
  })
  .configureLogging(signalR.LogLevel.Warning)
  .build()
const itemId = randomUUID()
const received = new Promise((resolve, reject) => {
  const timer = setTimeout(() => reject(new Error('no ItemChanged within 10 s')), 10_000)
  hub.on('ItemChanged', (m) => {
    if (m.item.id === itemId) {
      clearTimeout(timer)
      resolve(m)
    }
  })
})
hub.on('PresenceChanged', () => {})
await hub.start()
await hub.invoke('JoinList', list.id)
console.log('reader connected to the hub on replica A')

await call(B, '/api/sync', { mutations: [{ mutationId: randomUUID(), type: 'add', listId: list.id, itemId, text: '4 towels' }] }, writer)
const message = await received
console.log(`reader on A got ItemChanged for "${message.item.quantity}x ${message.item.name}" written through B`)
await hub.stop()
console.log('SCALE SMOKE PASS')
