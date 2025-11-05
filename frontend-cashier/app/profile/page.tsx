"use client"

import { useEffect, useMemo, useState } from "react"
import BackButton from "../../components/BackButton"
import { authHeaders } from "../../lib/api"

type Profile = {
  id?: string
  email?: string
  role?: string
  allowanceDays: number
  usedDays: number
  remainingDays: number
}

type Leave = {
  id: string
  from: string
  to: string
  fromTime?: string | null
  toTime?: string | null
  user: string
  reason?: string | null
  status: string
}

function fmtTr(d: Date) {
  return d.toLocaleDateString("tr-TR", { timeZone: "Europe/Istanbul" })
}

export default function ProfilePage() {
  const apiBase = useMemo(() => process.env.NEXT_PUBLIC_API_URL || "", [])
  const [profile, setProfile] = useState<Profile | null>(null)
  const [leaves, setLeaves] = useState<Leave[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")

  useEffect(() => {
    async function load() {
      setLoading(true)
      setError("")
      try {
        // Fetch current user profile + leave summary
        const meRes = await fetch(`${apiBase}/api/me`, { headers: { ...authHeaders() }, cache: "no-store" })
        if (!meRes.ok) throw new Error("Profil bulunamadı!")
        const me = (await meRes.json()) as Profile
        setProfile(me)

        // Fetch current year's leaves and filter by user email
        const y = new Date().getFullYear()
        const from = `${y}-01-01`
        const to = `${y}-12-31`
        const lvRes = await fetch(`${apiBase}/api/leaves?from=${from}&to=${to}`, { headers: { ...authHeaders() }, cache: "no-store" })
        if (lvRes.ok) {
          const j = await lvRes.json()
          const all = (j.items || []) as Leave[]
          const mine = me.email ? all.filter((l) => l.user === me.email) : []
          setLeaves(mine)
        }
      } catch (e: any) {
        setError(e.message || "Yüklerken bir hata oluştu!")
      } finally {
        setLoading(false)
      }
    }
    load()
  }, [apiBase])

  return (
    <main>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12 }}>
        <h1>Profilim</h1>
        <BackButton />
      </div>

      {loading ? (
        <p>Yükleniyor...</p>
      ) : error ? (
        <p className="error">{error}</p>
      ) : (
        <>
          <div className="card" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
            <div>
              <div className="label">Email</div>
              <div style={{ padding: "10px 12px", border: "1px solid #eee", borderRadius: 8 }}>{profile?.email || "-"}</div>
            </div>
            <div>
              <div className="label">Rol</div>
              <div style={{ padding: "10px 12px", border: "1px solid #eee", borderRadius: 8 }}>{profile?.role || "-"}</div>
            </div>
            <div>
              <div className="label">Yıllık İzin Hakkında (Gün)</div>
              <div style={{ padding: "10px 12px", border: "1px solid #eee", borderRadius: 8 }}>{profile?.allowanceDays ?? "-"}</div>
            </div>
            <div>
              <div className="label">Kullanılan (gün)</div>
              <div style={{ padding: "10px 12px", border: "1px solid #eee", borderRadius: 8 }}>{profile?.usedDays ?? "-"}</div>
            </div>
            <div>
              <div className="label">Kalan (gün)</div>
              <div style={{ padding: "10px 12px", border: "1px solid #eee", borderRadius: 8 }}>{profile?.remainingDays ?? "-"}</div>
            </div>
          </div>

          <div className="card" style={{ marginTop: 12 }}>
            <h2>İzinlerim</h2>
            {leaves.length === 0 ? (
              <p>İzin Yok!</p>
            ) : (
              <div style={{ overflowX: "auto" }}>
                <table className="table" style={{ minWidth: 360, width: "100%" }}>
                  <thead>
                    <tr>
                      <th>Tarih</th>
                      <th>Saat</th>
                      <th>Durum</th>
                      <th>Açıklama</th>
                    </tr>
                  </thead>
                  <tbody>
                    {leaves.map((l) => {
                      const same = l.from === l.to
                      const range = `${fmtTr(new Date(l.from))}${same ? "" : " - " + fmtTr(new Date(l.to))}`
                      const times = l.fromTime && l.toTime ? `${l.fromTime} - ${l.toTime}` : "-"
                      const st = l.status === "Approved" ? "Onaylandı" : l.status === "Rejected" ? "Reddedildi" : "Bekliyor"
                      return (
                        <tr key={l.id}>
                          <td>{range}</td>
                          <td>{same ? times : "-"}</td>
                          <td>{st}</td>
                          <td>{l.reason || "-"}</td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}
    </main>
  )
}

