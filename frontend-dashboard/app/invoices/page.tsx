"use client"
import { useEffect, useMemo, useState } from 'react'
import { api, type Invoice } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select } from '@/components/ui/select'
import { Table, TBody, TD, TH, THead, TR } from '@/components/ui/table'
import { Skeleton } from '@/components/ui/skeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { downloadInvoicesPdf, downloadInvoicesXlsx } from '@/lib/export'

type Filters = {
  start?: string
  end?: string
  method: 'all' | 'havale' | 'kredikarti'
  q: string
}

export default function InvoicesPage() {
  const [data, setData] = useState<Invoice[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [filters, setFilters] = useState<Filters>({ method: 'all', q: '' })

  // Load from API (refetch when filters change to match spec, even if server ignores)
  useEffect(() => {
    let mounted = true
    async function load() {
      try {
        setError(null)
        const list = await api.listInvoices()
        if (!mounted) return
        setData(list)
      } catch {
        if (!mounted) return
        setError('Veri alınamadı')
        setData(null)
      }
    }
    load()
    return () => {
      mounted = false
    }
  }, [filters.start, filters.end, filters.method, filters.q])

  const filtered = useMemo(() => {
    const all = data || []
    return all.filter((x) => {
      if (filters.start && x.tarih < filters.start) return false
      if (filters.end && x.tarih > filters.end) return false
      if (filters.method === 'havale' && x.odemeSekli !== 0) return false
      if (filters.method === 'kredikarti' && x.odemeSekli !== 1) return false
      const q = filters.q.trim().toLowerCase()
      if (q) {
        const inName = (x.musteriAdSoyad || '').toLowerCase().includes(q)
        const inTckn = (x.tckn || '').toLowerCase().includes(q)
        if (!inName && !inTckn) return false
      }
      return true
    })
  }, [data, filters])

  const total = useMemo(() => filtered.reduce((a, b) => a + Number(b.tutar), 0), [filtered])

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle>Fatura Filtreleri</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-3 md:grid-cols-4">
            <div className="space-y-1">
              <Label htmlFor="start">Başlangıç Tarihi</Label>
              <Input id="start" type="date" value={filters.start || ''} onChange={(e) => setFilters((f) => ({ ...f, start: e.target.value || undefined }))} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="end">Bitiş Tarihi</Label>
              <Input id="end" type="date" value={filters.end || ''} onChange={(e) => setFilters((f) => ({ ...f, end: e.target.value || undefined }))} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="method">Ödeme Şekli</Label>
              <Select id="method" value={filters.method} onChange={(e) => setFilters((f) => ({ ...f, method: e.target.value as Filters['method'] }))}>
                <option value="all">Tümü</option>
                <option value="havale">Havale</option>
                <option value="kredikarti">Kredi Kartı</option>
              </Select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="q">Müşteri Adı / TCKN</Label>
              <Input id="q" placeholder="Ara" value={filters.q} onChange={(e) => setFilters((f) => ({ ...f, q: e.target.value }))} />
            </div>
          </div>
        </CardContent>
      </Card>

      <Card className="transition-all animate-in fade-in-50">
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Faturalar</CardTitle>
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => downloadInvoicesPdf(filtered, { start: filters.start, end: filters.end })}>PDF olarak indir</Button>
              <Button variant="outline" onClick={() => downloadInvoicesXlsx(filtered)}>Excel olarak indir</Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          {error ? (
            <p className="text-sm text-red-600">{error}</p>
          ) : !data ? (
            <div className="space-y-2">
              <Skeleton className="h-6 w-1/3" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-2/3" />
            </div>
          ) : (
            <>
              <Table>
                <THead>
                  <TR>
                    <TH>Tarih</TH>
                    <TH>Sıra No</TH>
                    <TH>Müşteri Ad Soyad</TH>
                    <TH>TCKN</TH>
                    <TH className="text-right">Tutar</TH>
                    <TH>Ödeme Şekli</TH>
                  </TR>
                </THead>
                <TBody>
                  {filtered.length === 0 ? (
                    <TR>
                      <TD colSpan={6} className="text-center text-sm text-muted-foreground">Kayıt bulunamadı</TD>
                    </TR>
                  ) : filtered.map((x) => (
                    <TR key={x.id}>
                      <TD>{x.tarih}</TD>
                      <TD>{x.siraNo}</TD>
                      <TD>{x.musteriAdSoyad || '-'}</TD>
                      <TD>{x.tckn || '-'}</TD>
                      <TD className="text-right tabular-nums">{Number(x.tutar).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</TD>
                      <TD>
                        {x.odemeSekli === 0 ? (
                          <Badge variant="success">Havale</Badge>
                        ) : (
                          <Badge variant="warning">Kredi Kartı</Badge>
                        )}
                      </TD>
                    </TR>
                  ))}
                </TBody>
              </Table>
              <div className="text-right font-semibold">Toplam: {total.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
