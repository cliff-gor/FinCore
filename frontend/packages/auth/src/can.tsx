import { hasPermission, type PermissionGrant } from '@fincore/permissions';
import type { ReactNode } from 'react';
import { useSession } from './use-session.ts';

/** Props for `Can`; `fallback` renders when the permission is missing (default: nothing). */
export interface CanProps {
  permission: PermissionGrant;
  children: ReactNode;
  fallback?: ReactNode;
}

/** `<Can permission="loans.approve">` — UI convenience only; the API always enforces authorization. */
export function Can({ permission, children, fallback = null }: CanProps) {
  const session = useSession();
  // Hidden while the session loads or errors, so a privileged action never flashes into view. Customers carry no
  // permissions in M1 (spec 002), so only staff sessions can pass.
  const allowed =
    session.status === 'signed-in' &&
    session.user.kind === 'staff' &&
    hasPermission(session.user.permissions, permission);
  return <>{allowed ? children : fallback}</>;
}
