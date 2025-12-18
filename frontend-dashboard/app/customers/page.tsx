"use client"
import { useEffect, useMemo, useState } from 'react'
import { api, type Customer, type CustomerTransaction, formatDateTimeTr } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Table, TBody, TD, TH, THead, TR } from '@/components/ui/table'
import { Skeleton } from '@/components/ui/skeleton'
import { Badge } from '@/components/ui/badge'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'

export default function CustomersPage() {
  const [customers, setCustomers] = useState<Customer[] | null>(null)
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [q, setQ] = useState('')
  const [pendingQ, setPendingQ] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [historyOpen, setHistoryOpen] = useState(false)
  const [historyLoading, setHistoryLoading] = useState(false)
  const [history, setHistory] = useState<CustomerTransaction[] | null>(null)
  const [historyCustomer, setHistoryCustomer] = useState<Customer | null>(null)

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize])

  useEffect(() => {
    const t = setTimeout(() => { setQ(pendingQ.trim()) }, 300)
    return () => clearTimeout(t)
  }, [pendingQ])

  useEffect(() => {
    let mounted = true
    async function load() {
      setLoading(true)
      try {
        const resp = await api.listCustomers(page, pageSize, q || undefined)
        if (!mounted) return
        setCustomers(resp.items)
        setTotalCount(resp.totalCount)
        setError(null)
      } catch {
        if (!mounted) return
        setError('Müşteriler alınamadı')
        setCustomers(null)
      } finally {
        if (mounted) setLoading(false)
      }
    }
    load()
    return () => { mounted = false }
  }, [page, pageSize, q])

  function formatContact(c: Customer) {
    if (c.phone && c.email) return `${c.phone} · ${c.email}`
    if (c.phone) return c.phone
    if (c.email) return c.email
    return '—'
  }

  async function openHistory(c: Customer) {
    setHistoryCustomer(c)
    setHistoryOpen(true)
    setHistory(null)
    setHistoryLoading(true)
    try {
      const resp = await api.listCustomerTransactions(c.id, 80)
      setHistory(resp.items)
    } catch {
      setHistory([])
    } finally {
      setHistoryLoading(false)
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Müşteriler</h1>
        <div className="flex items-center gap-2">
          <Input
            placeholder="İsim veya TCKN ile ara"
            value={pendingQ}
            onChange={(e) => { setPendingQ(e.target.value); setPage(1) }}
            className="w-64"
          />
          <Button variant="outline" onClick={() => { setPendingQ(''); setQ(''); setPage(1) }}>Temizle</Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Toplam {totalCount}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {error && <div className="text-sm text-red-600">{error}</div>}
          {loading && !customers && (
            <div className="space-y-2">
              {[...Array(6)].map((_, i) => <Skeleton key={i} className="h-10 w-full" />)}
            </div>
          )}
          {!loading && customers && (
            <div className="overflow-auto rounded-md border">
              <Table>
                <THead>
                  <TR>
                    <TH>Ad Soyad</TH>
                    <TH>TCKN</TH>
                    <TH>İletişim</TH>
                    <TH>İşlem</TH>
                    <TH>Son İşlem</TH>
                    <TH>Kaydedildi</TH>
                  </TR>
                </THead>
                <TBody>
                  {customers.map((c) => (
                    <TR key={c.id}>
                      <TD className="font-semibold">{c.adSoyad || '—'}</TD>
                      <TD className="font-mono text-xs">{c.tckn || '—'}</TD>
                      <TD>
                        {c.phone || c.email ? (
                          <div className="flex flex-col gap-0.5 text-sm">
                            {c.phone && <span>{c.phone}</span>}
                            {c.email && <span className="text-muted-foreground">{c.email}</span>}
                          </div>
                        ) : (
                          <Badge variant="outline" className="text-amber-800 border-amber-200 bg-amber-50">Eksik</Badge>
                        )}
                      </TD>
                      <TD>
                        <Button variant="ghost" size="sm" onClick={() => openHistory(c)}>
                          {c.purchaseCount ?? 0}
                        </Button>
                      </TD>
                      <TD>{formatDateTimeTr(c.lastTransactionAt) || '—'}</TD>
                      <TD>{formatDateTimeTr(c.createdAt) || '—'}</TD>
                    </TR>
                  ))}
                  {customers.length === 0 && (
                    <TR><TD colSpan={5} className="text-center text-muted-foreground">Kayıt bulunamadı</TD></TR>
                  )}
                </TBody>
              </Table>
            </div>
          )}
          <div className="flex items-center justify-between">
            <div className="text-sm text-muted-foreground">Sayfa {page} / {totalPages}</div>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(p => Math.max(1, p - 1))}>Önceki</Button>
              <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(p => Math.min(totalPages, p + 1))}>Sonraki</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Dialog open={historyOpen} onOpenChange={setHistoryOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{historyCustomer ? `${historyCustomer.adSoyad} - Geçmiş İşlemler` : 'Geçmiş İşlemler'}</DialogTitle>
          </DialogHeader>
          {historyLoading && (
            <div className="space-y-2">
              {[...Array(4)].map((_, i) => <Skeleton key={i} className="h-8 w-full" />)}
            </div>
          )}
          {!historyLoading && history && (
            <div className="overflow-auto rounded-md border">
              <Table>
                <THead>
                  <TR>
                    <TH>Tür</TH>
                    <TH>Tarih</TH>
                    <TH>Sıra No</TH>
                    <TH>Tutar</TH>
                  </TR>
                </THead>
                <TBody>
                  {history.map((h) => (
                    <TR key={`${h.type}-${h.id}`}>
                      <TD className="capitalize">{h.type === 'invoice' ? 'Fatura' : 'Gider'}</TD>
                      <TD>{formatDateTimeTr(h.tarih)}</TD>
                      <TD className="font-mono text-xs">{h.siraNo}</TD>
                      <TD>{Number(h.tutar ?? 0).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</TD>
                    </TR>
                  ))}
                  {history.length === 0 && (
                    <TR><TD colSpan={4} className="text-center text-muted-foreground">Kayıt yok</TD></TR>
                  )}
                </TBody>
              </Table>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  )
}
