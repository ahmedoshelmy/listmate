# 1. Record architecture decisions

Date: 2026-11-20 · Status: accepted

## Context

Listmate is small, but the sync design makes choices (idempotency, conflict rules, a single service) that a reader
of the code will want explained, and commit messages are not where people look.

## Decision

Keep lightweight Architecture Decision Records in `docs/adr`, one file per decision, numbered, with context,
decision and consequences. Superseded records stay, marked as such.

## Consequences

New structural choices come with an ADR in the same change.
