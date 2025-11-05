"use client"

import { useEffect, useMemo, useState } from "react"
import { authHeaders } from "../../lib/api"
import BackButton from "../../components/BackButton"

type Leave = {
  id: string
  from: string
  to: string
  fromTime?: string | null
  toTime?: string | null
  user: string
  reason?: string | null
  status: "Pending" | "Approved" | "Rejected" | string
}

function fmt(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`
}

function fmtTr(d: Date) {
  return d.toLocaleDateString("tr-TR", { timeZone: "Europe/Istanbul" })
}

export default function LeavePage() {
  const apiBase = useMemo(() => process.env.NEXT_PUBLIC_API_URL || "", [])

  const [from, setFrom] = useState<string>(fmt(new Date()))
  const [to, setTo] = useState<string>(fmt(new Date()))
  const [useTime, setUseTime] = useState(false)
  const [fromTime, setFromTime] = useState<string>("09:00")
  const [toTime, setToTime] = useState<string>("13:00")
  const [reason, setReason] = useState<string>("")
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState("")
  const [ok, setOk] = useState("")

  const [month, setMonth] = useState<Date>(new Date(new Date().getFullYear(), new Date().getMonth(), 1))
  const [leaves, setLeaves] = useState<Leave[]>([])
  const [lvLoading, setLvLoading] = useState(true)
  const [isNarrow, setIsNarrow] = useState(false)

  const today = useMemo(() => fmt(new Date()), [])

  useEffect(() => {
    const calc = () => setIsNarrow(typeof window !== "undefined" && window.innerWidth < 520)
    calc()
    window.addEventListener("resize", calc)
    return () => window.removeEventListener("resize", calc)
  }, [])

  async function loadLeaves(m: Date = month) {
    setLvLoading(true)
    try {
      const start = new Date(m.getFullYear(), m.getMonth(), 1)
      const end = new Date(m.getFullYear(), m.getMonth() + 1, 0)
      const url = `${apiBase}/api/leaves?from=${fmt(start)}&to=${fmt(end)}`
      const res = await fetch(url, { headers: { ...authHeaders() }, cache: "no-store" })
      if (!res.ok) throw new Error("İzinler alınamadı")
      const j = await res.json()
      setLeaves((j.items || []) as Leave[])
    } catch {
      // ignore
    } finally {
      setLvLoading(false)
    }
  }

  useEffect(() => {
    loadLeaves()
  }, [])

  async function submit() {
    setLoading(true)
    setError("")
    setOk("")
    try {
      if (!from || !to) throw new Error("Tarih gerekli")
      if (to < from) throw new Error("Bitiş tarihi başlangıçtan önce olamaz")
      if (useTime && from === to) {
        const [fh, fm] = fromTime.split(":").map((x) => parseInt(x, 10))
        const [th, tm] = toTime.split(":").map((x) => parseInt(x, 10))
        const fromMin = fh * 60 + fm
        const toMin = th * 60 + tm
        if (!(toMin > fromMin)) throw new Error("Geçersiz saat aralığı")
      }
      const body: any = { from, to, reason }
      if (useTime) {
        body.fromTime = fromTime
        body.toTime = toTime
      }
      const res = await fetch(`${apiBase}/api/leaves`, {
        method: "POST",
        headers: { "Content-Type": "application/json", ...authHeaders() },
        body: JSON.stringify(body),
      })
      if (!res.ok) throw new Error("Kaydedilemedi")
      setOk("Talep gönderildi")
      await loadLeaves()
    } catch (e: any) {
      setError(e.message || "Kaydedilemedi")
    } finally {
      setLoading(false)
    }
  }

  const titleMonth = month.toLocaleDateString("tr-TR", { month: "long", year: "numeric", timeZone: "Europe/Istanbul" })

  return (
    <main>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
        <h1>İzin İste</h1>
        <BackButton />
      </div>

      <div className="card" style={{ display: "flex", flexDirection: "column", gap: 12 }}>
        <div style={{ display: "grid", gridTemplateColumns: isNarrow ? "1fr" : "1fr 1fr", gap: 12 }}>
          <div>
            <label className="label">Başlangıç</label>
            <input
              type="date"
              value={from}
              min={today}
              onChange={(e) => {
                const v = e.target.value
                setFrom(v)
                setTo(v)
              }}
              style={{ width: "100%", fontSize: 16, padding: "10px 12px", height: 44 }}
            />
          </div>
          <div>
            <label className="label">Bitiş</label>
            <input
              type="date"
              value={to}
              min={from}
              onChange={(e) => setTo(e.target.value)}
              style={{ width: "100%", fontSize: 16, padding: "10px 12px", height: 44 }}
            />
          </div>
        </div>

        <div style={{ display: "flex", alignItems: "flex-start", gap: 12, flexWrap: "wrap" }}>
          <label htmlFor="useTime" style={{ display: "flex", alignItems: "center", gap: 8, cursor: "pointer" }}>
            <input
              id="useTime"
              type="checkbox"
              checked={useTime}
              onChange={(e) => {
                setUseTime(e.target.checked)
                if (e.target.checked) setTo(from)
              }}
            />
            <span>Saat seç (yarım gün / saatlik)</span>
          </label>

          {useTime && (
            <div style={{ display: "grid", gridTemplateColumns: isNarrow ? "1fr" : "1fr 1fr", gap: 12, width: "100%" }}>
              <div>
                <label className="label">Başlangıç Saati</label>
                <input
                  type="time"
                  value={fromTime}
                  onChange={(e) => {
                    const v = e.target.value
                    setFromTime(v)
                    if (from === to && toTime < v) setToTime(v)
                  }}
                  style={{ width: "100%", fontSize: 16, padding: "10px 12px", height: 44 }}
                />
              </div>
              <div>
                <label className="label">Bitiş Saati</label>
                <input
                  type="time"
                  value={toTime}
                  min={from === to ? fromTime : undefined}
                  onChange={(e) => setToTime(e.target.value)}
                  style={{ width: "100%", fontSize: 16, padding: "10px 12px", height: 44 }}
                />
              </div>
            </div>
          )}
        </div>

        <div>
          <label className="label">Açıklama</label>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={4}
            style={{ resize: "vertical", width: "100%", fontSize: 16, padding: "10px 12px", minHeight: 88 }}
            placeholder="Kısa açıklama"
          />
          <div className="actions">
            <button className="primary" onClick={submit} disabled={loading}>
              {loading ? "Gönderiliyor…" : "Gönder"}
            </button>
          </div>
          {ok && <div className="ok">{ok}</div>}
          {error && <div className="error">{error}</div>}
        </div>
      </div>

      <div className="card" style={{ marginTop: 12 }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <h2 style={{ margin: 0 }}>Takvim — {titleMonth}</h2>
          <div style={{ display: "flex", gap: 8 }}>
            <button
              className="secondary"
              onClick={() => {
                const m = new Date(month.getFullYear(), month.getMonth() - 1, 1)
                setMonth(m)
                loadLeaves(m)
              }}
            >
              ‹
            </button>
            <button
              className="secondary"
              onClick={() => {
                const m = new Date(month.getFullYear(), month.getMonth() + 1, 1)
                setMonth(m)
                loadLeaves(m)
              }}
            >
              ›
            </button>
          </div>
        </div>
        {lvLoading ? <p>Yükleniyor…</p> : <Calendar month={month} leaves={leaves} />}
      </div>

      <div className="card" style={{ marginTop: 12 }}>
        <h2>İzinler (Ay)</h2>
        <LeavesTable leaves={leaves} />
      </div>
    </main>
  )
}

function Calendar({ month, leaves }: { month: Date; leaves: Leave[] }) {
  const firstDay = new Date(month.getFullYear(), month.getMonth(), 1)
  const nextMonth = new Date(month.getFullYear(), month.getMonth() + 1, 1)
  const lastDay = new Date(nextMonth.getTime() - 86400000)
  const firstWeekday = (firstDay.getDay() + 6) % 7 // 0: Pazartesi
  const daysInMonth = lastDay.getDate()
  const totalCells = firstWeekday + daysInMonth
  const rows = Math.ceil(totalCells / 7)

  function color(d: Date): string | null {
    const has = leaves.filter((l) => new Date(l.from) <= d && d <= new Date(l.to))
    if (has.some((h) => h.status === "Approved")) return "#22c55e33"
    if (has.some((h) => h.status === "Pending")) return "#f59e0b33"
    if (has.some((h) => h.status === "Rejected")) return "#ef444433"
    return null
  }

  return (
    <div style={{ display: "grid", gridTemplateColumns: "repeat(7, 1fr)", gap: 6 }}>
      {Array.from({ length: rows * 7 }).map((_, i) => {
        const dayNum = i - firstWeekday + 1
        const inMonth = dayNum >= 1 && dayNum <= daysInMonth
        const date = inMonth ? new Date(month.getFullYear(), month.getMonth(), dayNum) : null
        const bg = date ? color(date) : undefined
        return (
          <div key={i} style={{ padding: 10, textAlign: "center", border: "1px solid #eee", borderRadius: 8, background: bg || "transparent" }}>
            {inMonth ? dayNum : ""}
          </div>
        )
      })}
    </div>
  )
}

function LeavesTable({ leaves }: { leaves: Leave[] }) {
  const [detail, setDetail] = useState<Leave | null>(null)
  if (!leaves || leaves.length === 0) return <p>Bu ay için izin bulunmuyor</p>

  const rangeText = (l: Leave) => {
    const same = l.from === l.to
    const base = `${fmtTr(new Date(l.from))}${same ? "" : " - " + fmtTr(new Date(l.to))}`
    const times = l.fromTime && l.toTime ? ` (${l.fromTime}-${l.toTime})` : ""
    return base + (same ? times : "")
  }

  const statusTr = (s: string) => (s === "Approved" ? "Onaylandı" : s === "Rejected" ? "Reddedildi" : "Bekliyor")

  return (
    <>
      <div style={{ overflowX: "auto" }}>
        <table className="table" style={{ minWidth: 360, width: "100%" }}>
          <thead>
            <tr>
              <th>İzin Aralığı</th>
              <th>İzin İsteyen Kişi</th>
              <th>Onay Durumu</th>
            </tr>
          </thead>
          <tbody>
            {leaves.map((l) => (
              <tr key={l.id} onClick={() => setDetail(l)} style={{ cursor: "pointer" }}>
                <td>{rangeText(l)}</td>
                <td>{l.user}</td>
                <td>{statusTr(l.status)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {detail && (
        <div onClick={() => setDetail(null)} style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.35)", display: "flex", alignItems: "center", justifyContent: "center", padding: 16 }}>
          <div onClick={(e) => e.stopPropagation()} style={{ background: "#fff", borderRadius: 12, padding: 16, maxWidth: 420, width: "100%", boxShadow: "0 8px 30px rgba(0,0,0,0.15)" }}>
            <h3 style={{ marginTop: 0, marginBottom: 8 }}>İzin Detayı</h3>
            <div style={{ lineHeight: 1.6 }}>
              <div>
                <b>Aralık:</b> {rangeText(detail)}
              </div>
              <div>
                <b>Kişi:</b> {detail.user}
              </div>
              <div>
                <b>Durum:</b> {statusTr(detail.status)}
              </div>
              <div>
                <b>Açıklama:</b> {detail.reason || "-"}
              </div>
            </div>
            <div style={{ display: "flex", justifyContent: "flex-end", marginTop: 12 }}>
              <button className="secondary" onClick={() => setDetail(null)}>
                Kapat
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
