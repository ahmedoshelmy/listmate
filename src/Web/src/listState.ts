import { parseItemText } from './itemText'
import type { Item, Mutation } from './types'

/** What one device knows about one list: the last server version it has caught up to, and the items. */
export interface ListState {
  version: number
  items: Record<string, Item>
}

export const emptyState: ListState = { version: 0, items: {} }

/** Server items always win over what this device holds, but an older copy never replaces a newer one. */
export function applyServerItems(state: ListState, items: Item[], version?: number): ListState {
  const next = { ...state.items }
  for (const item of items) {
    const current = next[item.id]
    if (!current || current.version <= item.version) next[item.id] = item
  }
  return { version: Math.max(state.version, version ?? 0), items: next }
}

/** Shows a queued change immediately; the server's answer later replaces it via applyServerItems. */
export function applyLocal(state: ListState, m: Mutation, me: string): ListState {
  const current = state.items[m.itemId]
  let item: Item | undefined
  switch (m.type) {
    case 'add':
      if (current) return state
      item = { id: m.itemId, listId: m.listId, ...parseItemText(m.text), checked: false, deleted: false, version: 0, changedBy: me }
      break
    case 'edit':
      item = current && { ...current, name: m.name ?? current.name, quantity: m.quantity ?? current.quantity }
      break
    case 'check':
      item = current && { ...current, checked: m.checked }
      break
    case 'delete':
      item = current && { ...current, deleted: true }
      break
  }
  return item ? { ...state, items: { ...state.items, [item.id]: item } } : state
}

/** Open items first (in the order they were added), ticked ones at the bottom; tombstones hidden. */
export function visibleItems(state: ListState): Item[] {
  return Object.values(state.items)
    .filter((i) => !i.deleted)
    .sort((a, b) => Number(a.checked) - Number(b.checked) || order(a) - order(b))
}

const order = (i: Item) => (i.version === 0 ? Number.MAX_SAFE_INTEGER : i.version)
