"use client"
import { useCallback, useEffect, useRef, useState } from 'react'

type QueueItem = {
  endpoint: string
  payload: any
}

const STORAGE_KEY = 'offlineQueue:v1'

function readQueue(): QueueItem[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : []
  } catch {
    return []
  }
}

function writeQueue(items: QueueItem[]) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(items))
  } catch {
    // ignore
  }
}

function getToken(): string | null {
  try {
    const ls = typeof localStorage !== 'undefined' ? localStorage.getItem('ktp_c_token') : null
    if (ls) return ls
    if (typeof document !== 'undefined') {
      const v = document.cookie.split('; ').find(x => x.startsWith('ktp_c_token='))?.split('=')[1]
      return v || null
    }
    return null
  } catch { return null }
}

export function useOfflineQueue(baseUrl: string) {
  const [isOnline, setIsOnline] = useState<boolean>(true)
  const flushingRef = useRef(false)

  const flush = useCallback(async () => {
    if (flushingRef.current) return
    flushingRef.current = true
    try {
      let q = readQueue()
      if (q.length === 0) return

      const remaining: QueueItem[] = []
      for (const item of q) {
        try {
          const res = await fetch(baseUrl + item.endpoint, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', ...(getToken() ? { Authorization: `Bearer ${getToken()}` } : {}) },
            body: JSON.stringify(item.payload)
          })
          if (!res.ok) {
            remaining.push(item)
          }
        } catch {
          remaining.push(item)
        }
      }
      writeQueue(remaining)
    } finally {
      flushingRef.current = false
    }
  }, [baseUrl])

  const enqueue = useCallback((endpoint: string, payload: any) => {
    const q = readQueue()
    q.push({ endpoint, payload })
    writeQueue(q)
  }, [])

  const sendOrQueue = useCallback(async (endpoint: string, payload: any) => {
    if (!isOnline) {
      enqueue(endpoint, payload)
      return { queued: true }
    }
    try {
      const res = await fetch(baseUrl + endpoint, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...(getToken() ? { Authorization: `Bearer ${getToken()}` } : {}) },
        body: JSON.stringify(payload)
      })
      if (!res.ok) {
        enqueue(endpoint, payload)
        return { queued: true }
      }
      return { ok: true }
    } catch {
      enqueue(endpoint, payload)
      return { queued: true }
    }
  }, [baseUrl, enqueue, isOnline])

  useEffect(() => {
    const update = () => setIsOnline(typeof navigator !== 'undefined' ? navigator.onLine : true)
    update()
    window.addEventListener('online', () => { update(); flush() })
    window.addEventListener('offline', update)
    // try flush on mount too
    flush()
    return () => {
      window.removeEventListener('online', () => { update(); flush() })
      window.removeEventListener('offline', update)
    }
  }, [flush])

  return { isOnline, sendOrQueue, flush }
}
