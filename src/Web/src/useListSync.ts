import { useCallback, useEffect, useRef, useState } from 'react'
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import type { Api } from './api'
import { applyLocal, applyServerItems, emptyState, type ListState } from './listState'
import { Outbox } from './outbox'
import { idbOutboxStorage, listCache } from './storage'
import type { Item, Mutation } from './types'

export const outbox = new Outbox(idbOutboxStorage)

export type Link = 'live' | 'connecting' | 'offline'

interface ItemChangedMessage {
  listId: string
  listVersion: number
  item: Item
}

/**
 * Keeps one list in sync: cached state first, then the server. Live pushes update items; the "caught up to version N"
 * bookmark only moves on snapshot/changes responses, so a missed push is always repaired by the next resync.
 */
export function useListSync(api: Api, token: string, me: string, listId: string) {
  const [state, setState] = useState<ListState>(emptyState)
  const [people, setPeople] = useState<string[]>([])
  const [link, setLink] = useState<Link>('connecting')
  const [pending, setPending] = useState(0)
  const stateRef = useRef(state)
  stateRef.current = state

  const update = useCallback(
    (change: (s: ListState) => ListState) =>
      setState((current) => {
        const next = change(current)
        void listCache.save(listId, next)
        return next
      }),
    [listId],
  )

  const countPending = useCallback(async () => setPending((await outbox.pending()).length), [])

  const resync = useCallback(async () => {
    const version = stateRef.current.version
    if (version === 0) {
      const snapshot = await api.snapshot(listId)
      update((s) => applyServerItems(s, snapshot.items, snapshot.version))
    } else {
      const changes = await api.changes(listId, version)
      update((s) => applyServerItems(s, changes.items, changes.version))
    }
  }, [api, listId, update])

  /** Sends the outbox, then pulls whatever else changed. Failures leave everything queued for the next try. */
  const syncNow = useCallback(async () => {
    try {
      // Bounded: a huge queue drains in batches without looping forever if something keeps re-queueing.
      for (let batch = 0; batch < 20; batch++) {
        const response = await outbox.flush(api.sync)
        if (!response) break
        const items = response.results.flatMap((r) => (r.item && r.item.listId === listId ? [r.item] : []))
        update((s) => applyServerItems(s, items))
      }
      await resync()
    } catch {
      // Offline or server unreachable: the outbox keeps the changes.
    } finally {
      await countPending()
    }
  }, [api, listId, resync, update, countPending])

  const mutate = useCallback(
    async (mutation: Mutation) => {
      update((s) => applyLocal(s, mutation, me))
      await outbox.enqueue(mutation)
      await countPending()
      if (navigator.onLine) await syncNow()
    },
    [me, update, syncNow, countPending],
  )

  useEffect(() => {
    let cancelled = false
    void listCache.load(listId).then((cached) => {
      if (cancelled) return
      // Re-apply queued local changes on top of the cache so they stay visible after a reload.
      void outbox.pending().then((queued) => {
        if (cancelled) return
        const base = cached ?? emptyState
        setState(queued.filter((m) => m.listId === listId).reduce((s, m) => applyLocal(s, m, me), base))
        stateRef.current = base
        void syncNow()
      })
    })
    return () => {
      cancelled = true
    }
  }, [listId, me, syncNow])

  useEffect(() => {
    const hub = new HubConnectionBuilder()
      .withUrl('/hubs/lists', { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    hub.on('ItemChanged', (m: ItemChangedMessage) => {
      if (m.listId === listId) update((s) => applyServerItems(s, [m.item]))
    })
    hub.on('PresenceChanged', (m: { listId: string; people: string[] }) => {
      if (m.listId === listId) setPeople(m.people)
    })
    const join = async () => {
      await hub.invoke('JoinList', listId)
      setLink('live')
      await syncNow()
    }
    hub.onreconnecting(() => setLink('connecting'))
    hub.onreconnected(() => void join().catch(() => setLink('offline')))
    hub.onclose(() => setLink('offline'))
    void hub.start().then(join).catch(() => setLink('offline'))

    const onOnline = () => {
      if (hub.state === HubConnectionState.Disconnected) void hub.start().then(join).catch(() => setLink('offline'))
      else void syncNow()
    }
    window.addEventListener('online', onOnline)
    return () => {
      window.removeEventListener('online', onOnline)
      void hub.stop()
    }
  }, [listId, token, update, syncNow])

  return { state, people, link, pending, mutate, syncNow, update }
}
