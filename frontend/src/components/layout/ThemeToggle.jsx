import Icon from '../common/Icon.jsx'

// Shows the mode you will switch TO, the usual convention for theme buttons.
export default function ThemeToggle({ theme, onToggle, className = '' }) {
  const next = theme === 'dark' ? 'light' : 'dark'
  return (
    <button
      type="button"
      className={`icon-button theme-toggle ${className}`}
      onClick={onToggle}
      aria-label={`Switch to ${next} mode`}
      title={`Switch to ${next} mode`}
    >
      <Icon name={next === 'light' ? 'sun' : 'moon'} size={20} />
    </button>
  )
}
