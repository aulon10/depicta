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
import { ThemeToggle } from './components/ThemeToggle'
import { Logo, LogoMark } from './components/Logo'
import { ToastStack, type ToastData } from './components/ui/Toast'
import { useTheme } from './lib/useTheme'
import { cn } from './lib/cn'

const SESSION_KEY = 'depicta.session'

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
  const { theme, toggle } = useTheme()

  const [session, setSession] = useState<AuthSession | null>(null)
  const [items, setItems] = useState<MediaItem[]>([])
  const [site, setSite] = useState<Site | null>(null)
  const [scans, setScans] = useState<ScanRun[]>([])
  const [view, setView] = useState<View>('pending_review')
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [edits, setEdits] = useState<Record<string, string>>({})
  const [query, setQuery] = useState('')
  const [loading, setLoading] = useState(true)
  const [scanning, setScanning] = useState(false)
  const [busy, setBusy] = useState(false)
  const [navOpen, setNavOpen] = useState(false)
  const [toasts, setToasts] = useState<ToastData[]>([])

  const toast = useCallback((message: string, tone?: 'success' | 'error') => {
    setToasts((t) => [...t, { id: crypto.randomUUID(), message, tone }])
  }, [])
  const dismiss = useCallback((id: string) => setToasts((t) => t.filter((x) => x.id !== id)), [])

  // Persist the session in sessionStorage: it survives a refresh in the same tab, is cleared when
  // the tab closes, and stays out of localStorage (shorter window of exposure if the page is ever
  // hit by XSS). Wrapped in try/catch because storage can be unavailable/throw in some contexts.
  const persist = useCallback((s: AuthSession | null) => {
    try {
      if (s) sessionStorage.setItem(SESSION_KEY, JSON.stringify(s))
      else sessionStorage.removeItem(SESSION_KEY)
    } catch { /* storage unavailable — the session simply stays in memory only */ }
  }, [])

  // Any call that comes back 401 means the token is gone/expired: drop to the login screen.
  const handleAuthError = useCallback((err: unknown): boolean => {
    if (err instanceof UnauthorizedError) {
      client.setToken(null)
      setSession(null)
      persist(null)
      toast('Session expired — please sign in again', 'error')
      return true
    }
    return false
  }, [client, toast, persist])

  const login = useCallback(async (username: string, password: string) => {
    const s = await client.login(username, password) // throws on bad credentials; LoginView shows it
    client.setToken(s.token)
    setLoading(true)
    setSession(s)
    persist(s)
  }, [client, persist])

  const logout = useCallback(() => {
    client.setToken(null)
    setSession(null)
    persist(null)
    setItems([]); setSite(null); setScans([]); setSelected(new Set()); setEdits({})
    setView('pending_review'); setNavOpen(false); setLoading(true)
  }, [client, persist])

  // On first load, restore a still-valid session so a refresh doesn't force a re-login.
  useEffect(() => {
    try {
      const raw = sessionStorage.getItem(SESSION_KEY)
      if (!raw) return
      const s = JSON.parse(raw) as AuthSession
      if (s?.token && new Date(s.expiresAt).getTime() > Date.now()) {
        client.setToken(s.token)
        setSession(s)
      } else {
        sessionStorage.removeItem(SESSION_KEY) // expired — discard
      }
    } catch { /* ignore malformed/unavailable storage */ }
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
    } finally {
      setLoading(false)
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
  const showSkeleton = view !== 'settings' && loading && items.length === 0

  return (
    <div className="flex min-h-screen bg-bg text-fg">
      {/* Desktop sidebar */}
      <aside className="sticky top-0 hidden h-screen w-60 shrink-0 flex-col border-r border-line bg-surface md:flex">
        {nav}
      </aside>

      {/* Mobile drawer */}
      {navOpen && (
        <div className="fixed inset-0 z-40 md:hidden">
          <div className="absolute inset-0 bg-zinc-950/50" onClick={() => setNavOpen(false)} />
          <aside className="absolute left-0 top-0 flex h-full w-64 flex-col border-r border-line bg-surface shadow-xl">
            {nav}
          </aside>
        </div>
      )}

      {/* Main */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-10 flex h-14 items-center justify-between gap-3 border-b border-line bg-surface/80 px-4 backdrop-blur sm:px-6">
          <div className="flex min-w-0 items-center gap-3">
            <button className="grid h-9 w-9 place-items-center rounded-lg text-fg-muted hover:bg-surface-2 md:hidden" onClick={() => setNavOpen(true)} aria-label="Menu">
              <Menu size={18} />
            </button>
            <div className="min-w-0">
              <h1 className="truncate text-[15px] font-semibold leading-none">{titles[view]}</h1>
              <p className="mt-1 truncate text-xs text-fg-muted">{subtitle}</p>
            </div>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <ThemeToggle theme={theme} onToggle={toggle} />
            {view !== 'settings' && (
              <Button variant="primary" onClick={scan} disabled={scanning}>
                {scanning ? <Loader2 size={15} className="animate-spin" /> : <ScanLine size={15} />}
                <span className="hidden sm:inline">{scanning ? 'Scanning…' : 'Scan site'}</span>
              </Button>
            )}
          </div>
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
                <CoverageTile pct={coverage} />
              </div>

              {/* Toolbar: search + bulk */}
              <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                <div className="relative flex-1 sm:max-w-xs">
                  <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-fg-subtle" />
                  <input
                    value={query}
                    onChange={(e) => setQuery(e.target.value)}
                    placeholder="Search filename or alt text…"
                    className="h-9 w-full rounded-lg border border-line bg-surface pl-9 pr-3 text-sm text-fg placeholder:text-fg-subtle focus:border-brand-500 focus:outline-none"
                  />
                </div>
                {view === 'pending_review' && selectablePending.length > 0 && (
                  <div className="flex items-center gap-2">
                    <label className="mr-1 flex items-center gap-2 text-sm text-fg-muted">
                      <Checkbox checked={allSelected} indeterminate={!allSelected && someSelected} onChange={toggleSelectAll} aria-label="Select all" />
                      {selectedIds.length > 0 ? <span className="tnum font-medium text-fg">{selectedIds.length} selected</span> : <span className="hidden sm:inline">Select all</span>}
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
              <div className="overflow-hidden rounded-xl border border-line bg-surface shadow-sm">
                {showSkeleton
                  ? Array.from({ length: 4 }).map((_, i) => <SkeletonRow key={i} />)
                  : visible.length === 0
                    ? <EmptyState view={view} query={query} onScan={scan} scanning={scanning} />
                    : visible.map((item) => (
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
                      ))}
              </div>

              <p className="mt-3 text-xs text-fg-subtle">
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
  const navItem = (active: boolean) => cn(
    'flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors duration-150',
    active ? 'bg-brand-50 text-brand-800 dark:bg-brand-500/15 dark:text-brand-300' : 'text-fg-muted hover:bg-surface-2 hover:text-fg',
  )
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
            <button key={v.key} onClick={() => onGo(v.key)} className={navItem(active)}>
              <Icon size={16} className={active ? 'text-brand-700 dark:text-brand-400' : 'text-fg-subtle'} />
              <span className="flex-1 text-left">{v.label}</span>
              {badge > 0 && (
                <span className={cn('tnum rounded-full px-1.5 py-0.5 text-[11px]',
                  active ? 'bg-white text-brand-800 dark:bg-brand-500/25 dark:text-brand-200' : 'bg-surface-2 text-fg-muted')}>{badge}</span>
              )}
            </button>
          )
        })}

        <div className="my-2 border-t border-line" />
        <button onClick={() => onGo('settings')} className={navItem(view === 'settings')}>
          <Settings size={16} className={view === 'settings' ? 'text-brand-700 dark:text-brand-400' : 'text-fg-subtle'} /> Settings
        </button>
      </nav>

      <button
        onClick={() => onGo('settings')}
        className="mx-3 mt-3 rounded-lg border border-line bg-surface-2 p-3 text-left transition-colors hover:border-line-strong"
      >
        <div className="flex items-center gap-1.5 text-xs font-medium text-fg">
          <ShieldCheck size={13} className="text-brand-600" />
          <span className="truncate">{host ?? 'Not connected'}</span>
        </div>
        <div className="mt-0.5 text-[11px] text-fg-muted">
          {host ? 'Connected · reads media only' : 'Connect a site in Settings'}
        </div>
      </button>

      {/* Account + sign out */}
      <div className="m-3 flex items-center justify-between gap-2 rounded-lg px-1">
        <span className="flex min-w-0 items-center gap-2 text-xs text-fg-muted">
          <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-brand-100 text-[11px] font-semibold text-brand-800 dark:bg-brand-500/20 dark:text-brand-300">
            {username.slice(0, 1).toUpperCase()}
          </span>
          <span className="truncate">{username}</span>
        </span>
        <button
          onClick={onLogout}
          className="flex shrink-0 items-center gap-1 rounded-md px-2 py-1 text-xs text-fg-muted transition-colors hover:bg-surface-2 hover:text-fg"
          title="Sign out"
        >
          <LogOut size={13} /> Sign out
        </button>
      </div>
    </>
  )
}

