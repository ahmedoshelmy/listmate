# 3. Mutation log with per-field last-writer-wins, not a CRDT

Date: 2026-11-20 · Status: accepted

## Context

Devices edit lists offline and sync later. Concurrent edits must merge predictably and nothing may be applied twice.
Options considered:

1. **Whole-row last-writer-wins with a version check (reject stale writes).** Simple, but an offline tick fails
   just because someone else renamed the item; the user has to redo it.
2. **A CRDT (for example Automerge or Yjs documents).** Merges anything, including concurrent text edits inside
   a name, but the server stores opaque documents, queries like "items changed since N" and membership checks get
   harder, and the payloads and libraries are heavy for a grocery list.
3. **Intent mutations with client idempotency keys, applied in server order, per-field last-writer-wins.**

## Decision

Option 3. Details in [sync-protocol.md](../sync-protocol.md): client-generated `mutationId`s logged in
`processed_mutations` in the same transaction as the change; a per-list version counter; per-field LWW in server
arrival order; delete wins over edit; adds idempotent by client-generated item id.

## Consequences

- The fields of a list item are tiny (name, quantity, ticked). Concurrent edits to the *same* field are rare and
  "last one wins" matches what people expect from a shared list.
- Two people editing the same name at once lose one edit. Acceptable here; a CRDT text type would be the upgrade.
- The server stays an ordinary relational model: the resync query, membership checks and reporting are plain SQL.
- `baseVersion` is advisory: stale writes are merged and reported as `merged`, never rejected, so offline work is
  never thrown away silently.
- The processed-mutation log grows forever; a retention job (keep 30 days, longer than any outbox lives) is the
  follow-up.
