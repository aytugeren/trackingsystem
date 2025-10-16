"use client"
import React from 'react'

type Props = {
  onKey: (k: string) => void
  onBackspace?: () => void
  onClear?: () => void
}

const keys = [
  ['1','2','3'],
  ['4','5','6'],
  ['7','8','9'],
  ['.','0','⌫']
]

export default function BigKeypad({ onKey, onBackspace, onClear }: Props) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8 }}>
      {keys.flat().map((k) => (
        <button
          key={k}
          type="button"
          className="secondary"
          onClick={() => {
            if (k === '⌫') onBackspace?.()
            else if (k === '.' && onKey) onKey('.')
            else onKey(k)
          }}
          style={{ fontSize: 22 }}
        >
          {k}
        </button>
      ))}
      <button type="button" className="secondary" onClick={onClear} style={{ gridColumn: 'span 3' }}>Temizle</button>
    </div>
  )
}

