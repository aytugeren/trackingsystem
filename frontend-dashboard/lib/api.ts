export type Invoice = {
  id: string
  tarih: string // ISO or yyyy-MM-dd
  siraNo: number
  customerId?: string | null
  musteriAdSoyad?: string | null
  tckn?: string | null
  isForCompany?: boolean
  isCompany?: boolean
  vknNo?: string | null
  companyName?: string | null
  tutar: number
  // Backend enums are serialized as strings; old clients may have numbers
  odemeSekli: number | 'Havale' | 'KrediKarti'
  altinAyar?: number | 'Ayar22' | 'Ayar24'
  altinSatisFiyati?: number | null
  safAltinDegeri?: number | null
  urunFiyati?: number | null
  yeniUrunFiyati?: number | null
  gramDegeri?: number | null
  iscilik?: number | null
  finalizedAt?: string | null
  kesildi?: boolean
  kasiyerAdSoyad?: string | null
}

export type Expense = {
  id: string
  tarih: string
  siraNo: number
  customerId?: string | null
  musteriAdSoyad?: string | null
  tckn?: string | null
  isForCompany?: boolean
  isCompany?: boolean
  vknNo?: string | null
  companyName?: string | null
  tutar: number
  altinAyar?: number | 'Ayar22' | 'Ayar24'
  altinSatisFiyati?: number | null
  safAltinDegeri?: number | null
  urunFiyati?: number | null
  yeniUrunFiyati?: number | null
  gramDegeri?: number | null
  iscilik?: number | null
  finalizedAt?: string | null
  kesildi?: boolean
  kasiyerAdSoyad?: string | null
  odemeSekli: number | 'Havale' | 'KrediKarti'
}

export type DashboardSummary = {
  income: number
  outgo: number
  net: number
  invGrams: number
  expGrams: number
  karatRows: { ayar: number; inv: number; exp: number }[]
  availableYears: string[]
  availableMonths: string[]
  pendingInvoices: number
  pendingExpenses: number
}

export type Customer = {
  id: string
  adSoyad: string
  tckn: string
  isCompany?: boolean
  vknNo?: string | null
  companyName?: string | null
  phone?: string | null
  email?: string | null
  lastTransactionAt?: string | null
  createdAt?: string | null
  purchaseCount?: number
}

export type CustomerTransaction = {
  id: string
  type: 'invoice' | 'expense' | string
  tarih: string
  siraNo: number
  tutar: number
}

export type TurmobPreview = {
  action: string
  xml: string
}

export type TurmobSendResult = {
  status: 'Success' | 'Failed' | 'Skipped' | string
  errorMessage?: string | null
}

export type PreviewUpdatePayload = {
  tutar: number
  gramDegeri: number
  mode: 'tutar' | 'gram' | string
  altinAyar?: number | null
}

