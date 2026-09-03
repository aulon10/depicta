import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  ScanLine, ShieldCheck, Loader2, ImageOff, Check, X, Search,
  Inbox, CheckCircle2, Layers, Settings, AlertTriangle, Menu, LogOut,
} from 'lucide-react'
import { MockApiClient } from './api/mockClient'
import { HttpApiClient } from './api/httpClient'
import type { ApiClient } from './api/client'
import { UnauthorizedError } from './api/client'
import type { AuthSession, MediaItem, ScanRun, Site } from './api/types'
import { Button } from './components/ui/Button'
import { Checkbox } from './components/ui/Checkbox'
import { MediaReviewRow } from './components/MediaReviewRow'
import { SettingsView } from './components/SettingsView'
import { LoginView } from './components/LoginView'
import { Logo, LogoMark } from './components/Logo'
import { ToastStack, type ToastData } from './components/ui/Toast'
import { cn } from './lib/cn'

type View = 'pending_review' | 'applied' | 'all' | 'settings'

const NAV: { key: View; label: string; icon: typeof Inbox }[] = [
  { key: 'pending_review', label: 'Review queue', icon: Inbox },
  { key: 'applied', label: 'Applied', icon: CheckCircle2 },
  { key: 'all', label: 'All images', icon: Layers },
]

