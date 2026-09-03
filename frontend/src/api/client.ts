import type {
  ApproveItem, AuthSession, BulkActionResult, MediaItem, MediaItemState, PagedResult, ScanRun, Site,
} from './types'

/** Thrown when the API rejects the request as unauthenticated (expired/invalid token). */
export class UnauthorizedError extends Error {
  constructor(message = 'Session expired') {
    super(message)
    this.name = 'UnauthorizedError'
  }
}

/**
 * The contract the UI depends on. MockApiClient implements it now; HttpApiClient (fetch against
 * the .NET backend) implements the same shape. Auth: call login() to obtain a token, then the
 * client attaches it to every request; a 401 from any call throws UnauthorizedError.
 */
export interface ApiClient {
  login(username: string, password: string): Promise<AuthSession>
  /** Restore/attach a previously issued token (or clear it with null). */
  setToken(token: string | null): void

  getSite(): Promise<Site | null>
  startScan(): Promise<ScanRun>
  listScans(take?: number): Promise<ScanRun[]>
  listMedia(params: { state?: MediaItemState; page?: number; pageSize?: number }): Promise<PagedResult<MediaItem>>
  approve(items: ApproveItem[]): Promise<BulkActionResult>
  reject(ids: string[]): Promise<BulkActionResult>
  regenerate(ids: string[]): Promise<BulkActionResult>
  revert(id: string): Promise<MediaItem>
}
