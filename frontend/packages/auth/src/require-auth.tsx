import { Button } from '@fincore/ui';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { signIn, signOut, useSession, type Session } from './use-session.ts';

/** Props for `RequireAuth`. */
export interface RequireAuthProps {
  children: ReactNode;
  /** Shown while the session is loading. */
  fallback?: ReactNode;
}

/**
 * Renders children only for a signed-in user. Otherwise it explains why not: signed out (sign-in button), refused by
 * FinCore although the identity provider accepted the user (not registered / inactive, with a sign-out button so
 * another account can be used), or the BFF is unreachable.
 */
export function RequireAuth({ children, fallback }: RequireAuthProps) {
  const { t } = useTranslation();
  const session = useSession();

  if (session.status === 'signed-in') return <>{children}</>;
  if (session.status === 'loading') {
    return fallback ?? <p className="text-body text-text-muted">{t('session.checking')}</p>;
  }

  const refused = session.status === 'not-registered' || session.status === 'inactive';
  return (
    <section
      aria-labelledby="signed-out-title"
      className="flex flex-col items-start gap-4 rounded-lg bg-surface p-6 shadow-e1"
      data-testid={refused ? 'access-refused' : 'signed-out'}
    >
      <p id="signed-out-title" className="text-body text-text">
        {t(messageKey(session))}
      </p>
      {refused ? (
        <Button variant="secondary" onClick={() => void signOut()}>
          {t('session.signOut')}
        </Button>
      ) : (
        <Button onClick={() => signIn()}>{t('session.signIn')}</Button>
      )}
    </section>
  );
}

function messageKey(session: Exclude<Session, { status: 'signed-in' | 'loading' }>): string {
  switch (session.status) {
    case 'not-registered':
      return session.code === 'Identity.EmailNotVerified'
        ? 'session.emailNotVerified'
        : 'session.notRegistered';
    case 'inactive':
      return 'session.inactive';
    case 'error':
      return 'session.unavailable';
    case 'signed-out':
      return 'session.signedOut';
  }
}