export default function App() {
  const client = useMemo<ApiClient>(() => {
    const base = import.meta.env.VITE_API_BASE
    return base ? new HttpApiClient(base) : new MockApiClient()
  }, [])
  const usingMock = useMemo(() => !import.meta.env.VITE_API_BASE, [])

  const [session, setSession] = useState<AuthSession | null>(null)
  const [items, setItems] = useState<MediaItem[]>([])
  const [site, setSite] = useState<Site | null>(null)
  const [scans, setScans] = useState<ScanRun[]>([])
  const [view, setView] = useState<View>('pending_review')
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [edits, setEdits] = useState<Record<string, string>>({})
  const [query, setQuery] = useState('')
  const [scanning, setScanning] = useState(false)
  const [busy, setBusy] = useState(false)
  const [navOpen, setNavOpen] = useState(false)
  const [toasts, setToasts] = useState<ToastData[]>([])

  const toast = useCallback((message: string, tone?: 'success' | 'error') => {
    setToasts((t) => [...t, { id: crypto.randomUUID(), message, tone }])
  }, [])
  const dismiss = useCallback((id: string) => setToasts((t) => t.filter((x) => x.id !== id)), [])

  // Any call that comes back 401 means the token is gone/expired: drop to the login screen.
  const handleAuthError = useCallback((err: unknown): boolean => {
    if (err instanceof UnauthorizedError) {
      client.setToken(null)
      setSession(null)
      toast('Session expired — please sign in again', 'error')
      return true
    }
    return false
  }, [client, toast])

  const login = useCallback(async (username: string, password: string) => {
    const s = await client.login(username, password) // throws on bad credentials; LoginView shows it
    client.setToken(s.token)
    setSession(s)
  }, [client])

  const logout = useCallback(() => {
    client.setToken(null)
    setSession(null)
    setItems([]); setSite(null); setScans([]); setSelected(new Set()); setEdits({})
    setView('pending_review'); setNavOpen(false)
  }, [client])

  const refresh = useCallback(async () => {
    try {
      const res = await client.listMedia({})
      setItems(res.items)
      setEdits((prev) => {
        const next = { ...prev }
        for (const it of res.items) {
          if (it.activeProposal && next[it.id] === undefined) {
            next[it.id] = it.activeProposal.editedAlt ?? it.activeProposal.proposedAlt
          }
        }
        return next
      })
      client.listScans(8).then(setScans).catch((e) => handleAuthError(e))
    } catch (e) {
      if (!handleAuthError(e)) throw e
    }
  }, [client, handleAuthError])

  useEffect(() => { if (session) refresh() }, [refresh, session])
  useEffect(() => {
    if (!session) return
    client.getSite().then(setSite).catch((e) => { if (!handleAuthError(e)) setSite(null) })
  }, [client, session, handleAuthError])

  const counts = useMemo(() => ({
    pending_review: items.filter((i) => i.state === 'pending_review').length,
    applied: items.filter((i) => i.state === 'applied').length,
    failed: items.filter((i) => i.state === 'failed').length,
    processing: items.filter((i) => i.state === 'processing').length,
    all: items.length,
  }), [items])

  const coverage = counts.all === 0 ? 0 : Math.round((counts.applied / counts.all) * 100)

  const visible = useMemo(() => {
    const base = view === 'all' || view === 'settings' ? items : items.filter((i) => i.state === view)
    const q = query.trim().toLowerCase()
    if (!q) return base
    return base.filter((i) =>
      (i.filename ?? '').toLowerCase().includes(q) ||
      (i.activeProposal?.proposedAlt ?? '').toLowerCase().includes(q) ||
      (i.currentAlt ?? '').toLowerCase().includes(q))
  }, [items, view, query])

  const selectablePending = visible.filter((i) => i.state === 'pending_review')
  const allSelected = selectablePending.length > 0 && selectablePending.every((i) => selected.has(i.id))
  const someSelected = selectablePending.some((i) => selected.has(i.id))

  const toggleSelect = (id: string) =>
    setSelected((s) => { const n = new Set(s); n.has(id) ? n.delete(id) : n.add(id); return n })
  const toggleSelectAll = () =>
    setSelected(() => allSelected ? new Set() : new Set(selectablePending.map((i) => i.id)))
  const setEdit = (id: string, v: string) => setEdits((e) => ({ ...e, [id]: v }))
  const selectedIds = [...selected].filter((id) => selectablePending.some((i) => i.id === id))

  async function scan() {
    setScanning(true)
    try {
      const run = await client.startScan()
      toast(`Scan complete — ${run.discovered} image${run.discovered === 1 ? '' : 's'} to review`)
      await refresh(); setTimeout(() => { refresh().catch(handleAuthError) }, 1500)
    } catch (e) { if (!handleAuthError(e)) toast('Scan failed', 'error') }
    finally { setScanning(false) }
  }
  async function approveIds(ids: string[]) {
    if (!ids.length) return
    setBusy(true)
    try {
      const res = await client.approve(ids.map((id) => ({ mediaItemId: id, editedAlt: edits[id] })))
      await refresh(); setSelected(new Set())
      if (res.succeeded.length) toast(`${res.succeeded.length} image${res.succeeded.length > 1 ? 's' : ''} updated on your site`)
    } catch (e) { if (!handleAuthError(e)) toast('Could not apply changes', 'error') }
    finally { setBusy(false) }
  }
  async function rejectIds(ids: string[]) {
    if (!ids.length) return
    setBusy(true)
    try { await client.reject(ids); await refresh(); setSelected(new Set()); toast(`${ids.length} rejected`) }
    catch (e) { if (!handleAuthError(e)) toast('Could not reject', 'error') }
    finally { setBusy(false) }
  }
  async function regenerate(id: string) {
    try {
      await client.regenerate([id])
      setEdits((e) => { const n = { ...e }; delete n[id]; return n })
      await refresh(); setTimeout(() => { refresh().catch(handleAuthError) }, 1200)
    } catch (e) { if (!handleAuthError(e)) toast('Could not regenerate', 'error') }
  }
  async function revert(id: string) {
    try { await client.revert(id); await refresh(); toast('Reverted to the previous alt text') }
    catch (e) { if (!handleAuthError(e)) toast('Could not revert', 'error') }
  }

  const go = (v: View) => { setView(v); setSelected(new Set()); setNavOpen(false) }
  const titles: Record<View, string> = {
    pending_review: 'Review queue', applied: 'Applied', all: 'All images', settings: 'Settings',
  }
  const subtitle =
    view === 'pending_review' ? `${counts.pending_review} awaiting your review`
    : view === 'applied' ? `${counts.applied} applied to your site`
    : view === 'all' ? `${counts.all} images total`
    : 'Connection, security & scan history'

  if (!session) {
    return <LoginView onSubmit={login} demoHint={usingMock} />
  }

  const nav = (
    <SidebarContent view={view} counts={counts} site={site} onGo={go} onLogout={logout} username={session.username} />
  )

  return (
    <div className="flex min-h-screen bg-zinc-50 text-zinc-900">
      {/* Desktop sidebar */}
      <aside className="sticky top-0 hidden h-screen w-60 shrink-0 flex-col border-r border-zinc-200 bg-white md:flex">
        {nav}
      </aside>

      {/* Mobile drawer */}
      {navOpen && (
        <div className="fixed inset-0 z-40 md:hidden">
          <div className="absolute inset-0 bg-zinc-900/40" onClick={() => setNavOpen(false)} />
          <aside className="absolute left-0 top-0 flex h-full w-64 flex-col border-r border-zinc-200 bg-white shadow-xl">
            {nav}
          </aside>
        </div>
      )}

      {/* Main */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-10 flex h-14 items-center justify-between gap-3 border-b border-zinc-200 bg-white/80 px-4 backdrop-blur sm:px-6">
          <div className="flex min-w-0 items-center gap-3">
            <button className="grid h-9 w-9 place-items-center rounded-lg text-zinc-600 hover:bg-zinc-100 md:hidden" onClick={() => setNavOpen(true)} aria-label="Menu">
              <Menu size={18} />
            </button>
            <div className="min-w-0">
              <h1 className="truncate text-[15px] font-semibold leading-none">{titles[view]}</h1>
              <p className="mt-1 truncate text-xs text-zinc-500">{subtitle}</p>
            </div>
          </div>
          {view !== 'settings' && (
            <Button variant="primary" onClick={scan} disabled={scanning} className="shrink-0">
              {scanning ? <Loader2 size={15} className="animate-spin" /> : <ScanLine size={15} />}
              <span className="hidden sm:inline">{scanning ? 'Scanning…' : 'Scan site'}</span>
            </Button>
          )}
        </header>

        <main className="flex-1 px-4 py-6 sm:px-6">
          {view === 'settings' ? (
            <SettingsView site={site} scans={scans} model={items.find((i) => i.activeProposal)?.activeProposal?.model} />
          ) : (
            <>
              {/* KPI tiles */}
              <div className="mb-6 grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-4">
                <StatTile label="Awaiting review" value={counts.pending_review} tone="sky" icon={Inbox} />
                <StatTile label="Applied" value={counts.applied} tone="brand" icon={CheckCircle2} />
                <StatTile label="Failed" value={counts.failed} tone="red" icon={AlertTriangle} />
                <StatTile label="Alt coverage" value={`${coverage}%`} tone="zinc" icon={Layers} />
              </div>

              {/* Toolbar: search + bulk */}
              <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                <div className="relative flex-1 sm:max-w-xs">
                  <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
                  <input
                    value={query}
                    onChange={(e) => setQuery(e.target.value)}
                    placeholder="Search filename or alt text…"
                    className="h-9 w-full rounded-lg border border-zinc-200 bg-white pl-9 pr-3 text-sm text-zinc-800 placeholder:text-zinc-400 focus:border-brand-600 focus:outline-none"
                  />
                </div>
                {view === 'pending_review' && selectablePending.length > 0 && (
                  <div className="flex items-center gap-2">
                    <label className="mr-1 flex items-center gap-2 text-sm text-zinc-600">
                      <Checkbox checked={allSelected} indeterminate={!allSelected && someSelected} onChange={toggleSelectAll} aria-label="Select all" />
                      {selectedIds.length > 0 ? <span className="tnum font-medium">{selectedIds.length} selected</span> : <span className="hidden sm:inline">Select all</span>}
                    </label>
                    <Button size="sm" variant="danger" disabled={!selectedIds.length || busy} onClick={() => rejectIds(selectedIds)}>
                      <X size={13} /> Reject
                    </Button>
                    <Button size="sm" variant="primary" disabled={!selectedIds.length || busy} onClick={() => approveIds(selectedIds)}>
                      <Check size={13} /> <span className="hidden sm:inline">Approve</span>
                    </Button>
                  </div>
                )}
              </div>

              {/* List */}
              <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white shadow-sm">
                {visible.length === 0 ? (
                  <EmptyState view={view} query={query} onScan={scan} scanning={scanning} />
                ) : (
                  visible.map((item) => (
                    <MediaReviewRow
                      key={item.id}
                      item={item}
                      selected={selected.has(item.id)}
                      editValue={edits[item.id] ?? item.activeProposal?.proposedAlt ?? ''}
                      onToggleSelect={toggleSelect}
                      onEdit={setEdit}
                      onApprove={(id) => approveIds([id])}
                      onReject={(id) => rejectIds([id])}
                      onRegenerate={regenerate}
                      onRevert={revert}
                    />
                  ))
                )}
              </div>

              <p className="mt-3 text-xs text-zinc-400">
                Nothing is written to your site until you approve it. Every change is reversible.
              </p>
            </>
          )}
        </main>
      </div>

      <ToastStack toasts={toasts} onDismiss={dismiss} />
    </div>
  )
}

function SidebarContent({ view, counts, site, onGo, onLogout, username }: {
  view: View
  counts: Record<string, number>
  site: Site | null
  onGo: (v: View) => void
  onLogout: () => void
  username: string
}) {
  const host = site?.url ? site.url.replace(/^https?:\/\//, '').replace(/\/$/, '') : null
  return (
    <>
      <div className="px-5 py-5">
        <Logo />
      </div>

      <nav className="flex flex-1 flex-col gap-0.5 px-3">
        {NAV.map((v) => {
          const Icon = v.icon
          const active = v.key === view
          const badge = v.key === 'all' ? counts.all : counts[v.key]
          return (
            <button
              key={v.key}
              onClick={() => onGo(v.key)}
              className={cn(
                'flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors duration-150',
                active ? 'bg-brand-50 text-brand-800' : 'text-zinc-600 hover:bg-zinc-100',
              )}
            >
              <Icon size={16} className={active ? 'text-brand-700' : 'text-zinc-400'} />
              <span className="flex-1 text-left">{v.label}</span>
              {badge > 0 && (
                <span className={cn('tnum rounded-full px-1.5 py-0.5 text-[11px]',
                  active ? 'bg-white text-brand-800' : 'bg-zinc-100 text-zinc-500')}>{badge}</span>
              )}
            </button>
          )
        })}

        <div className="my-2 border-t border-zinc-100" />
        <button
          onClick={() => onGo('settings')}
          className={cn(
            'flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors duration-150',
            view === 'settings' ? 'bg-brand-50 text-brand-800' : 'text-zinc-600 hover:bg-zinc-100',
          )}
        >
          <Settings size={16} className={view === 'settings' ? 'text-brand-700' : 'text-zinc-400'} /> Settings
        </button>
      </nav>

      <button
        onClick={() => onGo('settings')}
        className="mx-3 mt-3 rounded-lg border border-zinc-200 bg-zinc-50 p-3 text-left transition-colors hover:border-zinc-300 hover:bg-zinc-100"
      >
        <div className="flex items-center gap-1.5 text-xs font-medium text-zinc-700">
          <ShieldCheck size={13} className="text-brand-700" />
          <span className="truncate">{host ?? 'Not connected'}</span>
        </div>
        <div className="mt-0.5 text-[11px] text-zinc-500">
          {host ? 'Connected · reads media only' : 'Connect a site in Settings'}
        </div>
      </button>

      {/* Account + sign out */}
      <div className="m-3 flex items-center justify-between gap-2 rounded-lg px-1">
        <span className="flex min-w-0 items-center gap-2 text-xs text-zinc-500">
          <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-brand-100 text-[11px] font-semibold text-brand-800">
            {username.slice(0, 1).toUpperCase()}
          </span>
          <span className="truncate">{username}</span>
        </span>
        <button
          onClick={onLogout}
          className="flex shrink-0 items-center gap-1 rounded-md px-2 py-1 text-xs text-zinc-500 transition-colors hover:bg-zinc-100 hover:text-zinc-700"
          title="Sign out"
        >
          <LogOut size={13} /> Sign out
        </button>
      </div>
    </>
  )
}

const toneMap = {
  sky: 'text-sky-700 bg-sky-50 border-sky-100',
  brand: 'text-brand-800 bg-brand-50 border-brand-100',
  red: 'text-red-700 bg-red-50 border-red-100',
  zinc: 'text-zinc-700 bg-zinc-50 border-zinc-200',
} as const

function StatTile({ label, value, tone, icon: Icon }:
  { label: string; value: number | string; tone: keyof typeof toneMap; icon: typeof Inbox }) {
  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <span className="text-xs font-medium uppercase tracking-wide text-zinc-500">{label}</span>
        <span className={cn('grid h-7 w-7 place-items-center rounded-lg border', toneMap[tone])}>
          <Icon size={14} />
        </span>
      </div>
      <div className="tnum mt-2 text-2xl font-semibold text-zinc-900">{value}</div>
    </div>
  )
}

function EmptyState({ view, query, onScan, scanning }:
  { view: View; query: string; onScan: () => void; scanning: boolean }) {
  if (query) {
    return (
      <div className="flex flex-col items-center gap-2 px-4 py-16 text-center">
        <span className="grid h-11 w-11 place-items-center rounded-full bg-zinc-100 text-zinc-400"><Search size={20} /></span>
        <p className="text-sm font-medium text-zinc-700">No matches for “{query}”</p>
        <p className="max-w-xs text-xs text-zinc-500">Try a different filename or word from the alt text.</p>
      </div>
    )
  }
  const review = view === 'pending_review'
  return (
    <div className="flex flex-col items-center gap-3 px-4 py-16 text-center">
      <span className="grid h-14 w-14 place-items-center rounded-2xl bg-brand-50 text-brand-700">
        {review ? <LogoMark px={30} /> : <ImageOff size={22} />}
      </span>
      <p className="text-sm font-semibold text-zinc-800">
        {review ? 'Your review queue is clear' : 'Nothing here yet'}
      </p>
      <p className="max-w-sm text-xs leading-relaxed text-zinc-500">
        {review
          ? 'Run a scan to find images on your site that are missing alt text — Depicta drafts a description for each, ready for your approval.'
          : 'Approved images will appear here once you apply them.'}
      </p>
      {review && (
        <Button variant="primary" onClick={onScan} disabled={scanning} className="mt-1">
          {scanning ? <Loader2 size={15} className="animate-spin" /> : <ScanLine size={15} />}
          {scanning ? 'Scanning…' : 'Scan site'}
        </Button>
      )}
    </div>
  )
}
