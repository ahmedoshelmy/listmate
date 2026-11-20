import { describe, expect, it } from 'vitest'
import { applyLocal, applyServerItems, emptyState, visibleItems } from './listState'
import type { Item } from './types'

const item = (patch: Partial<Item>): Item => ({
  id: 'milk', listId: 'list', name: 'milk', quantity: 1, checked: false, deleted: false, version: 1, changedBy: 'u1', ...patch,
})

describe('list state', () => {
  it('never lets an older server copy replace a newer one', () => {
    const state = applyServerItems(emptyState, [item({ version: 5, checked: true })], 5)
    const next = applyServerItems(state, [item({ version: 3, checked: false })])

    expect(next.items.milk?.checked).toBe(true)
    expect(next.version).toBe(5)
  })

  it('only moves the resync bookmark forward', () => {
    const state = applyServerItems({ ...emptyState, version: 9 }, [], 4)
    expect(state.version).toBe(9)
  })

  it('shows an offline add immediately with its parsed quantity', () => {
    const state = applyLocal(emptyState, { mutationId: 'm1', type: 'add', listId: 'list', itemId: 'x', text: '2x milk' }, 'me')

    expect(state.items.x).toMatchObject({ name: 'milk', quantity: 2, version: 0 })
  })

  it('replaces the optimistic copy when the server confirms it', () => {
    const local = applyLocal(emptyState, { mutationId: 'm1', type: 'add', listId: 'list', itemId: 'milk', text: 'milk' }, 'me')
    const confirmed = applyServerItems(local, [item({ version: 7 })])

    expect(confirmed.items.milk?.version).toBe(7)
  })

  it('hides tombstones and sorts ticked items last', () => {
    const state = applyServerItems(emptyState, [
      item({ id: 'a', name: 'bread', version: 1, checked: true }),
      item({ id: 'b', name: 'eggs', version: 2 }),
      item({ id: 'c', name: 'tea', version: 3, deleted: true }),
    ])

    expect(visibleItems(state).map((i) => i.name)).toEqual(['eggs', 'bread'])
  })

  it('ignores edits to items it does not know', () => {
    const state = applyLocal(emptyState, { mutationId: 'm', type: 'check', listId: 'list', itemId: 'ghost', checked: true }, 'me')
    expect(state).toBe(emptyState)
  })
})
