import { createHash } from 'node:crypto'
import { readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In development, Vite forwards /api requests to the local ASP.NET Core API.
// In production, Nginx on EC2 #1 does the same job, so the React code always
// calls the relative path /api and never needs to know the backend address.
const apiTarget = process.env.PENNYWISE_API_URL ?? 'http://localhost:5080'
const proxy = { '/api': { target: apiTarget, changeOrigin: true } }

// Writes dist/sw.js from pwa/sw.js after each build, filling in the list of
// built files to cache and a version made from their contents. A deployment
// that changes any file therefore changes sw.js, and browsers pick it up.
function serviceWorker() {
  let outDir
  const listFiles = (dir) =>
    readdirSync(dir).flatMap((name) => {
      const path = join(dir, name)
      return statSync(path).isDirectory() ? listFiles(path) : [path]
    })

  return {
    name: 'pennywise-service-worker',
    apply: 'build',
    configResolved(config) {
      outDir = config.build.outDir
    },
    closeBundle() {
      const files = listFiles(outDir)
        .map((path) => '/' + relative(outDir, path).split(sep).join('/'))
        .filter((url) => url !== '/sw.js' && !url.endsWith('.map'))
        .sort()
      const hash = createHash('sha256')
      for (const url of files) hash.update(url).update(readFileSync(join(outDir, url)))
      const source = readFileSync('pwa/sw.js', 'utf8')
        .replace("'__PENNYWISE_VERSION__'", JSON.stringify(hash.digest('hex').slice(0, 12)))
        .replace('__PENNYWISE_PRECACHE__', JSON.stringify(files))
      writeFileSync(join(outDir, 'sw.js'), source)
    },
  }
}

export default defineConfig({
  plugins: [react(), serviceWorker()],
  server: { proxy },
  preview: { proxy },
})
