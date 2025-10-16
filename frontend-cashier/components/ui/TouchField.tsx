"use client"
import React from 'react'

type Props = {
  label: string
  type?: string
  value: string | number
  placeholder?: string
  onChange: (v: string) => void
  inputMode?: React.HTMLAttributes<HTMLInputElement>["inputMode"]
  pattern?: string
  maxLength?: number
  name?: string
  onFocus?: () => void
  readOnly?: boolean
  disabled?: boolean
  error?: string
}

export default function TouchField({ label, type = 'text', value, placeholder, onChange, inputMode, pattern, maxLength, name, onFocus, readOnly, disabled, error }: Props) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      <label style={{ fontWeight: 600 }}>{label}</label>
      <input
        name={name}
        type={type}
        value={value as any}
        placeholder={placeholder}
        inputMode={inputMode}
        pattern={pattern}
        maxLength={maxLength}
        onFocus={onFocus}
        readOnly={readOnly}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        style={{
          minHeight: '56px',
          borderRadius: 10,
          border: `2px solid ${error ? '#b3261e' : '#ddd'}`,
          padding: '12px 14px',
          fontSize: 18,
          background: disabled ? '#f3f3f3' : '#fff'
        }}
      />
      {error && (
        <span style={{ color: '#b3261e', fontSize: 14 }}>{error}</span>
      )}
    </div>
  )
}
