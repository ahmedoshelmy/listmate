import type { Mutation, SyncResponse } from './types'

/** Where queued mutations live; IndexedDB in the app, a Map in tests. */
export interface OutboxStorage {
  load(): Promise<Mutation[]>
  save(mutations: Mutation[]): Promise<void>
}

export type Send = (mutations: Mutation[]) => Promise<SyncResponse>

/** The server refused the batch itself (HTTP 400): sending it again can never succeed, so it is dropped. */
export class PermanentSyncError extends Error {}

/**
 * Client half of the sync protocol: every change is queued here first (with a fresh mutationId), shown optimistically,
 * and sent in order when the device is online. A batch leaves the queue only after the server answered, so a crash or a
 * dropped connection mid-request just means the same ids are sent again, and the server de-duplicates them.
 */
export class Outbox {
  private flushing: Promise<SyncResponse | null> | null = null

  constructor(private readonly storage: OutboxStorage, private readonly maxBatch = 100) {}

  async enqueue(mutation: Mutation): Promise<void> {
    const pending = await this.storage.load()
    await this.storage.save([...pending, mutation])
  }

  pending(): Promise<Mutation[]> {
    return this.storage.load()
  }

  /** Sends what is queued; concurrent calls share one in-flight flush. Returns null when nothing was sent. */
  flush(send: Send): Promise<SyncResponse | null> {
    this.flushing ??= this.flushOnce(send).finally(() => {
      this.flushing = null
    })
    return this.flushing
  }

  private async flushOnce(send: Send): Promise<SyncResponse | null> {
    const pending = await this.storage.load()
    if (pending.length === 0) return null
    const batch = pending.slice(0, this.maxBatch)
    const sent = new Set(batch.map((m) => m.mutationId))
    const drop = async () => this.storage.save((await this.storage.load()).filter((m) => !sent.has(m.mutationId)))
    try {
      const response = await send(batch)
      await drop()
      return response
    } catch (error) {
      if (error instanceof PermanentSyncError) await drop()
      throw error
    }
  }
}

export function memoryStorage(initial: Mutation[] = []): OutboxStorage {
  let items = [...initial]
  return {
    load: async () => [...items],
    save: async (next) => {
      items = [...next]
    },
  }
}
