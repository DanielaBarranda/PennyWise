import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './styles/base.css'
import './styles/layout.css'
import './styles/components.css'
import './styles/features.css'
import App from './App.jsx'
import { startPwa } from './pwa/pwa.js'

startPwa()

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
