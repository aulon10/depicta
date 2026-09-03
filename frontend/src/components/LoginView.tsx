import { useState } from 'react'
import { Loader2, Lock, ShieldCheck, AlertTriangle } from 'lucide-react'
import { Logo } from './Logo'
import { Button } from './ui/Button'
import { cn } from '../lib/cn'

/**
 * Admin sign-in. Exchanges username + password for a bearer token via the ApiClient.
 * Shown whenever there's no active session; on success the app swaps to the dashboard.
 */
export function LoginView({
  onSubmit,
  demoHint,
}: {
  onSubmit: (username: string, password: string) => Promise<void>
  demoHint?: boolean
}) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await onSubmit(username.trim(), password)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Sign in failed.')
      setBusy(false)
    }
  }

  return (
    <div className="grid min-h-screen place-items-center bg-bg px-4">
      <div className="w-full max-w-sm">
        <div className="mb-6 flex justify-center">
          <Logo size="lg" />
        </div>

        <form
          onSubmit={submit}
          className="rounded-2xl border border-line bg-surface p-6 shadow-sm"
        >
          <h1 className="text-center text-lg font-semibold text-fg">Sign in</h1>
          <p className="mb-5 mt-1 text-center text-sm text-fg-muted">
            Admin access to your review queue.
          </p>

          <label className="mb-1 block text-[13px] font-medium text-fg" htmlFor="username">
            Username
          </label>
          <input
            id="username"
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            className="mb-3 w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-fg focus:border-brand-500 focus:outline-none"
            required
          />

          <label className="mb-1 block text-[13px] font-medium text-fg" htmlFor="password">
            Password
          </label>
          <div className="relative mb-4">
            <Lock size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-fg-subtle" />
            <input
              id="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-lg border border-line bg-surface px-3 py-2 pl-9 text-sm text-fg focus:border-brand-500 focus:outline-none"
              required
            />
          </div>

          {error && (
            <div className="mb-4 flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-[13px] text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300">
              <AlertTriangle size={14} className="shrink-0" /> {error}
            </div>
          )}

          <Button
            type="submit"
            variant="primary"
            size="md"
            disabled={busy || !username || !password}
            className={cn('w-full')}
          >
            {busy ? <Loader2 size={15} className="animate-spin" /> : <ShieldCheck size={15} />}
            {busy ? 'Signing in…' : 'Sign in'}
          </Button>

          {demoHint && (
            <p className="mt-4 text-center text-xs text-fg-subtle">
              Demo: <span className="font-mono text-fg-muted">admin</span> ·{' '}
              <span className="font-mono text-fg-muted">depicta-dev-2026</span>
            </p>
          )}
        </form>

        <p className="mt-5 flex items-center justify-center gap-1.5 text-xs text-fg-subtle">
          <ShieldCheck size={13} /> Tokens are short-lived and never leave this browser.
        </p>
      </div>
    </div>
  )
}
