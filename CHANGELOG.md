# Changelog

## [0.2.0](https://github.com/cliff-gor/FinCore/compare/platform-v0.1.0...platform-v0.2.0) (2026-10-14)


### Features

* add API host, service defaults and migrator ([f9bcc9c](https://github.com/cliff-gor/FinCore/commit/f9bcc9c236d563a9d5cdb54dc828525918e1b9cc))
* add audit log, BFF session and key storage tables ([a6ae895](https://github.com/cliff-gor/FinCore/commit/a6ae8955f882146bbee81f1d9204a42b8f7b56d4))
* add building blocks for dispatch, outbox, inbox and idempotency ([670b209](https://github.com/cliff-gor/FinCore/commit/670b209ee1b155963a0cf520fb1692bf64d16508))
* add shared kernel for money, results, ids and business date ([50fd5f4](https://github.com/cliff-gor/FinCore/commit/50fd5f4b3c733707c3c0640ae7732df3e52e374a))
* add two-pool sign-in and the shared security model ([c7485ab](https://github.com/cliff-gor/FinCore/commit/c7485abc467932cf6c60ca8e4de7489fefeb60d2))
* **api-client:** add the HTTP client and generated Organization client ([d4ae19b](https://github.com/cliff-gor/FinCore/commit/d4ae19b2884fe22855052b6f5218c5750108eeed))
* audit every successful command ([7ced759](https://github.com/cliff-gor/FinCore/commit/7ced7598fc1f3dad4718e03f7df48ab104e5c858))
* **backoffice:** add Back Office and Customer Portal shells ([f47dd9f](https://github.com/cliff-gor/FinCore/commit/f47dd9f371cdee13f4e31c1755999ac0a3a44b66))
* **bff:** add gateway in BackOffice, Portal and Mobile modes ([2b6d128](https://github.com/cliff-gor/FinCore/commit/2b6d1289633ae0ed155ca80e111538ab8b8ce7bd))
* **bff:** server-side sign-in sessions for the Back Office and Portal ([1374bb2](https://github.com/cliff-gor/FinCore/commit/1374bb2ec010e0485397c0339124b71f6aafb033))
* **identity:** add staff users, roles and the approval state machine ([08c348e](https://github.com/cliff-gor/FinCore/commit/08c348e719af31df6591d9c2c5d5a2abdb5c1236))
* **identity:** permissions on every request and Identity endpoints ([0172ee7](https://github.com/cliff-gor/FinCore/commit/0172ee73ada74a7bc3fa40f8c4f1f7a86ac33ec3))
* **identity:** staff management, approvals inbox and /me ([3857fa5](https://github.com/cliff-gor/FinCore/commit/3857fa5ffe93083ea266b5b0472db6da5d6b1d74))
* **mobile:** PKCE sign-in with secure token storage ([44b1e97](https://github.com/cliff-gor/FinCore/commit/44b1e97199785d00d43b42393ca66603f58e4e8f))
* **org:** add branches, holiday calendars and institution lifecycle ([1d1973c](https://github.com/cliff-gor/FinCore/commit/1d1973c30307c38f5e57e057a1b14c4a6b6c04e1))
* **org:** add institutions with currencies and business date ([3b3139b](https://github.com/cliff-gor/FinCore/commit/3b3139bb298da753e43a8843da32b2f3a19261db))
* **org:** branch, holiday and lifecycle endpoints and persistence ([197ea0e](https://github.com/cliff-gor/FinCore/commit/197ea0e518f5b12d14279e250077081dc05a601a))
* **org:** branch, holiday and status commands with institution isolation ([433939d](https://github.com/cliff-gor/FinCore/commit/433939d6a22f91bfaf2a461acbbf6ef9e63bc5aa))
* publish Organization and Identity contracts ([55459f4](https://github.com/cliff-gor/FinCore/commit/55459f4880b8c5822663a54efcc738e3d91ea527))
* register the Identity module in the API and migrator ([1f972c9](https://github.com/cliff-gor/FinCore/commit/1f972c96ed097b6e8641e6a9161dbdb73452e557))
* **tokens:** add design tokens, money, permissions and i18n packages ([d256190](https://github.com/cliff-gor/FinCore/commit/d25619091fe0695756e0bd31711325a39a99b1d8))
* **ui:** add session hooks and the first web UI components ([06be82e](https://github.com/cliff-gor/FinCore/commit/06be82e8242d8c355a363f1b8b55ff95e2876f4a))
* **web:** sign-in and sign-out in the Back Office and Portal ([4f5d2d2](https://github.com/cliff-gor/FinCore/commit/4f5d2d2f41c4c5c4da55ad1dc46a4fffad150e80))


### Bug Fixes

* **identity:** close two races in first sign-in and the institution status copy ([5952ec3](https://github.com/cliff-gor/FinCore/commit/5952ec3e39b7278d9dd90ecfbd447afa1d6f91c6))


### Infrastructure

* add Terraform skeleton for bootstrap, staging and production ([75321e7](https://github.com/cliff-gor/FinCore/commit/75321e7bcb3202ad17b8d22ea655d09ec3fc1616))

## Changelog

All notable changes to the FinCore platform are recorded here by release-please from Conventional Commits.
