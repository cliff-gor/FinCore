/**
 * Permission constants mirrored from the backend `Permissions` class (master spec §13, spec 001).
 * UI checks are convenience only — the API always enforces authorization.
 */
export const Permissions = {
  Org: {
    Manage: 'org.manage',
    Read: 'org.read',
  },
  Identity: {
    Manage: 'identity.manage',
  },
  Products: {
    Manage: 'products.manage',
  },
  Customers: {
    Create: 'customers.create',
    Read: 'customers.read',
    Update: 'customers.update',
  },
  Kyc: {
    Capture: 'kyc.capture',
    Approve: 'kyc.approve',
  },
  Accounts: {
    Open: 'accounts.open',
    Transact: 'accounts.transact',
  },
  Loans: {
    Apply: 'loans.apply',
    Assess: 'loans.assess',
    Repay: 'loans.repay',
    Approve: 'loans.approve',
  },
  Mfi: {
    GroupsManage: 'mfi.groups.manage',
    CollectionsRecord: 'mfi.collections.record',
  },
  Cards: {
    LimitApprove: 'cards.limit.approve',
  },
  Aml: {
    Review: 'aml.review',
  },
  Ledger: {
    JournalCreate: 'ledger.journal.create',
    Read: 'ledger.read',
  },
  Eod: {
    Run: 'eod.run',
  },
} as const;

type Leaves<T> = T extends string ? T : { [K in keyof T]: Leaves<T[K]> }[keyof T];

export type Permission = Leaves<typeof Permissions>;

/** Wildcard grant used by the Auditor role: every `*.read` permission. */
export const ReadAllWildcard = '*.read';

export type PermissionGrant = Permission | typeof ReadAllWildcard;

export const allPermissions: readonly Permission[] = Object.values(Permissions).flatMap(
  (group) => Object.values(group) as Permission[],
);
