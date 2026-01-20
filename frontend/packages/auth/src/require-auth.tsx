import { Button } from '@fincore/ui';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { signIn, useSession } from './use-session.ts';

export interface RequireAuthProps {
  children: ReactNode;
  /** Shown while the session is loading. */
  fallback?: ReactNode;
}

/** Renders children only for a signed-in user; otherwise a sign-in prompt (M0 placeholder, real OIDC in M1). */
export function RequireAuth({ children, fallback }: RequireAuthProps) {
  const { t } = useTranslation();
  const session = useSession();

  if (session.status === 'signed-in') return <>{children}</>;
  if (session.status === 'loading') {
    return fallback ?? <p className="text-body text-text-muted">{t('session.checking')}</p>;
  }

  return (
    <section
      aria-labelledby="signed-out-title"
      className="flex flex-col items-start gap-4 rounded-lg bg-surface p-6 shadow-e1"
      data-testid="signed-out"
    >
      <p id="signed-out-title" className="text-body text-text">
        {session.status === 'error' ? t('session.unavailable') : t('session.signedOut')}
      </p>
      <Button onClick={() => signIn()}>{t('session.signIn')}</Button>
    </section>
  );
}
