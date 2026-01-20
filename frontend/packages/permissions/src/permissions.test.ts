import { describe, expect, it } from 'vitest';
import { allPermissions, hasPermission, Permissions, rolePermissions } from './index.ts';

describe('permissions', () => {
  it('exposes the M0 organization permissions', () => {
    expect(Permissions.Org.Manage).toBe('org.manage');
    expect(Permissions.Org.Read).toBe('org.read');
  });

  it('has unique permission strings', () => {
    expect(new Set(allPermissions).size).toBe(allPermissions.length);
  });

  it('grants explicit permissions and the auditor read wildcard', () => {
    expect(hasPermission(rolePermissions.SystemAdmin, 'org.manage')).toBe(true);
    expect(hasPermission(rolePermissions.Auditor, 'org.read')).toBe(true);
    expect(hasPermission(rolePermissions.Auditor, 'org.manage')).toBe(false);
    expect(hasPermission([], 'org.read')).toBe(false);
  });
});
