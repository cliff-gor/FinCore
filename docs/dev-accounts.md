# Development accounts (spec 002, AC-22)

**Dev only.** These accounts exist in the local Keycloak realm exports (`deploy/keycloak/`) and are seeded into
FinCore's database by the Development seeder. Never reuse these passwords anywhere else.

All passwords: `FinCore-Dev-2026!`

## Realm `fincore-staff`
| Email | Institution | Home branch | Role(s) |
|---|---|---|---|
| `platform.admin@fincore.local` | — | — | PlatformAdmin |
| `admin1.fcb@fincore.local` | FCB | 001 | SystemAdmin |
| `admin2.fcb@fincore.local` | FCB | 001 | SystemAdmin |
| `branch.officer.fcb@fincore.local` | FCB | 002 | BranchOfficer |
| `loan.officer.fcb@fincore.local` | FCB | 002 | LoanOfficer |
| `field.officer.fcb@fincore.local` | FCB | 002 | FieldOfficer |
| `credit.approver.fcb@fincore.local` | FCB | 001 | CreditApprover |
| `compliance.fcb@fincore.local` | FCB | 001 | ComplianceOfficer |
| `accountant.fcb@fincore.local` | FCB | 001 | Accountant |
| `auditor.fcb@fincore.local` | FCB | 001 | Auditor |
| `admin1.fcmfi@fincore.local` | FCMFI | 001 | SystemAdmin |
| `admin2.fcmfi@fincore.local` | FCMFI | 001 | SystemAdmin |
| `branch.officer.fcmfi@fincore.local` | FCMFI | 002 | BranchOfficer |
| `loan.officer.fcmfi@fincore.local` | FCMFI | 002 | LoanOfficer |
| `field.officer.fcmfi@fincore.local` | FCMFI | 002 | FieldOfficer |
| `credit.approver.fcmfi@fincore.local` | FCMFI | 001 | CreditApprover |
| `compliance.fcmfi@fincore.local` | FCMFI | 001 | ComplianceOfficer |
| `accountant.fcmfi@fincore.local` | FCMFI | 001 | Accountant |
| `auditor.fcmfi@fincore.local` | FCMFI | 001 | Auditor |

Every staff email is marked verified in Keycloak, because FinCore links the IdP account to the pre-registered staff
user by verified email on first sign-in.

## Realm `fincore-customers`
| Email | Notes |
|---|---|
| `customer1@example.test` | identity only in M1 (customer records arrive in M2) |
| `customer2@example.test` | identity only in M1 |

## Branches seeded per institution
| Code | Name |
|---|---|
| 001 | Head Office |
| 002 | City Branch |
