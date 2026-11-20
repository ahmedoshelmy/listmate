export interface Item {
  id: string
  listId: string
  name: string
  quantity: number
  checked: boolean
  deleted: boolean
  /** Server list version at this item's last change; 0 while it exists only on this device. */
  version: number
  changedBy: string
}

export interface ListSummary {
  id: string
  name: string
  inviteCode: string
  version: number
  memberCount: number
}

export interface Member {
  userId: string
  /** Email of the member. */
  name: string
  role: 'Owner' | 'Editor'
  isYou: boolean
}

export interface ListSnapshot {
  list: ListSummary
  version: number
  items: Item[]
}

export interface ListChanges {
  listId: string
  since: number
  version: number
  items: Item[]
}

export type Mutation =
  | { mutationId: string; type: 'add'; listId: string; itemId: string; text: string }
  | { mutationId: string; type: 'edit'; listId: string; itemId: string; baseVersion?: number; name?: string; quantity?: number }
  | { mutationId: string; type: 'check'; listId: string; itemId: string; baseVersion?: number; checked: boolean }
  | { mutationId: string; type: 'delete'; listId: string; itemId: string; baseVersion?: number }

export interface MutationResult {
  mutationId: string
  status: 'applied' | 'merged' | 'ignored' | 'rejected' | 'duplicate'
  reason: string | null
  item: Item | null
}

export interface SyncResponse {
  results: MutationResult[]
  versions: Record<string, number>
}
