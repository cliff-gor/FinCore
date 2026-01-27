import { RequireAuth, signOut, useSession } from '@fincore/auth';
import { Button } from '@fincore/ui';
import { createFileRoute } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

export const Route = createFileRoute('/')({
  component: HomePage,
});

function HomePage() {
  const { t } = useTranslation();
  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="font-display text-h1">{t('home.title')}</h1>
        <p className="text-body text-text-muted">{t('home.backofficeSubtitle')}</p>
      </div>
      <RequireAuth>
        <SignedInSummary />
      </RequireAuth>
    </div>
  );
}

// Minimal sign-in plumbing (spec 002); the real shell and its user menu are spec 003.
function SignedInSummary() {
  const { t } = useTranslation();
  const session = useSession();
  const [signOutFailed, setSignOutFailed] = useState(false);
  if (session.status !== 'signed-in') return null;

  const kind = t(session.user.kind === 'staff' ? 'session.kindStaff' : 'session.kindCustomer');
  return (
    <div className="flex flex-col items-start gap-4" data-testid="signed-in">
      <p className="text-body">{t('session.signedInAsKind', { name: session.user.name, kind })}</p>
      <Button
        variant="secondary"
        onClick={() => {
          setSignOutFailed(false);
          signOut().catch(() => setSignOutFailed(true));
        }}
      >
        {t('session.signOut')}
      </Button>
      {signOutFailed && (
        <p role="alert" className="text-body text-text-muted">
          {t('session.signOutFailed')}
        </p>
      )}
    </div>
  );
}
