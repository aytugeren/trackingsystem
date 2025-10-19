import jsPDF from 'jspdf'

let fontReady: Promise<void> | null = null
const fontName = 'NotoSans'
const fontFileName = 'NotoSans-Regular.ttf'
const fontUrl = 'https://raw.githubusercontent.com/notofonts/noto-fonts/main/hinted/ttf/NotoSans/NotoSans-Regular.ttf'

async function fetchAsBase64(url: string): Promise<string> {
  const res = await fetch(url)
  if (!res.ok) throw new Error(`Font download failed: ${res.status}`)
  const buf = await res.arrayBuffer()
  let binary = ''
  const bytes = new Uint8Array(buf)
  const chunk = 0x8000
  for (let i = 0; i < bytes.length; i += chunk) {
    binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk) as any)
  }
  return btoa(binary)
}

export async function ensurePdfUnicodeFont(doc: jsPDF) {
  if (!fontReady) {
    fontReady = (async () => {
      try {
        const b64 = await fetchAsBase64(fontUrl)
        doc.addFileToVFS(fontFileName, b64)
        doc.addFont(fontFileName, fontName, 'normal')
      } catch (e) {
        // If remote fetch fails, keep default font; PDFs may miss Turkish glyphs.
      }
    })()
  }
  await fontReady
  try { doc.setFont(fontName, 'normal') } catch { /* ignore */ }
}

