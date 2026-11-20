import { useCallback, useEffect, useState } from 'react'
import { ApiError, type Api } from './api'
import type { Member } from './types'

interface Props {
  api: Api
  listId: string
  online: boolean
  /** Bumped when the server says membership changed. */
  refreshKey: number
  /** This person is no longer on the list (left, or removed by the owner). */
  onGone: () => void
}

/** Who is on the list; the owner can remove people, anyone can leave. Needs a connection: membership is not queued. */
export function Members({ api, listId, online, refreshKey, onGone }: Props) {
  const [members, setMembers] = useState<Member[]>([])
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    try {
      setMembers(await api.members(listId))
      setError(null)
    } catch (e) {
      if (e instanceof ApiError && e.status === 404) onGone()
    }
  }, [api, listId, onGone])

  useEffect(() => {
    if (online) void load()
  }, [online, load, refreshKey])

  const run = async (action: () => Promise<void>) => {
    try {
      await action()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Something went wrong.')
    }
  }

  const iOwn = members.some((m) => m.isYou && m.role === 'Owner')

  const leave = () =>
    run(async () => {
      if (!window.confirm('Leave this list? You will need a new invite code to come back.')) return
      await api.leave(listId)
      onGone()
    })

  const remove = (member: Member) =>
    run(async () => {
      if (!window.confirm(`Remove ${member.name} from this list?`)) return
      await api.removeMember(listId, member.userId)
      await load()
    })

  return (
    <details className="members">
      <summary>Members ({members.length})</summary>
      <ul>
        {members.map((m) => (
          <li key={m.userId} className="row spread">
            <span>
              {m.name}
              {m.isYou && ' (you)'} <span className="muted small">{m.role.toLowerCase()}</span>
            </span>
            {iOwn && !m.isYou && (
              <button className="link" disabled={!online} onClick={() => void remove(m)}>
                Remove
              </button>
            )}
          </li>
        ))}
      </ul>
      {error && <p className="error">{error}</p>}
      <button className="secondary" disabled={!online} onClick={() => void leave()}>
        Leave list
      </button>
    </details>
  )
}
