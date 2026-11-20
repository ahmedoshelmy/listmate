# 2. One service, no message broker

Date: 2026-11-20 · Status: accepted

## Context

Listmate has one bounded context: lists, their members and their items. Every write touches one list, reads are by
list, and real-time fan-out goes to the people who have that list open. Sibling projects use RabbitMQ, an outbox
and several services because they have several contexts that must evolve and fail independently.

## Decision

Build one ASP.NET Core service with Clean Architecture layers (Domain, Application, Infrastructure, Api) and no
broker. Changes are fanned out in-process after the transaction commits: `SyncService` collects changed items and
calls `IListNotifier`, implemented with `IHubContext` sending to the SignalR group `list:{id}`.

## Consequences

- One deployable, one database, one transaction per sync batch: exactly-once is a primary key, not a saga.
- A notification can be lost if the process dies between commit and send. That is acceptable because notifications
  are hints: every client resyncs with `changes?since=N` on (re)connect, and the database is the source of truth.
- Scale-out works without a broker: several API replicas share Postgres, and the SignalR Redis backplane carries
  group messages between replicas. Presence would then need a shared store (it is in memory today).
- If another context appears (for example price lookups or AI aisle sorting), it gets its own service and talks
  to this one through events, not by sharing tables.
