import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Dev proxy mirrors nginx.conf: the API serves /api and the SignalR hub.
const api = 'http://localhost:5180'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': api,
      '/hubs': { target: api, ws: true },
    },
  },
  test: {
    environment: 'node',
  },
})
