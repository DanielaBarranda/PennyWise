import { useState } from 'react'
import Icon from './Icon.jsx'
import { isIos, isStandalone, promptInstall, usePwa } from '../../pwa/pwa.js'

// Explains how to install PennyWise on this device, with a one-tap button
// where the browser supports it (Chrome, Edge, Brave, Samsung Internet, Android).
export default function InstallApp() {
  const { canInstall } = usePwa()
  const [installed, setInstalled] = useState(false)

  if (installed || isStandalone()) {
    return <p className="profile__hint">You’re using the installed PennyWise app.</p>
  }
  if (canInstall) {
    return (
      <>
        <p className="profile__hint">Add PennyWise to your home screen or desktop. It opens in its own window, starts faster and works with a weak connection.</p>
        <button type="button" className="button button--secondary" onClick={async () => setInstalled(await promptInstall())}>
          <Icon name="download" />
          Install app
        </button>
      </>
    )
  }
  if (isIos()) {
    return (
      <p className="profile__hint">
        On iPhone or iPad, open PennyWise in Safari, tap <strong>Share</strong>, then <strong>Add to Home Screen</strong>.
      </p>
    )
  }
  return (
    <p className="profile__hint">
      Use your browser’s menu to install PennyWise (look for “Install app” or “Add to Home screen”). If it’s already
      installed, open it from your home screen or app list.
    </p>
  )
}
