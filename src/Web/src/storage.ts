import { get, set } from 'idb-keyval'
import type { OutboxStorage } from './outbox'
import type { ListState } from './listState'
import type { ListSummary, Mutation } from './types'

/** IndexedDB-backed outbox: survives reloads, closed tabs and a phone that kills the browser in the background. */
export const idbOutboxStorage: OutboxStorage = {
  load: async () => (await get<Mutation[]>('outbox')) ?? [],
  save: (mutations) => set('outbox', mutations),
}

/** Last known state of each list, so it opens instantly and works offline. */
export const listCache = {
  load: (listId: string) => get<ListState>(`list:${listId}`),
  save: (listId: string, state: ListState) => set(`list:${listId}`, state),
}

/** The lists screen, so the app opens offline too. */
export const listsCache = {
  load: () => get<ListSummary[]>('lists'),
  save: (lists: ListSummary[]) => set('lists', lists),
}
