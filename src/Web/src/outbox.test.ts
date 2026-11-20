import { describe, expect, it, vi } from 'vitest'
import { Outbox, PermanentSyncError, memoryStorage } from './outbox'
import type { Mutation, SyncResponse } from './types'

const add = (n: number): Mutation => ({ mutationId: `m${n}`, type: 'add', listId: 'list', itemId: `i${n}`, text: `item ${n}` })
const ok = (batch: Mutation[]): SyncResponse => ({
  results: batch.map((m) => ({ mutationId: m.mutationId, status: 'applied', reason: null, item: null })),
  versions: { list: batch.length },
})

describe('Outbox', () => {
  it('sends queued mutations in order and empties the queue once acknowledged', async () => {
    const outbox = new Outbox(memoryStorage())
    await outbox.enqueue(add(1))
    await outbox.enqueue(add(2))
    const send = vi.fn(async (batch: Mutation[]) => ok(batch))

    await outbox.flush(send)

    expect(send).toHaveBeenCalledWith([add(1), add(2)])
    expect(await outbox.pending()).toEqual([])
  })

  it('keeps everything when the network fails, and resends the same ids later', async () => {
    const outbox = new Outbox(memoryStorage([add(1)]))

    await expect(outbox.flush(() => Promise.reject(new TypeError('Failed to fetch')))).rejects.toThrow()
    expect(await outbox.pending()).toEqual([add(1)])

    const send = vi.fn(async (batch: Mutation[]) => ok(batch))
    await outbox.flush(send)
    expect(send.mock.calls[0]?.[0][0]?.mutationId).toBe('m1')
  })

  it('keeps changes made while a flush was in flight', async () => {
    const outbox = new Outbox(memoryStorage([add(1)]))
    const send = async (batch: Mutation[]) => {
      await outbox.enqueue(add(2))
      return ok(batch)
    }

    await outbox.flush(send)

    expect(await outbox.pending()).toEqual([add(2)])
  })

  it('shares one in-flight flush between concurrent callers', async () => {
    const outbox = new Outbox(memoryStorage([add(1)]))
    const send = vi.fn(async (batch: Mutation[]) => ok(batch))

    await Promise.all([outbox.flush(send), outbox.flush(send)])

    expect(send).toHaveBeenCalledTimes(1)
  })

  it('drops a batch the server refuses as invalid instead of retrying it forever', async () => {
    const outbox = new Outbox(memoryStorage([add(1)]))

    await expect(outbox.flush(() => Promise.reject(new PermanentSyncError('bad')))).rejects.toThrow(PermanentSyncError)
    expect(await outbox.pending()).toEqual([])
  })

  it('sends large queues in bounded batches', async () => {
    const outbox = new Outbox(memoryStorage([add(1), add(2), add(3)]), 2)
    const send = vi.fn(async (batch: Mutation[]) => ok(batch))

    await outbox.flush(send)

    expect(send.mock.calls[0]?.[0]).toHaveLength(2)
    expect(await outbox.pending()).toEqual([add(3)])
  })
})
