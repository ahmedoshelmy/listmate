import { PermanentSyncError } from './outbox'
import type { ListChanges, ListSnapshot, ListSummary, Member, Mutation, SyncResponse } from './types'

const TOKEN_KEY = 'listmate.session'

export interface Session {
  email: string
  accessToken: string
}

export class ApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message)
  }
}

export function loadSession(): Session | null {
  try {
    const raw = localStorage.getItem(TOKEN_KEY)
    return raw ? (JSON.parse(raw) as Session) : null
  } catch {
    return null
  }
}

export function saveSession(session: Session | null) {
  if (session) localStorage.setItem(TOKEN_KEY, JSON.stringify(session))
  else localStorage.removeItem(TOKEN_KEY)
}

async function request<T>(path: string, init: RequestInit = {}, token?: string): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body) headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const response = await fetch(path, { ...init, headers })
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as { title?: string; detail?: string } | null
    throw new ApiError(response.status, problem?.detail ?? problem?.title ?? response.statusText)
  }
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export async function signIn(email: string, password: string, register: boolean): Promise<Session> {
  const body = JSON.stringify({ email, password })
  if (register) await request('/api/auth/register', { method: 'POST', body })
  const login = await request<{ accessToken: string }>('/api/auth/login', { method: 'POST', body })
  return { email, accessToken: login.accessToken }
}

export function createApi(token: string) {
  return {
    lists: () => request<ListSummary[]>('/api/lists', {}, token),
    createList: (name: string) => request<ListSummary>('/api/lists', { method: 'POST', body: JSON.stringify({ name }) }, token),
    joinList: (inviteCode: string) =>
      request<ListSummary>('/api/lists/join', { method: 'POST', body: JSON.stringify({ inviteCode }) }, token),
    snapshot: (listId: string) => request<ListSnapshot>(`/api/lists/${listId}`, {}, token),
    changes: (listId: string, since: number) => request<ListChanges>(`/api/lists/${listId}/changes?since=${since}`, {}, token),
    members: (listId: string) => request<Member[]>(`/api/lists/${listId}/members`, {}, token),
    leave: (listId: string) => request<void>(`/api/lists/${listId}/leave`, { method: 'POST' }, token),
    removeMember: (listId: string, userId: string) =>
      request<void>(`/api/lists/${listId}/members/${encodeURIComponent(userId)}`, { method: 'DELETE' }, token),
    mergeDuplicates: (listId: string) => request<ListChanges>(`/api/lists/${listId}/merge-duplicates`, { method: 'POST' }, token),
    sync: async (mutations: Mutation[]) => {
      try {
        return await request<SyncResponse>('/api/sync', { method: 'POST', body: JSON.stringify({ mutations }) }, token)
      } catch (error) {
        if (error instanceof ApiError && error.status === 400) throw new PermanentSyncError(error.message)
        throw error
      }
    },
  }
}

export type Api = ReturnType<typeof createApi>
