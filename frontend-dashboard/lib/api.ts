export type Invoice = {
  id: string
  tarih: string // ISO or yyyy-MM-dd
  siraNo: number
  musteriAdSoyad?: string | null
  tckn?: string | null
  tutar: number
  odemeSekli: number // 0: Havale, 1: KrediKarti
}

export type Expense = {
  id: string
  tarih: string
  siraNo: number
  musteriAdSoyad?: string | null
  tckn?: string | null
  tutar: number
}

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || ''

function authHeaders(): HeadersInit {
  try {
    const token = localStorage.getItem('ktp_token')
    return token ? { Authorization: `Bearer ${token}` } : {}
  } catch { return {} }
}

async function getJson<T>(path: string): Promise<T> {
  const url = `${API_BASE}${path}`
  const res = await fetch(url, { cache: 'no-store', headers: { ...authHeaders() } })
  if (!res.ok) throw new Error('API error')
  return res.json()
}

export const api = {
  listInvoices: () => getJson<Invoice[]>('/api/invoices'),
  listExpenses: () => getJson<Expense[]>('/api/expenses'),
  async login(email: string, password: string): Promise<{ token: string; role: string; email: string }> {
    const url = `${API_BASE}/api/auth/login`
    const res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password }) })
    if (!res.ok) throw new Error('Giriş başarısız')
    return res.json()
  }
}

export function toDateOnlyString(d: Date): string {
  // format as yyyy-MM-dd to compare with DateOnly serialized strings
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}
