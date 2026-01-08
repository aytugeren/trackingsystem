"use client"
import { useEffect, useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

type CompanyInfo = {
  companyName: string
  taxNo: string
  address: string
  tradeRegistryNo: string
  phone: string
  email: string
  cityName: string
  townName: string
  postalCode: string
  taxOfficeName: string
}

function authHeaders(): HeadersInit {
  try {
    const token = localStorage.getItem('ktp_token') || ''
    return token ? { Authorization: `Bearer ${token}` } : {}
  } catch { return {} }
}

async function getCompanyInfo(): Promise<CompanyInfo> {
  const base = process.env.NEXT_PUBLIC_API_BASE || ''
  const res = await fetch(`${base}/api/company-info`, { cache: 'no-store', headers: authHeaders() })
  if (!res.ok) throw new Error('Firma bilgileri alınamadı')
  const j = await res.json()
  return {
    companyName: j.companyName ?? '',
    taxNo: j.taxNo ?? '',
    address: j.address ?? '',
    tradeRegistryNo: j.tradeRegistryNo ?? '',
    phone: j.phone ?? '',
    email: j.email ?? '',
    cityName: j.cityName ?? '',
    townName: j.townName ?? '',
    postalCode: j.postalCode ?? '',
    taxOfficeName: j.taxOfficeName ?? ''
  }
}

async function saveCompanyInfo(info: CompanyInfo): Promise<void> {
  const base = process.env.NEXT_PUBLIC_API_BASE || ''
  const res = await fetch(`${base}/api/company-info`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify(info)
  })
  if (!res.ok) throw new Error('Firma bilgileri kaydedilemedi')
}

export default function CompanyInfoPage() {
  const [perms, setPerms] = useState<{ canManageSettings?: boolean } | null>(null)
  const [info, setInfo] = useState<CompanyInfo>({
    companyName: '',
    taxNo: '',
    address: '',
    tradeRegistryNo: '',
    phone: '',
    email: '',
    cityName: '',
    townName: '',
    postalCode: '',
    taxOfficeName: ''
  })
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    ;(async () => {
      try {
        const base = process.env.NEXT_PUBLIC_API_BASE || ''
        const token = typeof window !== 'undefined' ? (localStorage.getItem('ktp_token') || '') : ''
        const res = await fetch(`${base}/api/me/permissions`, { cache: 'no-store', headers: token ? { Authorization: `Bearer ${token}` } : {} })
        if (res.ok) setPerms(await res.json())
      } catch {}
    })()
    ;(async () => {
      try {
        setError('')
        setLoading(true)
        setInfo(await getCompanyInfo())
      } catch { setError('Yüklenemedi') } finally { setLoading(false) }
    })()
  }, [])

  if (!perms?.canManageSettings) return <p className="text-sm text-muted-foreground">Bu sayfa için yetkiniz yok.</p>

  return (
    <Card>
      <CardHeader>
        <CardTitle>Firma Bilgileri</CardTitle>
      </CardHeader>
      <CardContent>
        {error && <p className="text-red-600 mb-2">{error}</p>}
        {loading ? <p>Yükleniyor…</p> : (
          <div className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="companyName">Firma Ünvanı</Label>
                <Input id="companyName" value={info.companyName} onChange={e => setInfo({ ...info, companyName: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="taxNo">VKN</Label>
                <Input id="taxNo" value={info.taxNo} onChange={e => setInfo({ ...info, taxNo: e.target.value })} />
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label htmlFor="address">Adres</Label>
                <textarea
                  id="address"
                  rows={3}
                  value={info.address}
                  onChange={e => setInfo({ ...info, address: e.target.value })}
                  className="flex w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="tradeRegistryNo">Ticaret Sicil No</Label>
                <Input id="tradeRegistryNo" value={info.tradeRegistryNo} onChange={e => setInfo({ ...info, tradeRegistryNo: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="phone">Telefon</Label>
                <Input id="phone" value={info.phone} onChange={e => setInfo({ ...info, phone: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="email">Email</Label>
                <Input id="email" type="email" value={info.email} onChange={e => setInfo({ ...info, email: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="cityName">İl</Label>
                <Input id="cityName" value={info.cityName} onChange={e => setInfo({ ...info, cityName: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="townName">İlçe</Label>
                <Input id="townName" value={info.townName} onChange={e => setInfo({ ...info, townName: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="postalCode">Posta Kodu</Label>
                <Input id="postalCode" value={info.postalCode} onChange={e => setInfo({ ...info, postalCode: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="taxOfficeName">Vergi Dairesi</Label>
                <Input id="taxOfficeName" value={info.taxOfficeName} onChange={e => setInfo({ ...info, taxOfficeName: e.target.value })} />
              </div>
            </div>
            <Button onClick={async () => { await saveCompanyInfo(info); if (typeof window !== 'undefined') alert('Firma bilgileri kaydedildi') }}>
              Kaydet
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
