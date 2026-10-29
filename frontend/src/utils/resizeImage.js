// Crops a chosen photo to a centred square and shrinks it to 256 x 256 JPEG
// (usually 15-40 KB) before upload. Phone photos are rotated correctly first.
export const AVATAR_SIZE = 256
const MAX_INPUT_BYTES = 20 * 1024 * 1024

export async function resizeAvatar(file) {
  if (!/^image\/(jpeg|png|webp)$/.test(file.type)) throw new Error('Choose a JPEG, PNG or WebP photo.')
  if (file.size > MAX_INPUT_BYTES) throw new Error('That photo is too large. Choose one under 20 MB.')

  let bitmap
  try {
    bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
  } catch {
    throw new Error('That photo could not be opened. Try a different JPEG, PNG or WebP file.')
  }

  const side = Math.min(bitmap.width, bitmap.height)
  const canvas = document.createElement('canvas')
  canvas.width = AVATAR_SIZE
  canvas.height = AVATAR_SIZE
  const context = canvas.getContext('2d')
  context.fillStyle = '#ffffff' // transparent PNGs get a white background, not black
  context.fillRect(0, 0, AVATAR_SIZE, AVATAR_SIZE)
  context.imageSmoothingQuality = 'high'
  context.drawImage(bitmap, (bitmap.width - side) / 2, (bitmap.height - side) / 2, side, side, 0, 0, AVATAR_SIZE, AVATAR_SIZE)
  bitmap.close()

  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('That photo could not be prepared.'))), 'image/jpeg', 0.86),
  )
}