export type PreviewUpdateResult = {
  id: string
  tutar: number
  safAltinDegeri?: number | null
  urunFiyati?: number | null
  yeniUrunFiyati?: number | null
  gramDegeri?: number | null
  iscilik?: number | null
  altinAyar?: number | null
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
  dashboardSummary: (params?: { mode?: string; years?: string[]; months?: string[]; day?: string }) => {
    const search = new URLSearchParams()
    if (params?.mode) search.set('mode', params.mode)
    if (params?.years && params.years.length > 0) search.set('years', params.years.join(','))
    if (params?.months && params.months.length > 0) search.set('months', params.months.join(','))
    if (params?.day) search.set('day', params.day)
    const suffix = search.toString()
    return getJson<DashboardSummary>(`/api/dashboard/summary${suffix ? `?${suffix}` : ''}`)
  },
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
  async updateInvoicePreview(id: string, body: PreviewUpdatePayload): Promise<PreviewUpdateResult> {
    const url = `${API_BASE}/api/invoices/${id}/preview`
    const res = await fetch(url, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify(body) })
    if (!res.ok) throw new Error('Güncelleme başarısız')
    return res.json()
  },
  async setExpenseStatus(id: string, kesildi: boolean): Promise<void> {
    const url = `${API_BASE}/api/expenses/${id}/status`
    const res = await fetch(url, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify({ kesildi }) })
    if (!res.ok) throw new Error('Durum güncellenemedi')
  },
  async updateExpensePreview(id: string, body: PreviewUpdatePayload): Promise<PreviewUpdateResult> {
    const url = `${API_BASE}/api/expenses/${id}/preview`
    const res = await fetch(url, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify(body) })
    if (!res.ok) throw new Error('Güncelleme başarısız')
    return res.json()
  },
  async finalizeInvoice(id: string): Promise<void> {
    const url = `${API_BASE}/api/invoices/${id}/finalize`
    const res = await fetch(url, { method: 'POST', headers: { ...authHeaders() } })
    if (!res.ok) throw new Error('Fatura kesilemedi')
  },
  async finalizeExpense(id: string): Promise<void> {
    const url = `${API_BASE}/api/expenses/${id}/finalize`
    const res = await fetch(url, { method: 'POST', headers: { ...authHeaders() } })
    if (!res.ok) throw new Error('Gider kesilemedi')
  },
  async listCustomers(page = 1, pageSize = 20, q?: string): Promise<Paginated<Customer>> {
    const search = new URLSearchParams()
    search.set('page', String(page))
    search.set('pageSize', String(pageSize))
    if (q) search.set('q', q)
    const url = `${API_BASE}/api/customers?${search.toString()}`
    const res = await fetch(url, { headers: { ...authHeaders() }, cache: 'no-store' })
    if (!res.ok) throw new Error('Müşteriler alınamadı')
    return res.json()
  },
  async listCustomerTransactions(id: string, limit = 50): Promise<{ items: CustomerTransaction[] }> {
    const url = `${API_BASE}/api/customers/${id}/transactions?limit=${limit}`
    const res = await fetch(url, { headers: { ...authHeaders() }, cache: 'no-store' })
    if (!res.ok) throw new Error('Geçmiş işlemler alınamadı')
    return res.json()
  },
  async deleteInvoice(id: string): Promise<void> {
    const url = `${API_BASE}/api/invoices/${id}`
    const res = await fetch(url, { method: 'DELETE', headers: { ...authHeaders() } })
    if (!res.ok) throw new Error('Fatura silinemedi')
  },
  async deleteExpense(id: string): Promise<void> {
    const url = `${API_BASE}/api/expenses/${id}`
    const res = await fetch(url, { method: 'DELETE', headers: { ...authHeaders() } })
    if (!res.ok) throw new Error('Gider silinemedi')
  },
  async previewTurmobInvoice(id: string): Promise<TurmobPreview> {
    const url = `${API_BASE}/api/turmob/invoices/${id}/preview`
    const res = await fetch(url, { method: 'POST', headers: { ...authHeaders() } })
    if (!res.ok) {
      try {
        const data = await res.json()
        throw new Error(data?.error || 'XML oluşturulamadı')
      } catch {
        throw new Error('XML oluşturulamadı')
      }
    }
    return res.json()
  },
  async getTurmobStatus(): Promise<{ enabled: boolean; healthy: boolean }> {
    const url = `${API_BASE}/api/turmob/status`
    const res = await fetch(url, { headers: { ...authHeaders() }, cache: 'no-store' })
    if (!res.ok) throw new Error('TURMOB durumu alınamadı')
    return res.json()
  },
  async sendTurmobInvoice(id: string): Promise<TurmobSendResult> {
    const url = `${API_BASE}/api/turmob/invoices/${id}/send`
    const res = await fetch(url, { method: 'POST', headers: { ...authHeaders() } })
    if (!res.ok) throw new Error('Gönderme başarısız')
    return res.json()
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

// Leaves (admin)
export type Leave = {
  id: string
  from: string
  to: string
  fromTime?: string | null
  toTime?: string | null
  user: string
  reason?: string | null
  status: 'Pending' | 'Approved' | 'Rejected' | string
}

export async function listLeavesAdmin(params?: { from?: string; to?: string }): Promise<Leave[]> {
  const search = new URLSearchParams()
  if (params?.from) search.set('from', params.from)
  if (params?.to) search.set('to', params.to)
  const url = `${API_BASE}/api/leaves${search.toString() ? `?${search.toString()}` : ''}`
  const res = await fetch(url, { headers: { ...authHeaders() }, cache: 'no-store' })
  if (!res.ok) throw new Error('İzinler yüklenemedi')
  const data = await res.json()
  return (data.items as any[]) as Leave[]
}

export async function updateLeaveStatus(id: string, status: 'Pending' | 'Approved' | 'Rejected') {
  const url = `${API_BASE}/api/leaves/${id}/status`
  const res = await fetch(url, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify({ status }) })
  if (!res.ok) throw new Error('Durum güncellenemedi')
}

export type LeaveSummaryItem = { userId: string; email: string; usedDays: number; allowanceDays?: number | null; remainingDays: number }
export async function listLeaveSummary(year?: number): Promise<{ year: number; items: LeaveSummaryItem[] }> {
  const url = `${API_BASE}/api/leaves/summary${year ? `?year=${year}` : ''}`
  const res = await fetch(url, { headers: { ...authHeaders() }, cache: 'no-store' })
  if (!res.ok) throw new Error('Özet alınamadı')
  return res.json()
}

export async function setUserLeaveAllowance(userId: string, days: number) {
  const url = `${API_BASE}/api/users/${userId}/leave-allowance`
  const res = await fetch(url, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify({ days }) })
  if (!res.ok) throw new Error('İzin hakkı güncellenemedi')
}

