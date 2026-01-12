"use client"
import { useState } from 'react'
import { api, formatDateTimeTr, type Category } from '@/lib/api'
import { Button } from '@/components/ui/button'

type CategoryOption = { id: string; label: string; depth: number }

function buildCategoryOptions(categories: Category[]): { options: CategoryOption[]; childrenMap: Map<string, Category[]>; categoryMap: Map<string, Category> } {
  const rootKey = '__root__'
  const childrenMap = new Map<string, Category[]>()
  const categoryMap = new Map<string, Category>()
  for (const c of categories) {
    categoryMap.set(c.id, c)
    const key = c.parentId ?? rootKey
    const list = childrenMap.get(key) ?? []
    list.push(c)
    childrenMap.set(key, list)
  }
  for (const list of childrenMap.values()) {
    list.sort((a, b) => a.name.localeCompare(b.name, 'tr-TR'))
  }
  const options: CategoryOption[] = []
  const walk = (parentId: string | null, depth: number) => {
    const key = parentId ?? rootKey
    const list = childrenMap.get(key) ?? []
    for (const c of list) {
      options.push({ id: c.id, label: c.name, depth })
      walk(c.id, depth + 1)
    }
  }
  walk(null, 0)
  return { options, childrenMap, categoryMap }
}

function collectDescendantIds(childrenMap: Map<string, Category[]>, categoryId: string): Set<string> {
  const set = new Set<string>()
  const stack = [categoryId]
  while (stack.length > 0) {
    const current = stack.pop()
    if (!current) continue
    const children = childrenMap.get(current) ?? []
    for (const child of children) {
      if (!set.has(child.id)) {
        set.add(child.id)
        stack.push(child.id)
      }
    }
  }
  return set
}

export function CategoryManagement({
  categories,
  onRefresh,
}: {
  categories: Category[]
  onRefresh: () => Promise<void>
}) {
  const { options, childrenMap, categoryMap } = buildCategoryOptions(categories)

  if (categories.length === 0) {
    return (
      <div className="space-y-4">
        <NewCategoryForm options={options} onCreated={onRefresh} />
        <p className="text-sm text-muted-foreground">Kayıtlı kategori yok.</p>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <NewCategoryForm options={options} onCreated={onRefresh} />
      <div className="overflow-x-auto">
        <table className="min-w-[900px] w-full text-sm">
          <thead>
            <tr className="text-left">
              <th className="p-2">Kategori</th>
              <th className="p-2">Üst Kategori</th>
              <th className="p-2">Oluşturma</th>
              <th className="p-2">Güncelleme</th>
              <th className="p-2">Güncelleyen</th>
              <th className="p-2">İşlem</th>
            </tr>
          </thead>
          <tbody>
            {options.map(({ id, label, depth }) => {
              const category = categoryMap.get(id)
              if (!category) return null
              const parent = category.parentId ? categoryMap.get(category.parentId) : null
              const descendants = collectDescendantIds(childrenMap, category.id)
              return (
                <CategoryRow
                  key={category.id}
                  category={category}
                  label={label}
                  depth={depth}
                  parentLabel={parent?.name ?? '—'}
                  options={options}
                  invalidParentIds={new Set([category.id, ...descendants])}
                  onChange={async (patch) => { await api.updateCategory(category.id, patch); await onRefresh() }}
                  onDelete={async () => { await api.deleteCategory(category.id); await onRefresh() }}
                />
              )
            })}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function NewCategoryForm({
  options,
  onCreated,
}: {
  options: CategoryOption[]
  onCreated: () => Promise<void>
}) {
  const [name, setName] = useState('')
  const [parentId, setParentId] = useState('')
  const [busy, setBusy] = useState(false)

  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="flex flex-col">
        <label className="text-sm text-muted-foreground">Kategori Adı</label>
        <input value={name} onChange={(e) => setName(e.target.value)} className="border rounded px-3 py-2 w-64 text-slate-900" placeholder="Örn: Alyans" />
      </div>
      <div className="flex flex-col">
        <label className="text-sm text-muted-foreground">Üst Kategori</label>
        <select value={parentId} onChange={(e) => setParentId(e.target.value)} className="border rounded px-3 py-2 w-64 text-slate-900">
          <option value="">Yok</option>
          {options.map((opt) => (
            <option key={opt.id} value={opt.id}>
              {opt.depth > 0 ? `${'—'.repeat(opt.depth)} ${opt.label}` : opt.label}
            </option>
          ))}
        </select>
      </div>
      <Button
        disabled={busy || !name.trim()}
        onClick={async () => {
          setBusy(true)
          await api.createCategory({ name: name.trim(), parentId: parentId || null })
          setName('')
          setParentId('')
          await onCreated()
          setBusy(false)
        }}
      >
        Ekle
      </Button>
    </div>
  )
}

function CategoryRow({
  category,
  label,
  depth,
  parentLabel,
  options,
  invalidParentIds,
  onChange,
  onDelete,
}: {
  category: Category
  label: string
  depth: number
  parentLabel: string
  options: CategoryOption[]
  invalidParentIds: Set<string>
  onChange: (patch: { name: string; parentId?: string | null }) => Promise<void>
  onDelete: () => Promise<void>
}) {
  const [name, setName] = useState(category.name)
  const [parentId, setParentId] = useState(category.parentId ?? '')
  const [busy, setBusy] = useState(false)

  return (
    <tr className="border-t">
      <td className="p-2">
        <div className="flex items-center gap-2">
          <span className="text-xs text-muted-foreground">{depth > 0 ? '—'.repeat(depth) : ''}</span>
          <input value={name} onChange={(e) => setName(e.target.value)} className="border rounded px-2 py-1 w-48 text-slate-900" />
        </div>
        <div className="text-xs text-muted-foreground mt-1">{label}</div>
      </td>
      <td className="p-2">
        <select value={parentId} onChange={(e) => setParentId(e.target.value)} className="border rounded px-2 py-1 w-56 text-slate-900">
          <option value="">Yok</option>
          {options.map((opt) => (
            <option key={opt.id} value={opt.id} disabled={invalidParentIds.has(opt.id)}>
              {opt.depth > 0 ? `${'—'.repeat(opt.depth)} ${opt.label}` : opt.label}
            </option>
          ))}
        </select>
        <div className="text-xs text-muted-foreground mt-1">{parentLabel}</div>
      </td>
      <td className="p-2">{formatDateTimeTr(category.createdAt)}</td>
      <td className="p-2">{formatDateTimeTr(category.updatedAt)}</td>
      <td className="p-2">{category.updatedUserEmail || category.updatedUserId || '—'}</td>
      <td className="p-2 flex gap-2">
        <Button size="sm" disabled={busy || !name.trim()} onClick={async () => { setBusy(true); await onChange({ name: name.trim(), parentId: parentId || null }); setBusy(false) }}>Kaydet</Button>
        <Button size="sm" variant="outline" disabled={busy} onClick={async () => { setBusy(true); await onDelete(); setBusy(false) }}>Sil</Button>
      </td>
    </tr>
  )
}
