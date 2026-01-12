"use client"
import { useEffect, useMemo, useState, type ChangeEvent } from 'react'
import { api, type Category, type CategoryProduct, type Product } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

function buildCategoryPath(category: Category, map: Map<string, Category>) {
  const parts = [category.name]
  let current = category.parentId ? map.get(category.parentId) : null
  let guard = 0
  while (current && guard < 25) {
    parts.push(current.name)
    current = current.parentId ? map.get(current.parentId) : null
    guard++
  }
  return parts.reverse().join(' > ')
}

export default function CategoryProductPage() {
  const [categories, setCategories] = useState<Category[]>([])
  const [products, setProducts] = useState<Product[]>([])
  const [links, setLinks] = useState<CategoryProduct[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [allowed, setAllowed] = useState(false)

  const categoryMap = useMemo(() => new Map(categories.map((c) => [c.id, c])), [categories])
  const categoryOptions = useMemo(() => {
    return [...categories]
      .map((c) => ({ id: c.id, label: buildCategoryPath(c, categoryMap) }))
      .sort((a, b) => a.label.localeCompare(b.label, 'tr-TR'))
  }, [categories, categoryMap])

  const productOptions = useMemo(() => {
    return [...products]
      .map((p) => ({ id: p.id, label: `${p.code} - ${p.name}` }))
      .sort((a, b) => a.label.localeCompare(b.label, 'tr-TR'))
  }, [products])

  async function loadAll() {
    try {
      setError('')
      setLoading(true)
      const [cats, prods, mappings] = await Promise.all([
        api.listCategories(),
        api.listProducts(),
        api.listCategoryProducts(),
      ])
      setCategories(cats)
      setProducts(prods)
      setLinks(mappings)
    } catch {
      setError('Kategori-Ürün verileri yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadAll()
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
        <CardTitle>Kategori Ürün Tanımlama</CardTitle>
      </CardHeader>
      <CardContent>
        {error && <p className="text-red-600 mb-2">{error}</p>}
        {loading ? (
          <p>Yükleniyor…</p>
        ) : (
          <div className="space-y-4">
            <NewCategoryProductForm
              categories={categoryOptions}
              products={productOptions}
              onCreated={loadAll}
            />
            {links.length === 0 ? (
              <p className="text-sm text-muted-foreground">Henüz eşleşme yok.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="min-w-[800px] w-full text-sm">
                  <thead>
                    <tr className="text-left">
                      <th className="p-2">Kategori</th>
                      <th className="p-2">Ürün</th>
                      <th className="p-2">İşlem</th>
                    </tr>
                  </thead>
                  <tbody>
                    {links.map((link) => (
                      <CategoryProductRow
                        key={link.id}
                        link={link}
                        categories={categoryOptions}
                        products={productOptions}
                        onChange={async (patch) => { await api.updateCategoryProduct(link.id, patch); await loadAll() }}
                        onDelete={async () => { await api.deleteCategoryProduct(link.id); await loadAll() }}
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

function NewCategoryProductForm({
  categories,
  products,
  onCreated,
}: {
  categories: { id: string; label: string }[]
  products: { id: string; label: string }[]
  onCreated: () => Promise<void>
}) {
  const [categoryId, setCategoryId] = useState('')
  const [productId, setProductId] = useState('')
  const [busy, setBusy] = useState(false)

  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="flex flex-col">
        <label className="text-sm text-muted-foreground">Kategori</label>
        <select value={categoryId} onChange={(e: ChangeEvent<HTMLSelectElement>) => setCategoryId(e.target.value)} className="border rounded px-3 py-2 w-64 text-slate-900">
          <option value="">Seçin</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>{c.label}</option>
          ))}
        </select>
      </div>
      <div className="flex flex-col">
        <label className="text-sm text-muted-foreground">Ürün</label>
        <select value={productId} onChange={(e: ChangeEvent<HTMLSelectElement>) => setProductId(e.target.value)} className="border rounded px-3 py-2 w-64 text-slate-900">
          <option value="">Seçin</option>
          {products.map((p) => (
            <option key={p.id} value={p.id}>{p.label}</option>
          ))}
        </select>
      </div>
      <Button
        disabled={busy || !categoryId || !productId}
        onClick={async () => {
          setBusy(true)
          await api.createCategoryProduct({ categoryId, productId })
          setCategoryId('')
          setProductId('')
          await onCreated()
          setBusy(false)
        }}
      >
        Ekle
      </Button>
    </div>
  )
}

function CategoryProductRow({
  link,
  categories,
  products,
  onChange,
  onDelete,
}: {
  link: CategoryProduct
  categories: { id: string; label: string }[]
  products: { id: string; label: string }[]
  onChange: (patch: { categoryId: string; productId: string }) => Promise<void>
  onDelete: () => Promise<void>
}) {
  const [categoryId, setCategoryId] = useState(link.categoryId)
  const [productId, setProductId] = useState(link.productId)
  const [busy, setBusy] = useState(false)

  return (
    <tr className="border-t">
      <td className="p-2">
        <select value={categoryId} onChange={(e: ChangeEvent<HTMLSelectElement>) => setCategoryId(e.target.value)} className="border rounded px-2 py-1 w-64 text-slate-900">
          {categories.map((c) => (
            <option key={c.id} value={c.id}>{c.label}</option>
          ))}
        </select>
      </td>
      <td className="p-2">
        <select value={productId} onChange={(e: ChangeEvent<HTMLSelectElement>) => setProductId(e.target.value)} className="border rounded px-2 py-1 w-64 text-slate-900">
          {products.map((p) => (
            <option key={p.id} value={p.id}>{p.label}</option>
          ))}
        </select>
      </td>
      <td className="p-2 flex gap-2">
        <Button size="sm" disabled={busy} onClick={async () => { setBusy(true); await onChange({ categoryId, productId }); setBusy(false) }}>Kaydet</Button>
        <Button size="sm" variant="outline" disabled={busy} onClick={async () => { setBusy(true); await onDelete(); setBusy(false) }}>Sil</Button>
      </td>
    </tr>
  )
}