export type UserWithPermissions = { id: string; email: string; role: string; canCancelInvoice: boolean; canAccessLeavesAdmin: boolean; canPrintLabels: boolean; leaveAllowanceDays?: number | null; workingDayHours?: number | null; assignedRoleId?: string | null; customRoleName?: string | null }
export async function listUsersWithPermissions(): Promise<UserWithPermissions[]> {
  const url = `${API_BASE}/api/users/permissions`
  const res = await fetch(url, { headers: { ...authHeaders() }, cache: 'no-store' })
  if (!res.ok) throw new Error('Kullanıcılar yüklenemedi')
  return res.json()
}

export async function updateUserPermissions(id: string, p: Partial<Pick<UserWithPermissions, 'canCancelInvoice' | 'canAccessLeavesAdmin' | 'leaveAllowanceDays'> & { workingDayHours: number }>): Promise<void> {
  const url = `${API_BASE}/api/users/${id}/permissions`
  const res = await fetch(url, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify(p) })
  if (!res.ok) throw new Error('Yetkiler güncellenemedi')
}

export function toDateOnlyString(d: Date): string {
  // format as yyyy-MM-dd to compare with DateOnly serialized strings
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

// Formats a date/time string to DD-MM-YYYY HH:mm for TR locale.
// Accepts ISO strings or yyyy-MM-dd. Falls back gracefully if invalid.
export function formatDateTimeTr(input?: string | null): string {
  if (!input) return ''
  try {
    const isDateOnly = /^\d{4}-\d{2}-\d{2}$/.test(input)
    const d = isDateOnly ? new Date(`${input}T00:00:00`) : new Date(input)
    if (isNaN(d.getTime())) return String(input)
    const dd = String(d.getDate()).padStart(2, '0')
    const mm = String(d.getMonth() + 1).padStart(2, '0')
    const yyyy = d.getFullYear()
    const HH = String(d.getHours()).padStart(2, '0')
    const min = String(d.getMinutes()).padStart(2, '0')
    return `${dd}-${mm}-${yyyy} ${HH}:${min}`
  } catch {
    return String(input)
  }
}






// Roles
export type RoleDef = {
  id: string
  name: string
  canCancelInvoice: boolean
  canToggleKesildi: boolean
  canAccessLeavesAdmin: boolean
  canManageSettings: boolean
  canManageCashier: boolean
  canManageKarat: boolean
  canUseInvoices: boolean
  canUseExpenses: boolean
  canViewReports: boolean
  canPrintLabels: boolean
  leaveAllowanceDays?: number | null
  workingDayHours?: number | null
}
export async function listRoles(): Promise<RoleDef[]> {
  const url = `${API_BASE}/api/roles`
  const res = await fetch(url, { headers: { ...authHeaders() }, cache: 'no-store' })
  if (!res.ok) throw new Error('Roller yüklenemedi')
  return res.json()
}
export async function createRole(body: { name: string; canCancelInvoice?: boolean; canToggleKesildi?: boolean; canAccessLeavesAdmin?: boolean; canManageSettings?: boolean; canManageCashier?: boolean; canManageKarat?: boolean; canUseInvoices?: boolean; canUseExpenses?: boolean; canViewReports?: boolean; canPrintLabels?: boolean; leaveAllowanceDays?: number | null; workingDayHours?: number | null }): Promise<void> {
  const url = `${API_BASE}/api/roles`
  const res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify(body) })
  if (!res.ok) throw new Error('Rol oluşturulamadı')
}
export async function updateRole(id: string, body: Partial<{ name: string; canCancelInvoice: boolean; canToggleKesildi: boolean; canAccessLeavesAdmin: boolean; canManageSettings: boolean; canManageCashier: boolean; canManageKarat: boolean; canUseInvoices: boolean; canUseExpenses: boolean; canViewReports: boolean; canPrintLabels: boolean; leaveAllowanceDays: number | null; workingDayHours: number | null }>): Promise<void> {
  const url = `${API_BASE}/api/roles/${id}`
  const res = await fetch(url, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify(body) })
  if (!res.ok) throw new Error('Rol güncellenemedi')
}
export async function deleteRole(id: string): Promise<void> {
  const url = `${API_BASE}/api/roles/${id}`
  const res = await fetch(url, { method: 'DELETE', headers: { ...authHeaders() } })
  if (!res.ok) throw new Error('Rol silinemedi')
}
export async function assignRole(userId: string, roleId: string | null): Promise<void> {
  const url = `${API_BASE}/api/users/${userId}/assign-role`
  const res = await fetch(url, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify({ roleId }) })
  if (!res.ok) throw new Error('Rol atanamadı')
}