const toneMap = {
  sky: 'text-sky-700 bg-sky-50 border-sky-100 dark:text-sky-300 dark:bg-sky-500/15 dark:border-sky-500/20',
  brand: 'text-brand-800 bg-brand-50 border-brand-100 dark:text-brand-300 dark:bg-brand-500/15 dark:border-brand-500/20',
  red: 'text-red-700 bg-red-50 border-red-100 dark:text-red-300 dark:bg-red-500/15 dark:border-red-500/20',
} as const

function StatTile({ label, value, tone, icon: Icon }:
  { label: string; value: number | string; tone: keyof typeof toneMap; icon: typeof Inbox }) {
  return (
    <div className="flex items-center gap-3 rounded-xl border border-line bg-surface p-4 shadow-sm">
      <span className={cn('grid h-10 w-10 shrink-0 place-items-center rounded-lg border', toneMap[tone])}>
        <Icon size={18} />
      </span>
      <div className="min-w-0">
        <div className="tnum text-2xl font-semibold leading-none text-fg">{value}</div>
        <div className="mt-1 truncate text-xs font-medium text-fg-muted">{label}</div>
      </div>
    </div>
  )
}

function CoverageTile({ pct }: { pct: number }) {
  const r = 15
  const c = 2 * Math.PI * r
  const off = c * (1 - Math.min(1, Math.max(0, pct / 100)))
  return (
    <div className="flex items-center gap-3 rounded-xl border border-line bg-surface p-4 shadow-sm">
      <svg width="40" height="40" viewBox="0 0 40 40" className="shrink-0 -rotate-90">
        <circle cx="20" cy="20" r={r} fill="none" strokeWidth="4" className="stroke-line" />
        <circle
          cx="20" cy="20" r={r} fill="none" strokeWidth="4" strokeLinecap="round"
          className="stroke-brand-500 transition-[stroke-dashoffset] duration-500"
          strokeDasharray={c} strokeDashoffset={off}
        />
      </svg>
      <div className="min-w-0">
        <div className="tnum text-2xl font-semibold leading-none text-fg">{pct}%</div>
        <div className="mt-1 truncate text-xs font-medium text-fg-muted">Alt coverage</div>
      </div>
    </div>
  )
}

