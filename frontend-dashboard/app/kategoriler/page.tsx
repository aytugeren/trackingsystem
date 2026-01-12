"use client"
import { useEffect, useState } from 'react'
import { api, type Category } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { CategoryManagement } from '@/components/categories/category-management'

export default function CategoriesPage() {
  const [categories, setCategories] = useState<Category[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [allowed, setAllowed] = useState(false)

  async function load() {
    try {
      setError('')
      setLoading(true)
      const rows = await api.listCategories()
      setCategories(rows)
    } catch {
      setError('Kategoriler yüklenemedi.')
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
        <CardTitle>Kategori Yönetimi</CardTitle>
      </CardHeader>
      <CardContent>
        {error && <p className="text-red-600 mb-2">{error}</p>}
        {loading ? (
          <p>Yükleniyor…</p>
        ) : (
          <CategoryManagement categories={categories} onRefresh={load} />
        )}
      </CardContent>
    </Card>
  )
}
