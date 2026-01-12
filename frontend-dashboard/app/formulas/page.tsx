"use client"

import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { api, type FormulaBindingRow, type FormulaTemplate, type Product } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select } from '@/components/ui/select'
import { Separator } from '@/components/ui/separator'

const VARIABLE_BLOCKS = [
  { id: 'Amount', label: 'Amount', value: 'Amount' },
  { id: 'HasGoldPrice', label: 'HasGoldPrice', value: 'HasGoldPrice' },
  { id: 'AltinSatisFiyati', label: 'AltinSatisFiyati', value: 'AltinSatisFiyati' },
  { id: 'Product.Gram', label: 'Product.Gram', value: 'Product.Gram' },
  { id: 'VatRate', label: 'VatRate', value: 'VatRate' },
]

const OP_BLOCKS = [
  { id: '+', label: '+', value: '+' },
  { id: '-', label: '-', value: '-' },
  { id: '*', label: '*', value: '*' },
  { id: '/', label: '/', value: '/' },
  { id: '==', label: '==', value: '==' },
  { id: '!=', label: '!=', value: '!=' },
  { id: '<', label: '<', value: '<' },
  { id: '<=', label: '<=', value: '<=' },
  { id: '>', label: '>', value: '>' },
  { id: '>=', label: '>=', value: '>=' },
  { id: '?', label: '?:', value: '?' },
  { id: ':', label: ':', value: ':' },
]

const FUNC_BLOCKS = [
  { id: 'round', label: 'round(x,n)', value: 'round' },
  { id: 'min', label: 'min(x,y)', value: 'min' },
  { id: 'max', label: 'max(x,y)', value: 'max' },
  { id: 'abs', label: 'abs(x)', value: 'abs' },
  { id: 'mod', label: 'mod(x,y)', value: 'mod', disabled: true },
]

const LITERAL_BLOCKS = [
  { id: '0.20', label: '0.20', value: '0.20' },
  { id: '0', label: '0', value: '0' },
  { id: '1', label: '1', value: '1' },
  { id: '2', label: '2', value: '2' },
  { id: '3', label: '3', value: '3' },
  { id: '4', label: '4', value: '4' },
  { id: '5', label: '5', value: '5' },
  { id: '6', label: '6', value: '6' },
  { id: '7', label: '7', value: '7' },
  { id: '8', label: '8', value: '8' },
  { id: '9', label: '9', value: '9' },
  { id: '(', label: '(', value: '(' },
  { id: ')', label: ')', value: ')' },
  { id: ',', label: ',', value: ',' },
]

type Step = { op: string; var?: string; value?: number; expr?: string }

type Token = {
  id: string
  label: string
  value: string
  kind: 'var' | 'op' | 'func' | 'literal' | 'component'
}

type CustomComponent = {
  id: string
  name: string
  tokens: Token[]
  expr: string
}

function tokenizeExpr(expr: string): Token[] {
  const tokens: Token[] = []
  const regex = /([A-Za-z_][A-Za-z0-9_.]*|\d+(?:\.\d+)?|==|!=|<=|>=|[()+\-*/?:,])/g
  const matches = expr.match(regex) || []
  matches.forEach((value, index) => {
    const kind: Token['kind'] =
      VARIABLE_BLOCKS.some((b) => b.value === value) ? 'var'
        : OP_BLOCKS.some((b) => b.value === value) ? 'op'
          : FUNC_BLOCKS.some((b) => b.value === value) ? 'func'
            : 'literal'
    tokens.push({ id: `${value}-${index}`, label: value, value, kind })
  })
  return tokens
}

function buildExprFromTokens(tokens: Token[]): string {
  const raw = tokens.map((token) => token.value).join(' ')
  return raw
    .replace(/\s+,/g, ',')
    .replace(/,\s+/g, ',')
    .replace(/\(\s+/g, '(')
    .replace(/\s+\)/g, ')')
    .replace(/\s+\?/g, '?')
    .replace(/\?\s+/g, '?')
    .replace(/\s+:\s+/g, ':')
}

