import { useCallback, useState } from 'react'
import type { Api, Session } from './api'
import { formatItem } from './itemText'
import { applyServerItems, visibleItems } from './listState'
import { Members } from './Members'
import type { ListSummary } from './types'
import { useListSync } from './useListSync'
import { useOnline } from './useOnline'

interface Props {
  api: Api
  session: Session
  list: ListSummary
  onClose: () => void
}

const newId = () => crypto.randomUUID()

export function ListPage({ api, session, list, onClose }: Props) {
  const [membersKey, setMembersKey] = useState(0)
  const membersChanged = useCallback(() => setMembersKey((k) => k + 1), [])
  const { state, people, link, pending, mutate, update } = useListSync(api, session.accessToken, session.email, list.id, membersChanged)
  const online = useOnline()
  const [text, setText] = useState('')
  const items = visibleItems(state)
  const status = !online ? 'offline' : link

  const add = () => {
    const value = text.trim()
    if (!value) return
    setText('')
    void mutate({ mutationId: newId(), type: 'add', listId: list.id, itemId: newId(), text: value })
  }

  const mergeDuplicates = async () => {
    const merged = await api.mergeDuplicates(list.id)
    update((s) => applyServerItems(s, merged.items))
  }

  return (
    <section className="stack">
      <div className="row spread">
        <h2>{list.name}</h2>
        <span className={`pill ${status}`} role="status">
          {status === 'live' ? 'Live' : status === 'connecting' ? 'Connecting…' : 'Offline'}
          {pending > 0 && ` · ${pending} to sync`}
        </span>
      </div>
      <p className="muted">
        Invite code <code>{list.inviteCode}</code>
        {status === 'live' && people.length > 0 && <> · here now: {people.join(', ')}</>}
      </p>
      <form
        className="row"
        onSubmit={(e) => {
          e.preventDefault()
          add()
        }}
      >
        <input placeholder="Add an item, e.g. 2x milk" value={text} onChange={(e) => setText(e.target.value)} maxLength={200} />
        <button type="submit">Add</button>
      </form>
      <ul className="items">
        {items.map((item) => (
          <li key={item.id} className={item.checked ? 'done' : undefined}>
            <label>
              <input
                type="checkbox"
                checked={item.checked}
                onChange={(e) =>
                  void mutate({
                    mutationId: newId(),
                    type: 'check',
                    listId: list.id,
                    itemId: item.id,
                    baseVersion: item.version || undefined,
                    checked: e.target.checked,
                  })
                }
              />
              <span>{formatItem(item)}</span>
              {item.version === 0 && <span className="muted small">not synced</span>}
            </label>
            <button
              className="link"
              aria-label={`Delete ${item.name}`}
              onClick={() =>
                void mutate({ mutationId: newId(), type: 'delete', listId: list.id, itemId: item.id, baseVersion: item.version || undefined })
              }
            >
              ✕
            </button>
          </li>
        ))}
      </ul>
      {items.length === 0 && <p className="muted">Nothing here yet.</p>}
      <button className="secondary" disabled={status === 'offline'} onClick={() => void mergeDuplicates().catch(() => undefined)}>
        Merge duplicates
      </button>
      <Members api={api} listId={list.id} online={online} refreshKey={membersKey} onGone={onClose} />
    </section>
  )
}
