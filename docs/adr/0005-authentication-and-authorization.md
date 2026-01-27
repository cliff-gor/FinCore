# ADR-0005: Authentication and authorization (two identity pools, permissions from FinCore)

- **Status:** Accepted (spec 002)
- **Context:** Staff and customers sign in at different identity pools (two Keycloak realms locally, two Cognito user
  pools on AWS). Access must be revocable immediately, approvals need four-eyes rules, and browsers must never hold
  tokens.
- **Decision:**
  - **The identity provider proves who; FinCore decides what.** The API validates bearer tokens from both pools
    (one JWT scheme per pool, picked by the token's issuer). A claims transformation in the Identity module adds
    `fincore:*` claims and `permission` claims from FinCore's own database. Roles are fixed in M1 (master §13).
  - **Immediate revocation:** access profiles are cached per subject (HybridCache, 5 min) and invalidated explicitly
    on every access change and on institution status changes.
  - **First sign-in** links the IdP `sub` to a pre-registered staff user by *verified* email; afterwards `sub` is the key.
  - **Central guards:** every module endpoint requires authentication (no Development exception any more); a single
    endpoint filter blocks writes for staff of a Suspended institution; handlers enforce institution isolation and
    branch scope through `ICurrentUser`.
  - **Browsers:** a BFF keeps tokens server-side (`platform.bff_sessions`, Data Protection keys in Postgres); the
    cookie holds only a session id. **Mobile:** Authorization Code + PKCE, refresh token in SecureStore only.
  - **Maker-checker** is a generic engine in Identity; other modules use it through `ApprovalRequested` /
    `ApprovalRequestDecided` events. A PlatformAdmin's approval completes any request alone (owner decision), so
    large limits can be granted in a one-admin deployment.
- **Consequences:** Swapping Keycloak for Cognito is configuration only. Every request costs one cache lookup.
  Owned collections (roles, limits, decisions, holidays) use surrogate keys plus unique indexes, because natural
  keys make EF Core issue UPDATEs for newly added rows.
