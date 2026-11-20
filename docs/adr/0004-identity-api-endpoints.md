# 4. ASP.NET Core Identity API endpoints for auth

Date: 2026-11-20 · Status: accepted

## Context

Listmate needs accounts so lists can be shared, and the PWA and the SignalR hub need a token. Options: an external
identity provider (Entra ID, Auth0, Keycloak), hand-written JWT issuing, or ASP.NET Core Identity's built-in API
endpoints (`MapIdentityApi`, .NET 8+).

## Decision

Use `AddIdentityApiEndpoints<IdentityUser>()` with the EF Core stores in the same Postgres database, mapped under
`/api/auth` (`register`, `login`, `refresh`, …). Clients send the opaque bearer token in the `Authorization` header;
the SignalR hub also accepts it as `?access_token=` because browsers cannot set headers on a WebSocket.

## Consequences

- No extra container or third-party account for a self-hosted demo; password hashing, lockout and refresh tokens
  come from the framework.
- Tokens are opaque Data Protection payloads, not JWTs: only this API can read them. With several replicas the
  Data Protection key ring must be shared (Redis or blob storage) — part of the scale-out profile.
- Swapping to an external OIDC provider later changes only the authentication setup in `Program.cs`; the
  endpoints read the user id from the `NameIdentifier` claim either way.
