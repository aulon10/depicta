// Mirrors the Phase 2 backend contracts (Contracts/Dtos.cs). When the real API lands,
// only src/api/mockClient.ts is swapped for an HTTP client — these types stay.

export type MediaItemState =
  | 'unprocessed'
  | 'processing'
  | 'pending_review'
  | 'approved'
  | 'applied'
  | 'rejected'
  | 'reverted'
  | 'failed'

export type ScanRunStatus = 'running' | 'completed' | 'failed'

export interface Proposal {
  id: string
  proposedAlt: string
  editedAlt?: string
  provider: string
  model: string
  confidence?: number
  createdAt: string
}

export interface MediaItem {
  id: string
  wpAttachmentId: number
  url: string
  filename?: string
  width?: number
  height?: number
  currentAlt?: string
  state: MediaItemState
  activeProposal?: Proposal
  lastError?: string
  updatedAt: string
}

export interface ScanRun {
  id: string
  status: ScanRunStatus
  discovered: number
  alreadyHadAlt: number
  startedAt: string
  completedAt?: string
  error?: string
}

export interface Site {
  id: string
  url: string
  wpUsername: string
  lastScannedAt?: string
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface AuthSession {
  token: string
  expiresAt: string
  username: string
}

export interface ApproveItem {
  mediaItemId: string
  editedAlt?: string
}

export interface BulkActionResult {
  succeeded: string[]
  failed: { mediaItemId: string; reason: string }[]
}
