const PRINT_BASE = process.env.NEXT_PUBLIC_PRINT_BASE ?? 'http://localhost:8080'

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
    throw new Error(body || 'Etiket yazdırma isteği başarısız oldu.')
  }

  if (res.headers.get('content-type')?.includes('application/json')) {
    return res.json()
  }

  return { count: values.length }
}

