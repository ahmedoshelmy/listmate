import { useMemo, useState } from 'react'
import { createApi, loadSession, saveSession, signIn, type Session } from './api'
import { ListPage } from './ListPage'
import { ListsPage } from './ListsPage'
import type { ListSummary } from './types'

export function App() {
  const [session, setSession] = useState<Session | null>(loadSession)
  const [open, setOpen] = useState<ListSummary | null>(null)
  const api = useMemo(() => (session ? createApi(session.accessToken) : null), [session])

  if (!session || !api) {
    return (
      <SignIn
        onSignedIn={(s) => {
          saveSession(s)
          setSession(s)
        }}
      />
    )
  }

  const signOut = () => {
    saveSession(null)
    setSession(null)
    setOpen(null)
  }

  return (
    <main className="app">
      <header className="bar">
        {open ? (
          <button className="link" onClick={() => setOpen(null)}>
            ← Lists
          </button>
        ) : (
          <h1>Listmate</h1>
        )}
        <span className="muted">{session.email}</span>
        <button className="link" onClick={signOut}>
          Sign out
        </button>
      </header>
      {open ? (
        <ListPage api={api} session={session} list={open} />
      ) : (
        <ListsPage api={api} onOpen={setOpen} onUnauthorized={signOut} />
      )}
    </main>
  )
}

function SignIn({ onSignedIn }: { onSignedIn: (s: Session) => void }) {
  const [email, setEmail] = useState('alex@example.com')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (register: boolean) => {
    setBusy(true)
    setError(null)
    try {
      onSignedIn(await signIn(email.trim(), password, register))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not sign in.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="app narrow">
      <h1>Listmate</h1>
      <p className="muted">Shared lists that keep working offline.</p>
      <form
        className="stack"
        onSubmit={(e) => {
          e.preventDefault()
          void submit(false)
        }}
      >
        <label>
          Email
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoComplete="username" />
        </label>
        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={8}
            autoComplete="current-password"
          />
        </label>
        {error && <p className="error">{error}</p>}
        <div className="row">
          <button type="submit" disabled={busy}>
            Sign in
          </button>
          <button type="button" className="secondary" disabled={busy} onClick={() => void submit(true)}>
            Create account
          </button>
        </div>
      </form>
    </main>
  )
}
