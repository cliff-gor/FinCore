import { ReadAllWildcard } from './permissions.ts';

/** True when `granted` contains `required`, honouring the `*.read` wildcard. */
export function hasPermission(granted: readonly string[], required: string): boolean {
  if (granted.includes(required)) return true;
  return required.endsWith('.read') && granted.includes(ReadAllWildcard);
}
