import Icon from './Icon.jsx'
import { applyUpdate, usePwa } from '../../pwa/pwa.js'

// Shown at the top of every page when it matters: no connection, or a new
// version of PennyWise has been downloaded and is ready to use.
export default function PwaNotice() {
  const { online, updateReady } = usePwa()

  return (
    <>
      {!online && (
        <p className="pwa-notice pwa-notice--offline" role="status">
          <Icon name="alert" size={16} />
          You’re offline. PennyWise needs a connection to load and save your data; nothing you see is lost.
        </p>
      )}
      {updateReady && (
        <div className="pwa-notice pwa-notice--update" role="status">
          <Icon name="spark" size={16} />
          <span>A new version of PennyWise is ready.</span>
          <button type="button" className="button button--primary button--small" onClick={applyUpdate}>
            Reload to update
          </button>
        </div>
      )}
    </>
  )
}
