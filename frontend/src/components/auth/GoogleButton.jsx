import { useEffect, useRef, useState } from 'react'
import { api } from '../../services/api.js'

// Google's own sign-in button (Google Identity Services). Google requires its
// button to be used as-is, so it is drawn by Google's script inside our layout.
// Pressing it gives an ID token ("credential") that the API checks with Google's
// keys. The button only appears when the API has a Google client ID set up.
const SCRIPT_URL = 'https://accounts.google.com/gsi/client'

let setup = null
// Loads the client ID and Google's script once per page.
function loadGoogle() {
  setup ??= api
    .authOptions()
    .then(({ googleClientId }) => {
      if (!googleClientId) return null
      return new Promise((resolve) => {
        const script = document.createElement('script')
        script.src = SCRIPT_URL
        script.async = true
        script.onload = () => resolve(window.google?.accounts?.id ? googleClientId : null)
        script.onerror = () => resolve(null)
        document.head.appendChild(script)
      })
    })
    .catch(() => null)
    .then((clientId) => {
      if (!clientId) setup = null // try again next time (e.g. after coming back online)
      return clientId
    })
  return setup
}

function useTheme() {
  const [theme, setTheme] = useState(() => document.documentElement.dataset.theme)
  useEffect(() => {
    const observer = new MutationObserver(() => setTheme(document.documentElement.dataset.theme))
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] })
    return () => observer.disconnect()
  }, [])
  return theme
}

// `text` is Google's wording: 'continue_with', 'signup_with' or 'signin_with'.
// `divider` adds an "or" line under the button, before the email form.
export default function GoogleButton({ onCredential, text = 'continue_with', divider = false, disabled = false }) {
  const containerRef = useRef(null)
  const callbackRef = useRef(onCredential)
  const [clientId, setClientId] = useState(null)
  const theme = useTheme()

  useEffect(() => {
    callbackRef.current = onCredential
  })

  useEffect(() => {
    let active = true
    loadGoogle().then((id) => active && setClientId(id))
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    const container = containerRef.current
    if (!clientId || !container) return
    const google = window.google.accounts.id
    google.initialize({
      client_id: clientId,
      callback: (response) => callbackRef.current(response.credential),
      ux_mode: 'popup',
      context: text === 'signup_with' ? 'signup' : 'signin',
      itp_support: true,
    })
    google.renderButton(container, {
      type: 'standard',
      theme: theme === 'light' ? 'outline' : 'filled_black',
      size: 'large',
      shape: 'pill',
      text,
      logo_alignment: 'center',
      locale: 'en', // the rest of PennyWise is in English
      // Google draws the button at a fixed width (200–400px).
      width: Math.max(200, Math.min(400, Math.floor(container.getBoundingClientRect().width))),
    })
  }, [clientId, theme, text])

  if (!clientId) return null

  return (
    <div className={`google-signin${disabled ? ' google-signin--disabled' : ''}`}>
      <div ref={containerRef} className="google-signin__button" />
      {divider && (
        <p className="google-signin__divider">
          <span>or use your email</span>
        </p>
      )}
    </div>
  )
}
