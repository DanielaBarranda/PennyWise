import { useMemo } from 'react'
import qrcode from 'qrcode-generator'

// A QR code drawn as one SVG path (dark squares on a white tile, so phones can
// read it in dark mode too). Made in the browser; the text never leaves it.
export default function QrCode({ text, label, size = 200 }) {
  const { count, path } = useMemo(() => {
    const qr = qrcode(0, 'M')
    qr.addData(text)
    qr.make()
    const n = qr.getModuleCount()
    let d = ''
    for (let row = 0; row < n; row++) {
      for (let col = 0; col < n; col++) {
        if (qr.isDark(row, col)) d += `M${col + 4} ${row + 4}h1v1h-1z`
      }
    }
    return { count: n + 8, path: d } // 4-module quiet zone on every side
  }, [text])

  return (
    <svg className="qr-code" role="img" aria-label={label} width={size} height={size} viewBox={`0 0 ${count} ${count}`} shapeRendering="crispEdges">
      <rect width={count} height={count} fill="#fff" />
      <path d={path} fill="#000" />
    </svg>
  )
}
