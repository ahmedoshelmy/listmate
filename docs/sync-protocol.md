# Sync protocol

How a Listmate device keeps a list in step with the server and with other devices, online or not.

## Moving parts

| Piece | Where | Job |
|---|---|---|
| **Outbox** | IndexedDB on the device (`src/Web/src/outbox.ts`) | Every change is queued here first, with a fresh `mutationId`. |
| **List cache** | IndexedDB (`list:{id}`) | Last known items + the version the device has caught up to. |
| **`POST /api/sync`** | `SyncService` | Applies a batch of mutations exactly once, in order. |
| **`processed_mutations`** | Postgres | Idempotency log: every applied `mutationId`. |
| **List version** | `lists.Version` | A per-list counter; every item change takes the next value. |
| **`GET /api/lists/{id}/changes?since=N`** | `ListService` | Every item changed after version N, deletes included (tombstones). |
| **SignalR `/hubs/lists`** | `ListsHub` | Pushes `ItemChanged` to group `list:{id}` after each commit; presence. |

## Mutations

```json
{ "mutationId": "uuid", "type": "add",    "listId": "uuid", "itemId": "uuid", "text": "2x milk" }
{ "mutationId": "uuid", "type": "edit",   "listId": "uuid", "itemId": "uuid", "baseVersion": 7, "name": "oat milk", "quantity": 2 }
{ "mutationId": "uuid", "type": "check",  "listId": "uuid", "itemId": "uuid", "baseVersion": 7, "checked": true }
{ "mutationId": "uuid", "type": "delete", "listId": "uuid", "itemId": "uuid", "baseVersion": 7 }
```

- **Item ids are chosen by the client**, so an item added offline already has its final id; later edits in the same
  outbox can refer to it.
- **Mutations are intents, not toggles.** `check` carries the value (`checked: true`), so applying it twice is harmless.
- `baseVersion` is the item version the device last saw. It does not block the write; it only tells the server (and
  the client, through the result) that the change was made on stale data.

`POST /api/sync` takes `{ "mutations": [...] }` (1–500, in the order the person made them) and answers with one result
per mutation plus the current version of each list touched:

```json
{ "results": [{ "mutationId": "…", "status": "applied", "reason": null, "item": { "id": "…", "version": 12, … } }],
  "versions": { "<listId>": 12 } }
```

| Status | Meaning |
|---|---|
| `applied` | Written on top of the version the device saw. |
| `merged` | Written, but someone changed the item in between; fields this mutation did not touch keep their newer values. |
| `ignored` | Nothing to do: same value, the item id already exists, or the item was deleted (delete wins). |
| `rejected` | Unknown item, or a list the caller is not a member of. |
| `duplicate` | This `mutationId` was applied before; nothing changed. `item` shows the current state. |

## Exactly once

The whole batch runs in **one database transaction**: the items, the bumped list versions and one
`processed_mutations` row per mutation commit together. So:

- A batch that committed but whose response was lost (tunnel, train, lift) is re-sent with the same ids, and every
  mutation comes back `duplicate`.
- A batch that did not commit is re-sent and applied normally.
- Two copies of a batch racing (reconnect + manual retry) both start; the first commit wins. The second fails on the
  list's row version (Postgres `xmin`) or on the `processed_mutations` primary key, re-reads, and finds every id
  already processed. Retries are bounded (4 attempts), then the API answers 409 and the device tries later.

The device removes a batch from its outbox only after the server answered. A 400 (a malformed batch) is dropped, since
sending it again can never succeed; any other failure leaves it queued.

## Conflict rules

Applied in **server arrival order**, per list (the row version serialises writers):

1. **Per-field last writer wins.** Each mutation sets only its own fields. Alex renames "milk" to "oat milk" while
   Sam, offline, ticks it: both survive, because they touched different fields. If both set the same field, the one
   that reaches the server last wins.
2. **Delete wins over edit.** Once an item is deleted, later edits and ticks are `ignored`, whatever their
   `baseVersion`. Undo is a new `add`.
3. **Adds are idempotent by item id.** A second `add` with an existing id is `ignored`.
4. **No-ops do not create versions.** Setting a field to its current value is `ignored` and broadcasts nothing.

Why not a CRDT: see [ADR 0003](adr/0003-sync-protocol-lww-not-crdt.md).

## Versions and resync

Every item change takes `list.Version + 1` and stamps it on the item. Because versions are per list and gap-free,
"what changed since N" is one indexed query (`list_id = @id AND version > @n`, tombstones included).

The device keeps one bookmark per list: the version it has **fully caught up to**. Only snapshot and changes
responses move it. Live `ItemChanged` pushes and sync results update items, never the bookmark, because a push can
be missed (the socket was down for a second) and a sync result does not include other people's changes in between.
An older copy of an item never replaces a newer one (compare `version`).

```mermaid
sequenceDiagram
  participant P as Phone (offline)
  participant API
  participant DB as Postgres
  participant L as Laptop
  P->>P: tick "milk" → outbox [m1], show it ticked
  Note over P: back online
  P->>API: POST /api/sync [m1]
  API->>DB: BEGIN; check m1 not processed; item v=13; list v=13; insert m1; COMMIT
  API-->>P: m1 applied, item v13
  API-->>L: ItemChanged (group list:{id})
  Note over L: socket drops, reconnects
  L->>API: JoinList(id), then GET /changes?since=12
  API-->>L: [milk v13] — nothing missed
```

After any SignalR reconnect the client calls `JoinList` again (group membership is per connection), flushes its
outbox, then fetches `changes?since=bookmark`.

## Merge duplicates

`POST /api/lists/{id}/merge-duplicates` folds open (unticked) items with the same name, ignoring case and spacing:
`2x milk` + `milk` → `3x milk`. The oldest item survives so its id stays stable everywhere; the others become
tombstones. It is an ordinary change: new versions, broadcast, visible in `changes`.

## Membership changes and retention

- `POST /api/lists/{id}/leave` and the owner's `DELETE /api/lists/{id}/members/{userId}` take effect at once: the
  next request from that person's devices gets 404 for the list, and queued mutations for it come back `rejected`.
  The server sends `MemberLeft` to the list's group first (so the person's open screens close the list), then removes
  their connections on that replica from the group. An owner who leaves hands the list to the longest-standing member;
  the last one out deletes the list and its items.
- `processed_mutations` is pruned by a background sweep (every `Retention:SweepEveryHours`, default 6) of rows older
  than `Retention:ProcessedMutationDays` (default 30). This bounds the table, at a price: a device that stays offline
  longer than the window and then replays could apply a mutation twice. Adds are still safe (the client-chosen item
  id already exists); ticks and edits are last-writer-wins anyway. Pick a window longer than any realistic offline spell.
