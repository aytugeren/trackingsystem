"use client"

import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { printLabels } from '@/lib/printLabels'

type ToastState = { kind: 'success' | 'error'; text: string } | null

export default function EtiketBasmaPage() {
  const [values, setValues] = useState<string[]>([''])
  const [toast, setToast] = useState<ToastState>(null)
  const [isPrinting, setIsPrinting] = useState(false)

  function handleChange(index: number, next: string) {
    setValues(prev => prev.map((val, idx) => (idx === index ? next : val)))
  }

  function addField() {
    setValues(prev => [...prev, ''])
  }

  async function handlePrint() {
    const normalized = values
      .map(v => normalizeValue(v))
      .filter(v => v.length > 0)

    if (!normalized.length) {
      setToast({ kind: 'error', text: 'Lutfen en az bir gramaj degeri giriniz.' })
      return
    }

    setIsPrinting(true)
    setToast(null)
    try {
      const result = await printLabels(normalized)
      setToast({ kind: 'success', text: `${result.count} adet etiket yaziciya gonderildi.` })
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Bilinmeyen bir hata olustu.'
      setToast({ kind: 'error', text: message })
    } finally {
      setIsPrinting(false)
    }
  }

  return (
    <div className="grid gap-6 lg:grid-cols-[2fr_1fr]">
      <Card>
        <CardHeader>
          <CardTitle>Etiket Basma</CardTitle>
          <p className="text-sm text-muted-foreground">
            Gramaj listesi olusturun, ardindan tek tikla Zebra ZD220 yazicisina gonderin.
          </p>
        </CardHeader>
        <CardContent className="space-y-4">
          {values.map((value, index) => (
            <div key={`gram-${index}`} className="space-y-2">
              <Label htmlFor={`gram-${index}`}>Gramaj {index + 1}</Label>
              <Input
                id={`gram-${index}`}
                inputMode="decimal"
                placeholder="Orn: 3,43"
                value={value}
                onChange={(event) => handleChange(index, event.target.value)}
                autoComplete="off"
              />
            </div>
          ))}

          <div className="flex flex-wrap gap-3">
            <Button type="button" variant="outline" onClick={addField}>
              Yeni Alan Ekle
            </Button>
            <Button
              type="button"
              onClick={handlePrint}
              disabled={isPrinting}
            >
              {isPrinting ? 'Yazdiriliyor...' : 'Yazdir'}
            </Button>
          </div>

          {toast && (
            <div
              className={`rounded-md border px-3 py-2 text-sm ${
                toast.kind === 'success'
                  ? 'border-green-600/40 bg-green-600/5 text-green-700 dark:text-green-400'
                  : 'border-red-600/40 bg-red-600/5 text-red-700 dark:text-red-400'
              }`}
            >
              {toast.text}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function normalizeValue(value: string) {
  return value.trim().replace(/\s+/g, '').replace(/\./g, ',')
}
