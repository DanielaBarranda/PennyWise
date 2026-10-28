// Small line icons drawn on a 24px grid. They always sit next to text (or have
// an aria-label on their button), so they are hidden from screen readers.
const PATHS = {
  plus: 'M12 5v14M5 12h14',
  pencil: 'M4 20h4L18.5 9.5a2.12 2.12 0 0 0-3-3L5 17v3zM13.5 6.5l3 3',
  trash: 'M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13M10 11v6M14 11v6',
  chevronLeft: 'M15 6l-6 6 6 6',
  chevronRight: 'M9 6l6 6-6 6',
  calendar: 'M5 5h14v15H5zM5 9.5h14M9 3v4M15 3v4',
  dashboard: 'M4 4h7v7H4zM13 4h7v4h-7zM13 10h7v10h-7zM4 13h7v7H4z',
  receipt: 'M6 3h12v18l-3-2-3 2-3-2-3 2zM9 8h6M9 12h6M9 16h3',
  wallet: 'M19 7V5H6a2 2 0 0 0 0 4h14v11H6a2 2 0 0 1-2-2V7M16 14.5h.01',
  spent: 'M3 7l6 6 4-4 8 8M21 11v6h-6',
  gauge: 'M4 17a8 8 0 1 1 16 0M12 17l4-5',
  check: 'M5 12.5l4.5 4.5L19 7.5',
  alert: 'M12 4L2.8 20h18.4zM12 10v4.5M12 17.5h.01',
  close: 'M6 6l12 12M18 6L6 18',
  info: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM12 11v5M12 8h.01',
  sidebar: 'M5 4.5h14A1.5 1.5 0 0 1 20.5 6v12a1.5 1.5 0 0 1-1.5 1.5H5A1.5 1.5 0 0 1 3.5 18V6A1.5 1.5 0 0 1 5 4.5zM9.5 4.5v15',
  // Categories
  Food: 'M7 3v8M5 3v5a2 2 0 0 0 4 0V3M7 11v10M17 21V3c-2.2 1.3-3.5 4-3.5 7.5H17',
  Transportation: 'M6.5 4h11A1.5 1.5 0 0 1 19 5.5V17H5V5.5A1.5 1.5 0 0 1 6.5 4zM5 11h14M8 20v-3M16 20v-3M8.5 14h.01M15.5 14h.01',
  School: 'M4 5.5A1.5 1.5 0 0 1 5.5 4H11v16H5.5A1.5 1.5 0 0 1 4 18.5zM20 5.5A1.5 1.5 0 0 0 18.5 4H13v16h5.5a1.5 1.5 0 0 0 1.5-1.5z',
  Bills: 'M13 3L5 13.5h6L10 21l9-11h-6z',
  Entertainment: 'M4 7h16v3a2 2 0 0 0 0 4v3H4v-3a2 2 0 0 0 0-4zM14.5 7v2M14.5 11.5v1M14.5 15v2',
  Others: 'M4 8l8-4 8 4v8l-8 4-8-4zM4 8l8 4 8-4M12 12v8',
}

export default function Icon({ name, size = 18, className = '' }) {
  return (
    <svg
      className={`icon ${className}`}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <path d={PATHS[name]} />
    </svg>
  )
}
