export type Invoice = {
  id: string
  tarih: string // ISO or yyyy-MM-dd
  siraNo: number
  musteriAdSoyad?: string | null
  tckn?: string | null
  tutar: number
  // Backend enums are serialized as strings; old clients may have numbers
  odemeSekli: number | 'Havale' | 'KrediKarti'
  altinSatisFiyati?: number | null
  kesildi?: boolean
  kasiyerAdSoyad?: string | null
}

export type Expense = {
  id: string
  tarih: string
  siraNo: number
  musteriAdSoyad?: string | null
  tckn?: string | null
  tutar: number
  kasiyerAdSoyad?: string | null
}

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || ''

function authHeaders(): HeadersInit {
  try {
    const token = localStorage.getItem('ktp_token') || (typeof document !== 'undefined' ? (document.cookie.split('; ').find(x => x.startsWith('ktp_token='))?.split('=')[1] || '') : '')
    return token ? { Authorization: `Bearer ${token}` } : {}
  } catch { return {} }
}

export type Paginated<T> = { items: T[]; totalCount: number }

async function getJson<T>(path: string): Promise<T> {
  const url = `${API_BASE}${path}`
  const res = await fetch(url, { cache: 'no-store', headers: { ...authHeaders() } })
  if (!res.ok) throw new Error('API error')
  return res.json()
}

export const api = {
  listInvoices: (page = 1, pageSize = 20) => getJson<Paginated<Invoice>>(`/api/invoices?page=${page}&pageSize=${pageSize}`),
  listExpenses: (page = 1, pageSize = 20) => getJson<Paginated<Expense>>(`/api/expenses?page=${page}&pageSize=${pageSize}`),
  async listAllInvoices(): Promise<Invoice[]> {
    const pageSize = 500
    let page = 1
    const acc: Invoice[] = []
    while (true) {
      const { items, totalCount } = await api.listInvoices(page, pageSize)
      acc.push(...items)
      if (acc.length >= totalCount) break
      page++
    }
    return acc
  },
  async listAllExpenses(): Promise<Expense[]> {
    const pageSize = 500
    let page = 1
    const acc: Expense[] = []
    while (true) {
      const { items, totalCount } = await api.listExpenses(page, pageSize)
      acc.push(...items)
      if (acc.length >= totalCount) break
      page++
    }
    return acc
  },
  async setInvoiceStatus(id: string, kesildi: boolean): Promise<void> {
    const url = `${API_BASE}/api/invoices/${id}/status`
    const res = await fetch(url, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify({ kesildi }) })
    if (!res.ok) throw new Error('Durum güncellenemedi')
  },
  async login(email: string, password: string): Promise<{ token: string; role: string; email: string }> {
    const url = `${API_BASE}/api/auth/login`
    const res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password }) })
    if (!res.ok) throw new Error('Giriş başarısız')
    return res.json()
  }
}

export const adminApi = {
  async listUsers(role?: string): Promise<Array<{ id: string; email: string; role: string }>> {
    const url = `${API_BASE}/api/users${role ? `?role=${encodeURIComponent(role)}` : ''}`
    const res = await fetch(url, { headers: { ...authHeaders() } })
    if (!res.ok) throw new Error('Kullanıcılar yüklenemedi')
    return res.json()
  },
  async createUser(email: string, password: string, role: string): Promise<{ id: string; email: string; role: string }> {
    const url = `${API_BASE}/api/users`
    const res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify({ email, password, role }) })
    if (!res.ok) throw new Error('Kullanıcı oluşturulamadı')
    return res.json()
  },
  async resetUserPassword(id: string, password: string): Promise<void> {
    const url = `${API_BASE}/api/users/${id}/password`
    const res = await fetch(url, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify({ password }) })
    if (!res.ok) throw new Error('Şifre güncellenemedi')
  },
}

export function toDateOnlyString(d: Date): string {
  // format as yyyy-MM-dd to compare with DateOnly serialized strings
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}
