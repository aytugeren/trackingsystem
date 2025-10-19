"use client"
import { useEffect, useMemo, useState } from 'react'
import { api, type Expense, type Invoice } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import {
  LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend,
  BarChart, Bar, ResponsiveContainer
} from 'recharts'

function toDateOnlyString(d: Date) {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

function isoWeek(dt: Date) {
  const d = new Date(Date.UTC(dt.getFullYear(), dt.getMonth(), dt.getDate()))
  const dayNum = d.getUTCDay() || 7
  d.setUTCDate(d.getUTCDate() + 4 - dayNum)
  const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1))
  const weekNo = Math.ceil((((d.getTime() - yearStart.getTime()) / 86400000) + 1) / 7)
  return { year: d.getUTCFullYear(), week: weekNo }
}

export default function ReportsPage() {
  const [invoices, setInvoices] = useState<Invoice[] | null>(null)
  const [expenses, setExpenses] = useState<Expense[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let mounted = true
    async function load() {
      try {
        setError(null)
        const [inv, exp] = await Promise.all([api.listAllInvoices(), api.listAllExpenses()])
        if (!mounted) return
        setInvoices(inv)
        setExpenses(exp)
      } catch {
        if (!mounted) return
        setError('Veri alınamadı')
      }
    }
    load()
    return () => { mounted = false }
  }, [])

  const last30 = useMemo(() => {
    const today = new Date()
    const days: { key: string, label: string }[] = []
    for (let i = 29; i >= 0; i--) {
      const d = new Date(today)
      d.setDate(today.getDate() - i)
      days.push({ key: toDateOnlyString(d), label: `${d.getDate()}.${d.getMonth()+1}` })
    }
    const invByDay = new Map<string, number>()
    const expByDay = new Map<string, number>()
    for (const key of days.map(d => d.key)) { invByDay.set(key, 0); expByDay.set(key, 0) }
    for (const x of invoices || []) invByDay.set(x.tarih.substring(0,10), (invByDay.get(x.tarih.substring(0,10)) || 0) + Number(x.tutar))
    for (const x of expenses || []) expByDay.set(x.tarih.substring(0,10), (expByDay.get(x.tarih.substring(0,10)) || 0) + Number(x.tutar))
    return days.map(d => ({ day: d.label, gelir: invByDay.get(d.key) || 0, gider: expByDay.get(d.key) || 0 }))
  }, [invoices, expenses])

  const weekly = useMemo(() => {
    const groups = new Map<string, { hafta: string, gelir: number, gider: number }>()
    for (const x of invoices || []) {
      const dt = new Date(x.tarih)
      const w = isoWeek(dt)
      const key = `${w.year}-W${String(w.week).padStart(2,'0')}`
      const g = groups.get(key) || { hafta: key, gelir: 0, gider: 0 }
      g.gelir += Number(x.tutar)
      groups.set(key, g)
    }
    for (const x of expenses || []) {
      const dt = new Date(x.tarih)
      const w = isoWeek(dt)
      const key = `${w.year}-W${String(w.week).padStart(2,'0')}`
      const g = groups.get(key) || { hafta: key, gelir: 0, gider: 0 }
      g.gider += Number(x.tutar)
      groups.set(key, g)
    }
    return Array.from(groups.values()).sort((a,b) => a.hafta.localeCompare(b.hafta))
  }, [invoices, expenses])

  const monthly = useMemo(() => {
    const monthNames = ['Oca','Şub','Mar','Nis','May','Haz','Tem','Ağu','Eyl','Eki','Kas','Ara']
    type Row = { ay: string, havale: number, kredikarti: number }
    const map = new Map<string, Row>()
    for (const inv of invoices || []) {
      const d = new Date(inv.tarih)
      const key = `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}`
      const label = `${monthNames[d.getMonth()]} ${String(d.getFullYear()).slice(-2)}`
      const row = map.get(key) || { ay: label, havale: 0, kredikarti: 0 }
      if (inv.odemeSekli === 0) row.havale += Number(inv.tutar); else row.kredikarti += Number(inv.tutar)
      map.set(key, row)
    }
    return Array.from(map.entries()).sort((a,b) => a[0].localeCompare(b[0])).map(x => x[1])
  }, [invoices])

  return (
    <div className="space-y-6">
      <section>
        <h1 className="text-2xl font-semibold">Raporlar</h1>
        <p className="text-sm text-muted-foreground">Özet grafikler ve kırılımlar</p>
      </section>

      <Card className="h-80">
        <CardHeader>
          <CardTitle>Son 30 Günlük Gelir/Gider</CardTitle>
        </CardHeader>
        <CardContent className="h-60">
          {!invoices || !expenses ? (
            <Skeleton className="h-60 w-full" />
          ) : (
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={last30} margin={{ left: 8, right: 8 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="day" />
                <YAxis />
                <Tooltip />
                <Legend />
                <Line type="monotone" dataKey="gelir" name="Toplam Gelir (Invoices)" stroke="#1f77b4" strokeWidth={2} dot={false} />
                <Line type="monotone" dataKey="gider" name="Toplam Gider (Expenses)" stroke="#d62728" strokeWidth={2} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          )}
        </CardContent>
      </Card>

      <Card className="h-80">
        <CardHeader>
          <CardTitle>Haftalık Toplam Gelir/Gider</CardTitle>
        </CardHeader>
        <CardContent className="h-60">
          {!invoices || !expenses ? (
            <Skeleton className="h-60 w-full" />
          ) : (
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={weekly} margin={{ left: 8, right: 8 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="hafta" />
                <YAxis />
                <Tooltip />
                <Legend />
                <Bar dataKey="gelir" name="Gelir" fill="#2ca02c" />
                <Bar dataKey="gider" name="Gider" fill="#ff7f0e" />
              </BarChart>
            </ResponsiveContainer>
          )}
        </CardContent>
      </Card>

      <Card className="h-80">
        <CardHeader>
          <CardTitle>Aylık Kırılım</CardTitle>
        </CardHeader>
        <CardContent className="h-60">
          {!invoices ? (
            <Skeleton className="h-60 w-full" />
          ) : (
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={monthly} margin={{ left: 8, right: 8 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="ay" />
                <YAxis />
                <Tooltip />
                <Legend />
                <Bar dataKey="havale" name="Havale" stackId="a" fill="#1f77b4" />
                <Bar dataKey="kredikarti" name="Kredi Kartı" stackId="a" fill="#2ca02c" />
              </BarChart>
            </ResponsiveContainer>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
