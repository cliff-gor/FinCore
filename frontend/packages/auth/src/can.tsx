import { hasPermission, type PermissionGrant } from '@fincore/permissions';
import type { ReactNode } from 'react';
import { useSession } from './use-session.ts';

export interface CanProps {
  permission: PermissionGrant;
  children: ReactNode;
  fallback?: ReactNode;
}

/** `<Can permission="loans.approve">` — UI convenience only; the API always enforces authorization. */
export function Can({ permission, children, fallback = null }: CanProps) {
  const session = useSession();
  const allowed =
    session.status === 'signed-in' && hasPermission(session.user.permissions, permission);
  return <>{allowed ? children : fallback}</>;
}
