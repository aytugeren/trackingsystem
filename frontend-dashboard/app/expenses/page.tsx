"use client"
import { useEffect, useMemo, useState } from 'react'
import { api, type Expense } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Table, TBody, TD, TH, THead, TR } from '@/components/ui/table'
import { Skeleton } from '@/components/ui/skeleton'
import { Button } from '@/components/ui/button'
import { downloadExpensesPdf, downloadExpensesXlsx } from '@/lib/export'

type Filters = {
  start?: string
  end?: string
  q: string
}

export default function ExpensesPage() {
  const [data, setData] = useState<Expense[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [filters, setFilters] = useState<Filters>({ q: '' })
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [totalCount, setTotalCount] = useState(0)
  const [enterTick, setEnterTick] = useState(0)

  useEffect(() => {
    let mounted = true
    async function load() {
      try {
        setError(null)
        const resp = await api.listExpenses(page, pageSize)
        if (!mounted) return
        setData(resp.items)
        setTotalCount(resp.totalCount)
      } catch {
        if (!mounted) return
        setError('Veri alınamadı')
        setData(null)
      }
    }
    load()
    return () => { mounted = false }
  }, [filters.start, filters.end, filters.q, page, pageSize, enterTick])

  const filtered = useMemo(() => {
    const all = data || []
    return all.filter((x) => {
      if (filters.start && x.tarih < filters.start) return false
      if (filters.end && x.tarih > filters.end) return false
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
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle>Gider Filtreleri</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-3 md:grid-cols-3">
            <div className="space-y-1">
              <Label htmlFor="start">Başlangıç Tarihi</Label>
              <Input id="start" type="date" value={filters.start || ''} onChange={(e) => setFilters((f) => ({ ...f, start: e.target.value || undefined }))} onKeyDown={(e) => { if (e.key === 'Enter') setEnterTick((t) => t + 1) }} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="end">Bitiş Tarihi</Label>
              <Input id="end" type="date" value={filters.end || ''} onChange={(e) => setFilters((f) => ({ ...f, end: e.target.value || undefined }))} onKeyDown={(e) => { if (e.key === 'Enter') setEnterTick((t) => t + 1) }} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="q">Müşteri Adı / TCKN</Label>
              <Input id="q" placeholder="Ara" value={filters.q} onChange={(e) => setFilters((f) => ({ ...f, q: e.target.value }))} onKeyDown={(e) => { if (e.key === 'Enter') setEnterTick((t) => t + 1) }} />
            </div>
          </div>
        </CardContent>
      </Card>

      <Card className="transition-all animate-in fade-in-50">
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Giderler</CardTitle>
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => downloadExpensesPdf(filtered, { start: filters.start, end: filters.end })}>PDF olarak indir</Button>
              <Button variant="outline" onClick={() => downloadExpensesXlsx(filtered)}>Excel olarak indir</Button>
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
                    <TH>Kasiyer</TH>
                    <TH className="text-right">Tutar</TH>
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
                      <TD>{(x as any).kasiyerAdSoyad || '-'}</TD>
                      <TD className="text-right tabular-nums">{Number(x.tutar).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</TD>
                    </TR>
                  ))}
                </TBody>
              </Table>
              <div className="flex items-center justify-between">
                <div className="space-x-2">
                  <Button variant="outline" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>← Önceki</Button>
                  {Array.from({ length: Math.min(3, Math.max(1, Math.ceil(totalCount / pageSize))) }).map((_, idx) => {
                    const pNo = idx + 1
                    return <Button key={pNo} variant={pNo === page ? 'default' : 'outline'} onClick={() => setPage(pNo)}>{pNo}</Button>
                  })}
                  <Button variant="outline" disabled={page >= Math.ceil(totalCount / pageSize)} onClick={() => setPage((p) => p + 1)}>Sonraki →</Button>
                </div>
                <div className="text-right font-semibold">Toplam: {total.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</div>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
