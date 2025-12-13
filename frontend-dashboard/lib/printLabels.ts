const PRINT_BASE = process.env.NEXT_PUBLIC_API_BASE || ''

export type PrintLabelsResponse = { count: number }

export async function printLabels(values: string[]): Promise<PrintLabelsResponse> {
  const payload = { values }
  const token = typeof window !== 'undefined' ? localStorage.getItem('ktp_token') : null

  const res = await fetch(`${PRINT_BASE}/print/multi`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(payload),
    cache: 'no-store',
  })

  if (!res.ok) {
    const body = await res.text()
    if (res.status === 403) {
      throw new Error(body || 'Etiket yazdirma yetkiniz bulunmamaktadir.')
    }
    throw new Error(body || 'Etiket yazdirma istegi basarisiz oldu.')
  }

  if (res.headers.get('content-type')?.includes('application/json')) {
    return res.json()
  }

  return { count: values.length }
}
