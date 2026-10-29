import { useState } from 'react'
import { api } from '../../services/api.js'
import { initials } from '../../utils/initials.js'

// The user's profile photo, or their initials when there is none (or it fails
// to load). Decorative: the name is always written next to it or in a label.
export default function Avatar({ user, size }) {
  const [failedVersion, setFailedVersion] = useState(null)
  const showPhoto = user.avatarVersion && failedVersion !== user.avatarVersion
  const className = `avatar${size ? ` avatar--${size}` : ''}${showPhoto ? ' avatar--photo' : ''}`

  return (
    <span className={className} aria-hidden="true">
      {showPhoto ? (
        <img src={api.avatarUrl(user.avatarVersion)} alt="" onError={() => setFailedVersion(user.avatarVersion)} />
      ) : (
        initials(user.name)
      )}
    </span>
  )
}
