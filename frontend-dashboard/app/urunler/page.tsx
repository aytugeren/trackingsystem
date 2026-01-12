"use client"
import { useEffect, useState, type ChangeEvent } from 'react'
import { api, type Product } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

function normalizeAccountingType(value: number | string | null | undefined) {
  if (typeof value === 'string') return value.toLowerCase() === 'adet' ? 1 : 0
  return value === 1 ? 1 : 0
}

function accountingTypeLabel(value: number | string | null | undefined) {
  return normalizeAccountingType(value) === 1 ? 'Adet' : 'Gram'
}

export default function ProductsPage() {
  const [products, setProducts] = useState<Product[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [allowed, setAllowed] = useState(false)

  async function load() {
    try {
      setError('')
      setLoading(true)
      const rows = await api.listProducts()
      setProducts(rows)
    } catch {
      setError('Ürünler yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
    ;(async () => {
      try {
        const base = process.env.NEXT_PUBLIC_API_BASE || ''
        const token = typeof window !== 'undefined' ? (localStorage.getItem('ktp_token') || '') : ''
        const res = await fetch(`${base}/api/me/permissions`, { cache: 'no-store', headers: token ? { Authorization: `Bearer ${token}` } : {} })
        if (res.ok) {
          const j = await res.json()
          setAllowed(String(j?.role) === 'Yonetici')
        }
      } catch {}
    })()
  }, [])

  if (!allowed) return <p className="text-sm text-muted-foreground">Bu sayfa için yetkiniz yok.</p>

  return (
    <Card>
      <CardHeader>
        <CardTitle>Ürün Yönetimi</CardTitle>
      </CardHeader>
      <CardContent>
        {error && <p className="text-red-600 mb-2">{error}</p>}
        {loading ? (
          <p>Yükleniyor…</p>
        ) : (
          <div className="space-y-4">
            <NewProductForm onCreated={load} />
            {products.length === 0 ? (
              <p className="text-sm text-muted-foreground">Kayıtlı ürün yok.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="min-w-[980px] w-full text-sm">
                  <thead>
                    <tr className="text-left">
                      <th className="p-2">Kod</th>
                      <th className="p-2">Ad</th>
                      <th className="p-2">Aktif</th>
                      <th className="p-2">Satışta</th>
                      <th className="p-2">Tip</th>
                      <th className="p-2">Gram</th>
                      <th className="p-2">İşlem</th>
                    </tr>
                  </thead>
                  <tbody>
                    {products.map((p) => (
                      <ProductRow
                        key={p.id}
                        product={p}
                        onChange={async (patch) => { await api.updateProduct(p.id, patch); await load() }}
                        onDelete={async () => { await api.deleteProduct(p.id); await load() }}
                      />
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function NewProductForm({ onCreated }: { onCreated: () => Promise<void> }) {
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [isActive, setIsActive] = useState(true)
  const [showInSales, setShowInSales] = useState(true)
  const [accountingType, setAccountingType] = useState(0)
  const [gram, setGram] = useState<number | ''>('')
  const [busy, setBusy] = useState(false)

  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="flex flex-col">
        <label className="text-sm text-muted-foreground">Kod</label>
        <input value={code} onChange={(e: ChangeEvent<HTMLInputElement>) => setCode(e.target.value)} className="border rounded px-3 py-2 w-40 text-slate-900" placeholder="Örn: 22" />
      </div>
      <div className="flex flex-col">
        <label className="text-sm text-muted-foreground">Ad</label>
        <input value={name} onChange={(e: ChangeEvent<HTMLInputElement>) => setName(e.target.value)} className="border rounded px-3 py-2 w-56 text-slate-900" placeholder="Ürün adı" />
      </div>
      <label className="flex items-center gap-2">
        <input type="checkbox" checked={isActive} onChange={(e: ChangeEvent<HTMLInputElement>) => setIsActive(e.target.checked)} />
        Aktif
      </label>
      <label className="flex items-center gap-2">
        <input type="checkbox" checked={showInSales} onChange={(e: ChangeEvent<HTMLInputElement>) => setShowInSales(e.target.checked)} />
        Satışta Göster
      </label>
      <div className="flex flex-col">
        <label className="text-sm text-muted-foreground">Tip</label>
        <select value={accountingType} onChange={(e) => setAccountingType(parseInt(e.target.value, 10))} className="border rounded px-3 py-2 w-28 text-slate-900">
          <option value={0}>Gram</option>
          <option value={1}>Adet</option>
        </select>
      </div>
      <div className="flex flex-col">
        <label className="text-sm text-muted-foreground">Gram</label>
        <input type="number" step="0.001" value={gram} onChange={(e: ChangeEvent<HTMLInputElement>) => setGram(e.target.value === '' ? '' : parseFloat(e.target.value))} className="border rounded px-3 py-2 w-28 text-slate-900" placeholder="Opsiyonel" />
      </div>
      <Button
        disabled={busy || !code.trim() || !name.trim()}
        onClick={async () => {
          setBusy(true)
          await api.createProduct({
            code: code.trim(),
            name: name.trim(),
            isActive,
            showInSales,
            accountingType,
            gram: gram === '' ? null : gram,
          })
          setCode('')
          setName('')
          setIsActive(true)
          setShowInSales(true)
          setAccountingType(0)
          setGram('')
          await onCreated()
          setBusy(false)
        }}
      >
        Ekle
      </Button>
    </div>
  )
}

function ProductRow({ product, onChange, onDelete }: { product: Product; onChange: (patch: { code: string; name: string; isActive?: boolean | null; showInSales?: boolean | null; accountingType?: number | null; gram?: number | null }) => Promise<void>; onDelete: () => Promise<void> }) {
  const [code, setCode] = useState(product.code)
  const [name, setName] = useState(product.name)
  const [isActive, setIsActive] = useState(product.isActive)
  const [showInSales, setShowInSales] = useState(product.showInSales)
  const [accountingType, setAccountingType] = useState(normalizeAccountingType(product.accountingType))
  const [gram, setGram] = useState<number | ''>(product.gram ?? '')
  const [busy, setBusy] = useState(false)

  return (
    <tr className="border-t">
      <td className="p-2"><input value={code} onChange={(e: ChangeEvent<HTMLInputElement>) => setCode(e.target.value)} className="border rounded px-2 py-1 w-32 text-slate-900" /></td>
      <td className="p-2"><input value={name} onChange={(e: ChangeEvent<HTMLInputElement>) => setName(e.target.value)} className="border rounded px-2 py-1 w-56 text-slate-900" /></td>
      <td className="p-2"><input type="checkbox" checked={isActive} onChange={(e: ChangeEvent<HTMLInputElement>) => setIsActive(e.target.checked)} /></td>
      <td className="p-2"><input type="checkbox" checked={showInSales} onChange={(e: ChangeEvent<HTMLInputElement>) => setShowInSales(e.target.checked)} /></td>
      <td className="p-2">
        <select value={accountingType} onChange={(e) => setAccountingType(parseInt(e.target.value, 10))} className="border rounded px-2 py-1 w-28 text-slate-900">
          <option value={0}>Gram</option>
          <option value={1}>Adet</option>
        </select>
        <div className="text-xs text-muted-foreground mt-1">{accountingTypeLabel(accountingType)}</div>
      </td>
      <td className="p-2">
        <input type="number" step="0.001" value={gram} onChange={(e: ChangeEvent<HTMLInputElement>) => setGram(e.target.value === '' ? '' : parseFloat(e.target.value))} className="border rounded px-2 py-1 w-28 text-slate-900" />
      </td>
      <td className="p-2 flex gap-2">
        <Button size="sm" disabled={busy} onClick={async () => { setBusy(true); await onChange({ code: code.trim(), name: name.trim(), isActive, showInSales, accountingType, gram: gram === '' ? null : gram }); setBusy(false) }}>Kaydet</Button>
        <Button size="sm" variant="outline" disabled={busy} onClick={async () => { setBusy(true); await onDelete(); setBusy(false) }}>Sil</Button>
      </td>
    </tr>
  )
}
