import type { ApiClient } from './client'
import { UnauthorizedError } from './client'
import type {
  ApproveItem, AuthSession, BulkActionResult, MediaItem, MediaItemState, PagedResult, ScanRun, Site,
} from './types'

/**
 * Talks to the real .NET backend. Selected automatically when VITE_API_BASE is set
 * (see App.tsx); otherwise the in-memory MockApiClient is used. Implements the exact same
 * ApiClient interface, so no component code changes between mock and live.
 *
 * Auth: every call except login carries `Authorization: Bearer <token>`. A 401 surfaces as
 * UnauthorizedError so the app can drop back to the login screen.
 */
export class HttpApiClient implements ApiClient {
  private token: string | null = null

  constructor(private readonly base: string) {}

  setToken(token: string | null): void {
    this.token = token
  }

  private headers(json = true): HeadersInit {
    const h: Record<string, string> = {}
    if (json) h['Content-Type'] = 'application/json'
    if (this.token) h['Authorization'] = `Bearer ${this.token}`
    return h
  }

  private async json<T>(path: string, init?: RequestInit): Promise<T> {
    const res = await fetch(this.base.replace(/\/$/, '') + path, {
      ...init,
      headers: { ...this.headers(), ...(init?.headers ?? {}) },
    })
    if (res.status === 401) throw new UnauthorizedError()
    if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
    const text = await res.text()
    return (text ? JSON.parse(text) : undefined) as T
  }

  async login(username: string, password: string): Promise<AuthSession> {
    const res = await fetch(this.base.replace(/\/$/, '') + '/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    })
    if (res.status === 401) throw new UnauthorizedError('Invalid username or password.')
    if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
    const session = (await res.json()) as AuthSession
    this.token = session.token
    return session
  }

  async getSite(): Promise<Site | null> {
    const res = await fetch(this.base.replace(/\/$/, '') + '/api/site', { headers: this.headers(false) })
    if (res.status === 401) throw new UnauthorizedError()
    if (res.status === 404) return null
    if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
    return (await res.json()) as Site
  }

  listScans(take = 10): Promise<ScanRun[]> {
    return this.json<ScanRun[]>(`/api/scans?take=${take}`)
  }

  startScan(): Promise<ScanRun> {
    return this.json<ScanRun>('/api/scans', { method: 'POST' })
  }

  listMedia(params: { state?: MediaItemState; page?: number; pageSize?: number }): Promise<PagedResult<MediaItem>> {
    const page = params.page ?? 1
    const pageSize = params.pageSize ?? 200 // fetch all; the UI filters by view client-side
    return this.json<PagedResult<MediaItem>>(`/api/media?page=${page}&pageSize=${pageSize}`)
  }

  approve(items: ApproveItem[]): Promise<BulkActionResult> {
    return this.json<BulkActionResult>('/api/media/approve', {
      method: 'POST', body: JSON.stringify({ items }),
    })
  }

  reject(ids: string[]): Promise<BulkActionResult> {
    return this.json<BulkActionResult>('/api/media/reject', {
      method: 'POST', body: JSON.stringify({ mediaItemIds: ids }),
    })
  }

  regenerate(ids: string[]): Promise<BulkActionResult> {
    return this.json<BulkActionResult>('/api/media/regenerate', {
      method: 'POST', body: JSON.stringify({ mediaItemIds: ids }),
    })
  }

  revert(id: string): Promise<MediaItem> {
    return this.json<MediaItem>(`/api/media/${id}/revert`, { method: 'POST' })
  }
}
