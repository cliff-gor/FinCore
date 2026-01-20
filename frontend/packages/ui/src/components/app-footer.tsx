import { cn } from '../cn.ts';

/** Build metadata shown by `AppFooter`, normally from the Vite-injected build info. */
export interface AppFooterProps {
  version: string;
  sha: string;
  env: string;
  className?: string;
}

/** Version footer: `v<version> · <sha> · <env>` (master spec §33.7.3). */
export function AppFooter({ version, sha, env, className }: AppFooterProps) {
  return (
    <footer
      className={cn('border-t border-border px-6 py-4 text-body-sm text-text-muted', className)}
      data-testid="app-footer"
    >
      v{version} · {sha} · {env}
    </footer>
  );
}
