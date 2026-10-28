import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In development, Vite forwards /api requests to the local ASP.NET Core API.
// In production, Nginx on EC2 #1 does the same job, so the React code always
// calls the relative path /api and never needs to know the backend address.
const apiTarget = process.env.PENNYWISE_API_URL ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true },
    },
  },
})
