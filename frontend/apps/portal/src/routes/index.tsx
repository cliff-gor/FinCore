import { RequireAuth, useSession } from '@fincore/auth';
import { createFileRoute } from '@tanstack/react-router';
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
        <p className="text-body text-text-muted">{t('home.portalSubtitle')}</p>
      </div>
      <RequireAuth>
        <SignedInSummary />
      </RequireAuth>
    </div>
  );
}

function SignedInSummary() {
  const { t } = useTranslation();
  const session = useSession();
  if (session.status !== 'signed-in') return null;
  return <p className="text-body">{t('session.signedInAs', { name: session.user.name })}</p>;
}
