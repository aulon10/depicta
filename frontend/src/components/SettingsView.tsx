import {
  ShieldCheck, Globe, User, Clock, Sparkles, Lock, Undo2, Eye, History, CheckCircle2, XCircle, Loader2,
} from 'lucide-react'
import type { ScanRun, Site } from '../api/types'
import { cn } from '../lib/cn'

function timeAgo(iso?: string): string {
  if (!iso) return '—'
  const s = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000)
  if (s < 60) return 'just now'
  if (s < 3600) return `${Math.floor(s / 60)}m ago`
  if (s < 86400) return `${Math.floor(s / 3600)}h ago`
  return `${Math.floor(s / 86400)}d ago`
}

export function SettingsView({ site, scans, model }: { site: Site | null; scans: ScanRun[]; model?: string }) {
  return (
    <div className="max-w-5xl">
      <div className="grid gap-6 lg:grid-cols-3">
        {/* Left column: connection + security */}
        <div className="space-y-6 lg:col-span-2">
          <section className="overflow-hidden rounded-xl border border-line bg-surface shadow-sm">
            <div className="border-b border-line px-5 py-3.5">
              <h2 className="text-sm font-semibold text-fg">Connected site</h2>
            </div>
            <div className="divide-y divide-line">
              <Row icon={Globe} label="Site URL" value={site?.url ?? 'Not connected'} mono />
              <Row icon={User} label="WordPress user" value={site?.wpUsername ?? '—'} />
              <Row icon={Clock} label="Last scanned" value={timeAgo(site?.lastScannedAt)} />
              <Row icon={Sparkles} label="Vision model" value={model ?? 'claude-haiku-4-5'} mono />
            </div>
            <div className="flex items-center gap-2 bg-brand-50/70 px-5 py-3 text-xs text-brand-800 dark:bg-brand-500/10 dark:text-brand-300">
              <ShieldCheck size={14} /> Depicta reads your media library only — nothing else on the site.
            </div>
          </section>

          <section className="rounded-xl border border-line bg-surface p-5 shadow-sm">
            <h2 className="mb-4 text-sm font-semibold text-fg">How Depicta stays safe</h2>
            <div className="grid gap-4 sm:grid-cols-3">
              <Guard icon={Eye} title="Owner-approved" body="Nothing is written to your site until you click Approve." />
              <Guard icon={Lock} title="Least-privilege" body="The plugin can only touch alt text — no settings, files, or code." />
              <Guard icon={Undo2} title="Reversible" body="Every change stores the previous value for one-click revert." />
            </div>
          </section>
        </div>

        {/* Right column: scan history */}
        <div className="lg:col-span-1">
          <section className="overflow-hidden rounded-xl border border-line bg-surface shadow-sm">
            <div className="flex items-center gap-2 border-b border-line px-5 py-3.5">
              <History size={15} className="text-fg-subtle" />
              <h2 className="text-sm font-semibold text-fg">Recent scans</h2>
            </div>
            {scans.length === 0 ? (
              <p className="px-5 py-6 text-sm text-fg-muted">No scans yet.</p>
            ) : (
              <ul className="divide-y divide-line">
                {scans.map((s) => (
                  <li key={s.id} className="flex items-center justify-between gap-3 px-5 py-3 text-sm">
                    <span className="flex items-center gap-2.5">
                      <ScanStatus status={s.status} />
                      <span className="text-fg">
                        Found <b className="tnum font-semibold">{s.discovered}</b> image{s.discovered === 1 ? '' : 's'}
                      </span>
                    </span>
                    <span className="tnum shrink-0 text-xs text-fg-subtle">{timeAgo(s.completedAt ?? s.startedAt)}</span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>
      </div>
    </div>
  )
}

function Row({ icon: Icon, label, value, mono }: { icon: typeof Globe; label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex items-center justify-between gap-3 px-5 py-3.5">
      <span className="flex items-center gap-2.5 text-sm text-fg-muted">
        <Icon size={15} className="text-fg-subtle" /> {label}
      </span>
      <span className={cn('truncate text-sm font-medium text-fg', mono && 'font-mono text-[13px]')}>{value}</span>
    </div>
  )
}

function Guard({ icon: Icon, title, body }: { icon: typeof Eye; title: string; body: string }) {
  return (
    <div className="rounded-lg border border-line bg-surface-2/60 p-3.5">
      <span className="mb-2 inline-grid h-8 w-8 place-items-center rounded-lg bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-brand-300">
        <Icon size={16} />
      </span>
      <h3 className="text-[13px] font-semibold text-fg">{title}</h3>
      <p className="mt-0.5 text-xs leading-relaxed text-fg-muted">{body}</p>
    </div>
  )
}

function ScanStatus({ status }: { status: ScanRun['status'] }) {
  if (status === 'completed') return <CheckCircle2 size={15} className="text-brand-600" />
  if (status === 'failed') return <XCircle size={15} className="text-red-500" />
  return <Loader2 size={15} className="animate-spin text-amber-500" />
}