function SkeletonRow() {
  return (
    <div className="flex gap-4 border-b border-line px-4 py-4">
      <div className="skeleton h-[76px] w-[104px] rounded-lg sm:h-[99px] sm:w-[132px]" />
      <div className="flex-1 space-y-2.5 py-1">
        <div className="skeleton h-4 w-28 rounded" />
        <div className="skeleton h-16 w-full rounded-lg" />
        <div className="skeleton h-3 w-40 rounded" />
      </div>
      <div className="hidden w-[112px] shrink-0 space-y-1.5 sm:block">
        <div className="skeleton h-8 w-full rounded-lg" />
        <div className="skeleton h-8 w-full rounded-lg" />
      </div>
    </div>
  )
}

function EmptyState({ view, query, onScan, scanning }:
  { view: View; query: string; onScan: () => void; scanning: boolean }) {
  if (query) {
    return (
      <div className="flex flex-col items-center gap-2 px-4 py-16 text-center">
        <span className="grid h-11 w-11 place-items-center rounded-full bg-surface-2 text-fg-subtle"><Search size={20} /></span>
        <p className="text-sm font-medium text-fg">No matches for “{query}”</p>
        <p className="max-w-xs text-xs text-fg-muted">Try a different filename or word from the alt text.</p>
      </div>
    )
  }
  const review = view === 'pending_review'
  return (
    <div className="flex flex-col items-center gap-3 px-4 py-16 text-center">
      <span className="grid h-14 w-14 place-items-center rounded-2xl bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-brand-300">
        {review ? <LogoMark px={30} /> : <ImageOff size={22} />}
      </span>
      <p className="text-sm font-semibold text-fg">
        {review ? 'Your review queue is clear' : 'Nothing here yet'}
      </p>
      <p className="max-w-sm text-xs leading-relaxed text-fg-muted">
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
