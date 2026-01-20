import { Permissions, ReadAllWildcard, type PermissionGrant } from './permissions.ts';

/** Standard staff roles and their typical permissions (master spec §13). */
export const Roles = {
  SystemAdmin: 'SystemAdmin',
  BranchOfficer: 'BranchOfficer',
  LoanOfficer: 'LoanOfficer',
  FieldOfficer: 'FieldOfficer',
  CreditApprover: 'CreditApprover',
  ComplianceOfficer: 'ComplianceOfficer',
  Accountant: 'Accountant',
  Auditor: 'Auditor',
} as const;

export type Role = (typeof Roles)[keyof typeof Roles];

export const rolePermissions: Readonly<Record<Role, readonly PermissionGrant[]>> = {
  SystemAdmin: [Permissions.Org.Manage, Permissions.Identity.Manage, Permissions.Products.Manage],
  BranchOfficer: [
    Permissions.Customers.Create,
    Permissions.Kyc.Capture,
    Permissions.Accounts.Open,
    Permissions.Accounts.Transact,
  ],
  LoanOfficer: [Permissions.Loans.Apply, Permissions.Loans.Assess, Permissions.Loans.Repay],
  FieldOfficer: [Permissions.Mfi.GroupsManage, Permissions.Mfi.CollectionsRecord],
  CreditApprover: [Permissions.Loans.Approve, Permissions.Cards.LimitApprove],
  ComplianceOfficer: [Permissions.Kyc.Approve, Permissions.Aml.Review],
  Accountant: [Permissions.Ledger.JournalCreate, Permissions.Ledger.Read, Permissions.Eod.Run],
  Auditor: [ReadAllWildcard],
};