export default function FormulaBuilderPage() {
  const [products, setProducts] = useState<Product[]>([])
  const [productId, setProductId] = useState<string>('')
  const [direction, setDirection] = useState<'Sale' | 'Purchase'>('Sale')
  const [template, setTemplate] = useState<FormulaTemplate | null>(null)
  const [bindings, setBindings] = useState<FormulaBindingRow[]>([])
  const [steps, setSteps] = useState<Step[]>([])
  const [definitionMeta, setDefinitionMeta] = useState<{ vars?: Record<string, unknown>; output?: Record<string, unknown> }>({})
  const [activeStepIndex, setActiveStepIndex] = useState<number>(0)
  const [tokens, setTokens] = useState<Token[]>([])
  const [selectedTokens, setSelectedTokens] = useState<Set<string>>(new Set())
  const [customComponents, setCustomComponents] = useState<CustomComponent[]>([])
  const [componentName, setComponentName] = useState('')
  const [componentInput, setComponentInput] = useState('Amount')
  const [componentOp, setComponentOp] = useState<'*' | '/' | '+' | '-' >('*')
  const [componentNumber, setComponentNumber] = useState('0')
  const [newStepVar, setNewStepVar] = useState('sonuc')
  const [loadError, setLoadError] = useState('')
  const [saveStatus, setSaveStatus] = useState('')
  const dragPayloadRef = useRef<Token | null>(null)
  const [dragHover, setDragHover] = useState(false)
  const [flowHover, setFlowHover] = useState(false)
  const [isDragging, setIsDragging] = useState(false)

  const activeStep = steps[activeStepIndex] || null

  const palette = useMemo(() => {
    const customBlocks = customComponents.map((component) => ({
      id: component.id,
      label: component.name,
      value: component.name,
      kind: 'component' as const,
      expr: component.expr,
    }))

    return {
      variables: VARIABLE_BLOCKS.map((item) => ({ ...item, kind: 'var' as const })),
      operators: OP_BLOCKS.map((item) => ({ ...item, kind: 'op' as const })),
      functions: FUNC_BLOCKS.map((item) => ({ ...item, kind: 'func' as const })),
      literals: LITERAL_BLOCKS.map((item) => ({ ...item, kind: 'literal' as const })),
      components: customBlocks,
    }
  }, [customComponents])

  const fetchProducts = useCallback(async () => api.listProducts(), [])

  useEffect(() => {
    let alive = true
    const load = async () => {
      try {
        const data = await fetchProducts()
        if (!alive) return
        setProducts(data)
        if (!productId && data.length > 0) setProductId(data[0].id)
      } catch {
        if (!alive) return
        setLoadError('Ürünler alınamadı.')
      }
    }
    load()
    return () => { alive = false }
  }, [fetchProducts, productId])

  useEffect(() => {
    let alive = true
    if (!productId) return
    const load = async () => {
      try {
        setLoadError('')
        setSaveStatus('')
        const product = products.find((p) => p.id === productId)
        const bindingRows = await api.listProductFormulaBindings(productId)
        if (!alive) return
        setBindings(bindingRows)
        const activeBinding = bindingRows.find((row) => String(row.direction).toLowerCase() === direction.toLowerCase() && row.isActive)
        const templateId = activeBinding?.templateId || product?.defaultFormulaId || ''
        if (!templateId) {
          setTemplate(null)
          setSteps([])
          setDefinitionMeta({})
          setTokens([])
          return
        }
        const formula = await api.getFormula(templateId)
        if (!alive) return
        setTemplate(formula)
        const parsed = JSON.parse(formula.definitionJson || '{}')
        const parsedSteps = Array.isArray(parsed.steps) ? parsed.steps : []
        setSteps(parsedSteps)
        setDefinitionMeta({ vars: parsed.vars || {}, output: parsed.output || {} })
        setActiveStepIndex(0)
        const firstExpr = parsedSteps[0]?.expr || ''
        setTokens(firstExpr ? tokenizeExpr(firstExpr) : [])
      } catch {
        if (!alive) return
        setLoadError('Formül bilgisi alınamadı.')
      }
    }
    load()
    return () => { alive = false }
  }, [productId, direction, products])

useEffect(() => {
  if (activeStep && activeStep.op === 'calc') {
    setTimeout(() => {
      document.getElementById('formula-drop-zone')?.scrollIntoView({ behavior: 'smooth' })
    }, 0)
  }
}, [activeStepIndex])

  useEffect(() => {
    if (!activeStep?.expr) {
      setTokens([])
      return
    }
    setTokens(tokenizeExpr(activeStep.expr))
    setSelectedTokens(new Set())
  }, [activeStepIndex])

  useEffect(() => {
    // Reset drag state when switching steps to avoid stale drag lifecycle.
    dragPayloadRef.current = null
    setIsDragging(false)
    setDragHover(false)
    setFlowHover(false)
  }, [activeStepIndex])

  const handleDragStart = (token: Token) => (event: React.DragEvent<HTMLDivElement>) => {
    const payload = JSON.stringify(token)
    event.dataTransfer.setData('application/json', payload)
    event.dataTransfer.setData('text/plain', payload)
    event.dataTransfer.effectAllowed = 'copy'
    dragPayloadRef.current = token
    // Delay state update to avoid cancelling drag start in some browsers.
    requestAnimationFrame(() => setIsDragging(true))
  }

  const handleDropAt = (index: number) => (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault()
    event.stopPropagation()

  let payload: Token | null = dragPayloadRef.current
  try {
    const text =
      event.dataTransfer.getData('application/json') ||
      event.dataTransfer.getData('text/plain')
    if (text) payload = JSON.parse(text)
  } catch {}

  if (!payload) return

  // Use functional state to avoid stale closure bugs during rapid drag/drop.
  setTokens((prev) => {
    const safeIndex = Math.min(index, prev.length)
    const next = [...prev]
    next.splice(safeIndex, 0, {
      ...payload,
      id: `${payload.value}-${Date.now()}-${Math.random()}`
    })
    updateActiveStepExpr(next)
    return next
  })

  dragPayloadRef.current = null
  setIsDragging(false)
}

  const toggleTokenSelection = (tokenId: string) => {
    setSelectedTokens((prev) => {
      const next = new Set(prev)
      if (next.has(tokenId)) next.delete(tokenId)
      else next.add(tokenId)
      return next
    })
  }

  const removeToken = (tokenId: string) => {
    const next = tokens.filter((token) => token.id !== tokenId)
    setTokens(next)
    updateActiveStepExpr(next)
  }

  const handleCreateComponent = () => {
    if (!componentName.trim()) return
    const numberValue = Number(componentNumber.replace(',', '.'))
    if (!Number.isFinite(numberValue)) return
    const expr = `${componentInput} ${componentOp} ${numberValue}`
    const selected = tokenizeExpr(expr)
    const newComponent: CustomComponent = {
      id: `component-${Date.now()}`,
      name: componentName.trim(),
      tokens: selected,
      expr,
    }
    setCustomComponents((prev) => [newComponent, ...prev])
    setComponentName('')
    setSelectedTokens(new Set())
  }

  const upsertComponent = (name: string, expr: string) => {
    setCustomComponents((prev) => {
      const existing = prev.find((item) => item.name === name)
      const tokens = expr ? tokenizeExpr(expr) : []
      if (existing) {
        return prev.map((item) => item.name === name ? { ...item, expr, tokens } : item)
      }
      return [{ id: `component-${Date.now()}`, name, expr, tokens }, ...prev]
    })
  }

  const updateActiveStepExpr = (nextTokens: Token[]) => {
    if (!activeStep) return
    const nextExpr = buildExprFromTokens(nextTokens)
    setSteps((prev) => prev.map((step, idx) => idx === activeStepIndex ? { ...step, expr: nextExpr } : step))
    if (activeStep.var && nextExpr.trim()) {
      upsertComponent(activeStep.var, nextExpr)
    }
  }

  const handleAddCalcStep = () => {
    const name = newStepVar.trim()
    if (!name) return
    const step: Step = { op: 'calc', var: name, expr: '' }
    setSteps((prev) => {
      const next = [...prev, step]
      const index = next.length - 1
      setActiveStepIndex(index)
      setTokens([]) // kalabilir AMA drop alanları token.length’e bağlı OLMAMALI
      return next
    })
    upsertComponent(name, '')
    setIsDragging(false)
    dragPayloadRef.current = null
  }

  const handleSave = async () => {
    if (!template) return
    try {
      setSaveStatus('')
      const nextDefinition = JSON.stringify({ vars: definitionMeta.vars || {}, steps, output: definitionMeta.output || {} }, null, 2)
      await api.updateFormula(template.id, {
        code: template.code,
        name: template.name,
        scope: template.scope as string,
        formulaType: template.formulaType as string,
        dslVersion: template.dslVersion,
        definitionJson: nextDefinition,
        isActive: template.isActive,
      })
      setSaveStatus('Kaydedildi.')
    } catch {
      setSaveStatus('Kaydetme basarisiz.')
    }
  }

  const productOptions = products.map((product) => ({ id: product.id, label: `${product.code} • ${product.name}` }))
  // Drop hedefi her zaman aktif olabilir; slotlar sadece drag sırasında görünür.
  const canDrop = Boolean(activeStep && activeStep.op === 'calc')
  const showSlots = canDrop && isDragging

  return (
    <div className="space-y-6">
      <Card className="border-[color:hsl(var(--border))]">
        <CardHeader>
          <CardTitle>Formül Motoru Tasarımı</CardTitle>
          <p className="text-sm text-muted-foreground">Ürün bazlı formülleri sürükle bırak ile düzenleyin, birleşen oluşturup tekrar kullanın.</p>
        </CardHeader>
        <CardContent className="grid gap-6 lg:grid-cols-[320px,1fr]">
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Ürün</Label>
              <Select value={productId} onChange={(event) => setProductId(event.target.value)}>
                <option value="" disabled>Ürün seçin</option>
                {productOptions.map((item) => (
                  <option key={item.id} value={item.id}>{item.label}</option>
                ))}
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Yön</Label>
              <Select value={direction} onChange={(event) => setDirection(event.target.value as 'Sale' | 'Purchase')}>
                <option value="Sale">Satış</option>
                <option value="Purchase">Alış</option>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Aktif Formül</Label>
              <div className="rounded-md border px-3 py-2 text-sm">
                {template ? `${template.code} • ${template.name}` : 'Formül bulunamadı'}
              </div>
            </div>
            <div className="space-y-2">
              <Label>Ürün Bağlantıları</Label>
              <div className="space-y-2 text-xs text-muted-foreground">
                {bindings.length === 0 ? (
                  <p>Bağlı formül yok.</p>
                ) : bindings.map((binding) => (
                  <div key={binding.id} className="rounded-md border px-3 py-2">
                    <div className="flex items-center justify-between">
                      <span>{binding.templateCode || binding.templateName || binding.templateId}</span>
                      <span>{String(binding.direction) === 'Purchase' ? 'Alış' : 'Satış'}</span>
                    </div>
                    <div>{binding.isActive ? 'Aktif' : 'Pasif'}</div>
                  </div>
                ))}
              </div>
            </div>
            {loadError && <p className="text-sm text-red-600">{loadError}</p>}
            <Separator />
            <div className="space-y-2">
              <Label>Adımlar</Label>
              <div className="space-y-2">
                {steps.length === 0 ? (
                  <p className="text-sm text-muted-foreground">Formül adımı yok.</p>
                ) : steps.map((step, index) => (
                  <button
                    key={`${step.var}-${index}`}
                    className={`w-full rounded-md border px-3 py-2 text-left text-sm transition ${index === activeStepIndex ? 'border-emerald-500 bg-emerald-50/60' : 'border-[color:hsl(var(--border))] hover:bg-muted/40'}`}
                    onClick={() => setActiveStepIndex(index)}
                  >
                    <div className="flex items-center justify-between">
                      <span className="font-medium">{step.var || 'Adım'}</span>
                      <span className="text-xs text-muted-foreground">{step.op}</span>
                    </div>
                    <div className="text-xs text-muted-foreground truncate">{step.expr || step.value}</div>
                  </button>
                ))}
              </div>
            </div>
            <Separator />
            <div className="space-y-2">
              <Label>Birleşen Oluştur</Label>
              <Input
                placeholder="Örn. GramX0316"
                value={componentName}
                onChange={(event) => setComponentName(event.target.value)}
              />
              <div className="grid gap-2 sm:grid-cols-[1fr,120px]">
                <Select value={componentInput} onChange={(event) => setComponentInput(event.target.value)}>
                  {VARIABLE_BLOCKS.map((item) => (
                    <option key={item.id} value={item.value}>{item.label}</option>
                  ))}
                </Select>
                <Select value={componentOp} onChange={(event) => setComponentOp(event.target.value as '*' | '/' | '+' | '-')}>
                  <option value="*">Çarpma</option>
                  <option value="/">Bölme</option>
                  <option value="+">Toplama</option>
                  <option value="-">Çıkarma</option>
                </Select>
              </div>
              <Input
                placeholder="Örn. 0,316"
                value={componentNumber}
                onChange={(event) => setComponentNumber(event.target.value)}
              />
              <Button className="w-full" onClick={handleCreateComponent}>Bileşen oluştur</Button>
              <p className="text-xs text-muted-foreground">Girdi + işlem + sayıdan birleşen oluşturulur ve palette görünür.</p>
            </div>
            <Button variant="outline" className="w-full" onClick={handleSave} disabled={!template}>Formülü kaydet</Button>
            {saveStatus && <p className="text-sm text-muted-foreground">{saveStatus}</p>}
          </div>

          <div className="space-y-6">
            <Card className="border-[color:hsl(var(--border))]">
              <CardHeader>
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <CardTitle className="text-base">Formül Tuvali</CardTitle>
                    <p className="text-xs text-muted-foreground">Tokenları sürükleyip adımın sağ/sol tarafına bırakın. Seçmek için tıklayın.</p>
                  </div>
                  <Button onClick={handleSave} disabled={!template}>Formülü kaydet</Button>
                </div>
              </CardHeader>
              <CardContent>
                <div className="mb-4 rounded-lg border border-dashed border-[color:hsl(var(--border))] bg-white/70 p-4">
                  <div className="flex flex-wrap items-center gap-3">
                    <Label className="text-xs uppercase tracking-wide text-muted-foreground">Yeni hesap adımı</Label>
                    <Input
                      className="max-w-[200px]"
                      placeholder="Örn. gram"
                      value={newStepVar}
                      onChange={(event) => setNewStepVar(event.target.value)}
                    />
                    <Button variant="outline" onClick={handleAddCalcStep}>Adım ekle</Button>
                  </div>
                </div>
                <div className="rounded-lg border border-dashed border-[color:hsl(var(--border))] bg-gradient-to-br from-muted/30 via-white to-muted/10 p-4">
                  <div className="mb-3 flex items-center justify-between text-xs uppercase tracking-[0.2em] text-muted-foreground">
                    <span>Birleştirme Alanı</span>
                    <span className="normal-case tracking-normal">{activeStep?.var || 'Adım seçilmedi'}</span>
                  </div>
                  <div
                    onDragEnter={() => setDragHover(true)}
                    onDragLeave={() => setDragHover(false)}
                    onDragOver={(event) => { event.preventDefault(); setDragHover(true) }}
                    onDrop={(event) => {
                      event.stopPropagation()
                      setDragHover(false)
                      if (!canDrop) return
                      handleDropAt(Number.MAX_SAFE_INTEGER)(event)
                    }}
                    className={`mb-4 flex min-h-[120px] items-center justify-center rounded-xl border-2 border-dashed bg-white/80 text-sm transition ${
                      dragHover ? 'border-emerald-400 bg-emerald-50 text-emerald-700' : 'border-emerald-200 text-muted-foreground'
                    }`}
                  >
                    {activeStep && activeStep.op === 'calc'
                      ? (dragHover ? 'Bırak ve birleştir' : 'Formülü burada toparla')
                      : 'Önce hesap adımı seç veya ekle'}
                  </div>
                  {!canDrop ? (
                    <p className="text-sm text-muted-foreground">Hesaplama adımı seçildiğinde tokenlar burada birleştirilecek.</p>
                  ) : (
                    <div
                      onDragEnter={() => setFlowHover(true)}
                      onDragLeave={() => setFlowHover(false)}
                      onDragOver={(event) => { event.preventDefault(); setFlowHover(true) }}
                      onDrop={(event) => {
                        event.stopPropagation()
                        setFlowHover(false)
                        if (!canDrop) return
                        handleDropAt(Number.MAX_SAFE_INTEGER)(event)
                      }}
                      className={`rounded-lg border border-dashed px-3 py-2 transition ${
                        flowHover ? 'border-emerald-300 bg-emerald-50/40' : 'border-emerald-100 bg-white/60'
                      }`}
                    >
                      <div className="mb-2 text-xs uppercase tracking-[0.2em] text-muted-foreground">Akış</div>
                      <div className="flex flex-wrap items-center gap-2">
                        {showSlots && (
                          <div
                            onDragOver={(event) => event.preventDefault()}
                            onDrop={handleDropAt(0)}
                            className="h-10 w-6 rounded-md border border-dashed border-emerald-200"
                            title="Buraya bırak"
                          />
                        )}
                        {tokens.map((token, index) => (
                          <div key={token.id} className="flex items-center gap-2">
                            <div
                              draggable
                              onDragStart={handleDragStart(token)}
                              onDragEnd={() => { dragPayloadRef.current = null; setIsDragging(false) }}
                              onClick={() => toggleTokenSelection(token.id)}
                              className={`group relative flex items-center gap-2 rounded-full border px-3 py-1 text-sm shadow-sm transition ${
                                selectedTokens.has(token.id) ? 'border-emerald-500 bg-emerald-50' : 'border-[color:hsl(var(--border))] bg-white'
                              }`}
                            >
                              <span className="font-medium">{token.label}</span>
                              <button
                                type="button"
                                onClick={(event) => { event.stopPropagation(); removeToken(token.id) }}
                                className="opacity-0 transition group-hover:opacity-100 text-xs text-muted-foreground"
                                aria-label="Token sil"
                              >
                                ✕
                              </button>
                            </div>
                            {showSlots && (
                              <div
                                onDragOver={(event) => event.preventDefault()}
                                onDrop={handleDropAt(index + 1)}
                                className="h-10 w-6 rounded-md border border-dashed border-emerald-200"
                                title="Buraya bırak"
                              />
                            )}
                          </div>
                        ))}
                        {tokens.length === 0 && <span className="text-xs text-muted-foreground">Token bırakın</span>}
                      </div>
                    </div>
                  )}
                </div>

              </CardContent>
            </Card>

            <Card className="border-[color:hsl(var(--border))]">
              <CardHeader>
                <CardTitle className="text-base">Bileşen Paleti</CardTitle>
                <p className="text-xs text-muted-foreground">Sürükle bırak ile formüle ekle.</p>
              </CardHeader>
              <CardContent className="grid gap-5 lg:grid-cols-2">
                <PaletteBlock title="Girdi" blocks={palette.variables} onDragStart={handleDragStart} onDragEnd={() => { dragPayloadRef.current = null; setIsDragging(false) }} />
                <PaletteBlock title="Operatör" blocks={palette.operators} onDragStart={handleDragStart} onDragEnd={() => { dragPayloadRef.current = null; setIsDragging(false) }} />
                <PaletteBlock title="Fonksiyon" blocks={palette.functions} onDragStart={handleDragStart} onDragEnd={() => { dragPayloadRef.current = null; setIsDragging(false) }} />
                <PaletteBlock title="Sabitler" blocks={palette.literals} onDragStart={handleDragStart} onDragEnd={() => { dragPayloadRef.current = null; setIsDragging(false) }} />
                <PaletteBlock title="Birleşenler" blocks={palette.components} onDragStart={handleDragStart} onDragEnd={() => { dragPayloadRef.current = null; setIsDragging(false) }} emptyText="Henüz bileşen yok" />
              </CardContent>
            </Card>

            <Card className="border-[color:hsl(var(--border))]">
              <CardHeader>
                <CardTitle className="text-base">Formül JSON</CardTitle>
              </CardHeader>
              <CardContent>
                <pre className="max-h-64 overflow-auto rounded-md border bg-muted/30 p-3 text-xs">
                  {JSON.stringify({ steps }, null, 2)}
                </pre>
              </CardContent>
            </Card>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}

function PaletteBlock({
  title,
  blocks,
  onDragStart,
  onDragEnd,
  emptyText,
}: {
  title: string
  blocks: Array<{ id: string; label: string; value: string; kind?: Token['kind']; disabled?: boolean; expr?: string }>
  onDragStart: (token: Token) => (event: React.DragEvent<HTMLDivElement>) => void
  onDragEnd?: () => void
  emptyText?: string
}) {
  return (
    <div className="space-y-3">
      <h3 className="text-xs font-semibold uppercase tracking-[0.2em] text-muted-foreground">{title}</h3>
      {blocks.length === 0 ? (
        <p className="text-xs text-muted-foreground">{emptyText || 'Kayıt yok'}</p>
      ) : (
        <div className="flex flex-wrap gap-2">
          {blocks.map((block) => (
            <div key={block.id} className="flex flex-col items-start">
              <div
                draggable={!block.disabled}
                onDragStart={block.disabled ? undefined : onDragStart({
                  id: block.id,
                  label: block.label,
                  value: block.value,
                  kind: block.kind || 'literal',
                })}
                onDragEnd={onDragEnd}
                className={`cursor-grab rounded-full border px-3 py-1 text-xs shadow-sm transition ${block.disabled ? 'cursor-not-allowed bg-muted/40 text-muted-foreground' : 'bg-white hover:-translate-y-0.5 hover:shadow-md'}`}
                title={block.disabled ? 'Yakinda' : 'Surukle'}
              >
                {block.label}
              </div>
              {block.kind === 'component' && block.expr ? (
                <div className="mt-1 text-[11px] text-muted-foreground">{block.expr}</div>
              ) : null}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
