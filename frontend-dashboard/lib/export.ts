import jsPDF from 'jspdf'
import autoTable, { UserOptions } from 'jspdf-autotable'
import * as XLSX from 'xlsx'
import { type Invoice, type Expense } from './api'

export function downloadInvoicesPdf(rows: Invoice[], opts?: { start?: string; end?: string }) {
  const doc = new jsPDF({ orientation: 'l' })
  const title = 'Faturalar'
  const range = (opts?.start || opts?.end) ? `Tarih Aralığı: ${opts?.start || '-'} - ${opts?.end || '-'}` : ''
  const total = rows.reduce((a, b) => a + Number(b.tutar), 0)
  doc.setFontSize(16)
  doc.text(title, 14, 16)
  if (range) {
    doc.setFontSize(11)
    doc.text(range, 14, 24)
  }
  const head = [[ 'Tarih', 'Sıra No', 'Müşteri', 'TCKN', 'Tutar', 'Ödeme Şekli' ]]
  const body = rows.map(r => [ r.tarih, String(r.siraNo), r.musteriAdSoyad || '-', r.tckn || '-', formatTRY(r.tutar), r.odemeSekli === 0 ? 'Havale' : 'Kredi Kartı' ])
  autoTable(doc, { head, body } as UserOptions)
  const y = (doc as any).lastAutoTable?.finalY || 28
  doc.setFontSize(12)
  doc.text(`Toplam Tutar: ${formatTRY(total)}`, 14, y + 10)
  const name = `Faturalar_${yyyymmdd(new Date())}.pdf`
  doc.save(name)
}

export function downloadInvoicesXlsx(rows: Invoice[]) {
  const ws = XLSX.utils.json_to_sheet(rows.map(r => ({
    Tarih: r.tarih,
    'Sıra No': r.siraNo,
    Müşteri: r.musteriAdSoyad || '',
    TCKN: r.tckn || '',
    Tutar: r.tutar,
    'Ödeme Şekli': r.odemeSekli === 0 ? 'Havale' : 'Kredi Kartı'
  })))
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'Faturalar')
  const name = `Faturalar_${yyyymmdd(new Date())}.xlsx`
  XLSX.writeFile(wb, name)
}

export function downloadExpensesPdf(rows: Expense[], opts?: { start?: string; end?: string }) {
  const doc = new jsPDF({ orientation: 'l' })
  const title = 'Giderler'
  const range = (opts?.start || opts?.end) ? `Tarih Aralığı: ${opts?.start || '-'} - ${opts?.end || '-'}` : ''
  const total = rows.reduce((a, b) => a + Number(b.tutar), 0)
  doc.setFontSize(16)
  doc.text(title, 14, 16)
  if (range) {
    doc.setFontSize(11)
    doc.text(range, 14, 24)
  }
  const head = [[ 'Tarih', 'Sıra No', 'Müşteri', 'TCKN', 'Tutar' ]]
  const body = rows.map(r => [ r.tarih, String(r.siraNo), r.musteriAdSoyad || '-', r.tckn || '-', formatTRY(r.tutar) ])
  autoTable(doc, { head, body } as UserOptions)
  const y = (doc as any).lastAutoTable?.finalY || 28
  doc.setFontSize(12)
  doc.text(`Toplam Tutar: ${formatTRY(total)}`, 14, y + 10)
  const name = `Giderler_${yyyymmdd(new Date())}.pdf`
  doc.save(name)
}

export function downloadExpensesXlsx(rows: Expense[]) {
  const ws = XLSX.utils.json_to_sheet(rows.map(r => ({
    Tarih: r.tarih,
    'Sıra No': r.siraNo,
    Müşteri: r.musteriAdSoyad || '',
    TCKN: r.tckn || '',
    Tutar: r.tutar
  })))
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'Giderler')
  const name = `Giderler_${yyyymmdd(new Date())}.xlsx`
  XLSX.writeFile(wb, name)
}

function yyyymmdd(d: Date) {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}${m}${day}`
}

function formatTRY(v: number) {
  return Number(v).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })
}

