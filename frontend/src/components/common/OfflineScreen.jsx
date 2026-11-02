import Icon from './Icon.jsx'

// The app opened without a connection, so it cannot check who is signed in.
// It tries again by itself as soon as the connection comes back.
export default function OfflineScreen({ onRetry }) {
  return (
    <main id="main" className="offline-screen" tabIndex={-1}>
      <img src="/icons/icon-192.png" alt="" width="96" height="96" />
      <h1>You’re offline</h1>
      <p>PennyWise needs an internet connection to show your budget. It will continue automatically when you’re back online.</p>
      <button type="button" className="button button--primary" onClick={onRetry}>
        <Icon name="refresh" />
        Try again
      </button>
    </main>
  )
}
