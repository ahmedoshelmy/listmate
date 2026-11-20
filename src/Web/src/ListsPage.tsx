import { useCallback, useEffect, useState } from 'react'
import { ApiError, type Api } from './api'
import { listsCache } from './storage'
import type { ListSummary } from './types'

interface Props {
  api: Api
  onOpen: (list: ListSummary) => void
  onUnauthorized: () => void
}

export function ListsPage({ api, onOpen, onUnauthorized }: Props) {
  const [lists, setLists] = useState<ListSummary[] | null>(null)
  const [name, setName] = useState('')
  const [code, setCode] = useState('')
  const [error, setError] = useState<string | null>(null)

  const run = useCallback(
    async (action: () => Promise<void>) => {
      setError(null)
      try {
        await action()
      } catch (e) {
        if (e instanceof ApiError && e.status === 401) onUnauthorized()
        else setError(e instanceof Error ? e.message : 'Something went wrong.')
      }
    },
    [onUnauthorized],
  )

  useEffect(() => {
    let cancelled = false
    void listsCache.load().then(async (cached) => {
      if (cancelled) return
      if (cached) setLists(cached)
      try {
        const fresh = await api.lists()
        if (cancelled) return
        setLists(fresh)
        await listsCache.save(fresh)
      } catch (e) {
        // Offline with a cached copy: show it quietly; otherwise report.
        if (!cached || e instanceof ApiError) void run(() => Promise.reject(e))
      }
    })
    return () => {
      cancelled = true
    }
  }, [api, run])

  return (
    <section className="stack">
      <h2>Your lists</h2>
      {lists === null && <p className="muted">Loading…</p>}
      {lists?.length === 0 && <p className="muted">No lists yet. Create one or join with a code.</p>}
      <ul className="cards">
        {lists?.map((l) => (
          <li key={l.id}>
            <button className="card" onClick={() => onOpen(l)}>
              <strong>{l.name}</strong>
              <span className="muted">
                {l.memberCount} {l.memberCount === 1 ? 'member' : 'members'}
              </span>
            </button>
          </li>
        ))}
      </ul>
      <form
        className="row"
        onSubmit={(e) => {
          e.preventDefault()
          void run(async () => {
            const created = await api.createList(name)
            setName('')
            onOpen(created)
          })
        }}
      >
        <input placeholder="New list, e.g. Weekly groceries" value={name} onChange={(e) => setName(e.target.value)} required maxLength={80} />
        <button type="submit">Create</button>
      </form>
      <form
        className="row"
        onSubmit={(e) => {
          e.preventDefault()
          void run(async () => onOpen(await api.joinList(code)))
        }}
      >
        <input placeholder="Invite code" value={code} onChange={(e) => setCode(e.target.value)} required />
        <button type="submit" className="secondary">
          Join
        </button>
      </form>
      {error && <p className="error">{error}</p>}
    </section>
  )
}
