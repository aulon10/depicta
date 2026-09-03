import type { ApiClient } from './client'
import { UnauthorizedError } from './client'
import type {
  ApproveItem, AuthSession, BulkActionResult, MediaItem, MediaItemState, PagedResult, ScanRun, Site,
} from './types'

// --- tiny SVG placeholder "photos" so the review list looks real without network ---
function img(bg: string, mid: string, accent: string, kind: 'hills' | 'city' | 'portrait' | 'food' | 'abstract'): string {
  const scenes: Record<string, string> = {
    hills: `<circle cx="150" cy="55" r="26" fill="${accent}"/><path d="M0 150 L60 80 L120 150Z" fill="${mid}"/><path d="M80 150 L150 65 L210 150Z" fill="${accent}" opacity=".55"/>`,
    city: `<rect x="30" y="70" width="22" height="80" fill="${mid}"/><rect x="60" y="50" width="26" height="100" fill="${accent}" opacity=".7"/><rect x="95" y="85" width="20" height="65" fill="${mid}"/><rect x="122" y="60" width="28" height="90" fill="${accent}" opacity=".6"/>`,
    portrait: `<circle cx="100" cy="70" r="30" fill="${accent}" opacity=".8"/><path d="M55 150 C55 110 145 110 145 150Z" fill="${mid}"/>`,
    food: `<circle cx="100" cy="95" r="46" fill="${mid}"/><circle cx="100" cy="95" r="30" fill="${accent}" opacity=".7"/>`,
    abstract: `<circle cx="70" cy="70" r="34" fill="${accent}" opacity=".7"/><rect x="95" y="80" width="70" height="60" rx="10" fill="${mid}"/>`,
  }
  const svg = `<svg xmlns='http://www.w3.org/2000/svg' width='200' height='150' viewBox='0 0 200 150'><rect width='200' height='150' fill='${bg}'/>${scenes[kind]}</svg>`
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`
}

const now = () => new Date().toISOString()

function seed(): MediaItem[] {
  const p = (alt: string, conf: number) => ({
    id: crypto.randomUUID(), proposedAlt: alt, provider: 'claude', model: 'claude-vision',
    confidence: conf, createdAt: now(),
  })
  return [
    {
      id: crypto.randomUUID(), wpAttachmentId: 482, url: img('#e8efe9', '#5c6f63', '#0f9d70', 'hills'),
      filename: 'lake-sunrise.jpg', width: 1600, height: 900, currentAlt: '',
      state: 'pending_review', updatedAt: now(),
      activeProposal: p('Calm mountain lake at sunrise, framed by pine trees', 0.94),
    },
    {
      id: crypto.randomUUID(), wpAttachmentId: 501, url: img('#eef1f5', '#64748b', '#0ea5a0', 'city'),
      filename: 'office-exterior.png', width: 2000, height: 1333, currentAlt: '',
      state: 'pending_review', updatedAt: now(),
      activeProposal: p('Glass office building downtown on a clear afternoon', 0.88),
    },
    {
      id: crypto.randomUUID(), wpAttachmentId: 517, url: img('#f4eee6', '#b08968', '#0f9d70', 'food'),
      filename: 'IMG_2381.jpg', width: 1200, height: 1200, currentAlt: '',
      state: 'pending_review', updatedAt: now(),
      activeProposal: p('Bowl of ramen with soft-boiled egg and scallions', 0.91),
    },
    {
      id: crypto.randomUUID(), wpAttachmentId: 522, url: img('#efe9f0', '#8a6f8f', '#0f9d70', 'portrait'),
      filename: 'team-jane.jpg', width: 800, height: 1000, currentAlt: '',
      state: 'pending_review', updatedAt: now(),
      activeProposal: p('Smiling person in a dark blazer against a plain wall', 0.82),
    },
    {
      id: crypto.randomUUID(), wpAttachmentId: 540, url: img('#e9eff0', '#5f8f8a', '#0f9d70', 'abstract'),
      filename: 'hero-bg.webp', width: 2400, height: 1200, currentAlt: '',
      state: 'processing', updatedAt: now(),
    },
    {
      id: crypto.randomUUID(), wpAttachmentId: 555, url: img('#eef1ee', '#6b7f70', '#0f9d70', 'hills'),
      filename: 'trail-map.png', width: 1024, height: 768, currentAlt: 'Trail map',
      state: 'applied', updatedAt: now(),
      activeProposal: p('Hiking trail map showing three marked loop routes', 0.9),
    },
    {
      id: crypto.randomUUID(), wpAttachmentId: 560, url: img('#f5ecec', '#a86b6b', '#0f9d70', 'abstract'),
      filename: 'broken-source.gif', width: 600, height: 400, currentAlt: '',
      state: 'failed', lastError: 'Vision provider timed out', updatedAt: now(),
    },
  ]
}

/** In-memory implementation of ApiClient with simulated latency + state transitions. */
export class MockApiClient implements ApiClient {
  private items: MediaItem[] = seed()
  private previousAlt = new Map<string, string>()
  private scans: ScanRun[] = [
    { id: crypto.randomUUID(), status: 'completed', discovered: 7, alreadyHadAlt: 12, startedAt: new Date(Date.now() - 3600e3).toISOString(), completedAt: new Date(Date.now() - 3595e3).toISOString() },
  ]

  private delay<T>(value: T, ms = 320): Promise<T> {
    return new Promise((res) => setTimeout(() => res(value), ms))
  }

  setToken(): void {
    // The mock has no real auth; the token is irrelevant here.
  }

  async login(username: string, password: string): Promise<AuthSession> {
    // Mirror the backend's dev credentials so the login screen behaves realistically offline.
    await this.delay(null, 350)
    if (username !== 'admin' || password !== 'depicta-dev-2026') {
      throw new UnauthorizedError('Invalid username or password.')
    }
    return { token: 'mock-token', expiresAt: new Date(Date.now() + 2 * 3600e3).toISOString(), username }
  }

  async getSite(): Promise<Site | null> {
    return this.delay({ id: crypto.randomUUID(), url: 'https://demo-site.com', wpUsername: 'admin', lastScannedAt: now() }, 200)
  }

  async listScans(take = 10): Promise<ScanRun[]> {
    return this.delay(this.scans.slice(0, take))
  }

  async startScan(): Promise<ScanRun> {
    // pretend a scan discovers the two "processing" items into pending_review shortly after
    const run: ScanRun = {
      id: crypto.randomUUID(), status: 'completed',
      discovered: this.items.filter((i) => i.state === 'processing').length, alreadyHadAlt: 12,
      startedAt: now(), completedAt: now(),
    }
    this.scans.unshift(run)
    setTimeout(() => {
      this.items = this.items.map((i) =>
        i.state === 'processing'
          ? { ...i, state: 'pending_review', updatedAt: now(),
              activeProposal: { id: crypto.randomUUID(), proposedAlt: 'Abstract green gradient hero background', provider: 'claude', model: 'claude-vision', confidence: 0.79, createdAt: now() } }
          : i)
    }, 1200)
    return this.delay(run, 600)
  }

  async listMedia(params: { state?: MediaItemState; page?: number; pageSize?: number }): Promise<PagedResult<MediaItem>> {
    const page = params.page ?? 1
    const pageSize = params.pageSize ?? 50
    const filtered = params.state ? this.items.filter((i) => i.state === params.state) : this.items
    const start = (page - 1) * pageSize
    return this.delay({ items: filtered.slice(start, start + pageSize), total: filtered.length, page, pageSize })
  }

  async approve(items: ApproveItem[]): Promise<BulkActionResult> {
    const ok: string[] = []
    for (const a of items) {
      const it = this.items.find((i) => i.id === a.mediaItemId)
      if (!it || (it.state !== 'pending_review' && it.state !== 'reverted')) continue
      this.previousAlt.set(it.id, it.currentAlt ?? '')
      const written = a.editedAlt?.trim() || it.activeProposal?.proposedAlt || ''
      it.currentAlt = written
      it.state = 'applied'
      it.updatedAt = now()
      if (it.activeProposal && a.editedAlt) it.activeProposal.editedAlt = a.editedAlt
      ok.push(it.id)
    }
    return this.delay({ succeeded: ok, failed: [] })
  }

  async reject(ids: string[]): Promise<BulkActionResult> {
    const ok: string[] = []
    for (const id of ids) {
      const it = this.items.find((i) => i.id === id)
      if (!it || it.state !== 'pending_review') continue
      it.state = 'rejected'; it.updatedAt = now(); ok.push(id)
    }
    return this.delay({ succeeded: ok, failed: [] })
  }

  async regenerate(ids: string[]): Promise<BulkActionResult> {
    const ok: string[] = []
    for (const id of ids) {
      const it = this.items.find((i) => i.id === id)
      if (!it) continue
      it.state = 'processing'; it.lastError = undefined; it.updatedAt = now(); ok.push(id)
      setTimeout(() => {
        it.state = 'pending_review'; it.updatedAt = now()
        it.activeProposal = { id: crypto.randomUUID(), proposedAlt: 'Regenerated: descriptive alt text for this image', provider: 'claude', model: 'claude-vision', confidence: 0.86, createdAt: now() }
      }, 1000)
    }
    return this.delay({ succeeded: ok, failed: [] })
  }

  async revert(id: string): Promise<MediaItem> {
    const it = this.items.find((i) => i.id === id)
    if (!it || it.state !== 'applied') throw new Error('Not revertable')
    it.currentAlt = this.previousAlt.get(id) ?? ''
    it.state = 'reverted'; it.updatedAt = now()
    return this.delay({ ...it })
  }
}
