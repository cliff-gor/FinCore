# Skills per track (spec §35.2.4)

Discovery run for M0 on 2026-10-13 (read-only; nothing new installed).

| Track | Skill | Publisher | Status | Notes |
|---|---|---|---|---|
| Backend | `dotnet-claude-kit` plugin skills (spec, plan, ef-core, minimal-api, testing, …) | owner-installed | Installed | Owner's choice per §35.2.3 |
| Web | `frontend-design` | Anthropic | Installed | Apply within Soft Ledger tokens (§32) |
| Web | `react-best-practices`, `composition-patterns`, `web-design-guidelines` | Vercel | Not installed | Recommended by §35.2.1 — owner to approve install |
| Web | `webapp-testing` | Anthropic | Not installed | Recommended for Playwright work — owner to approve |
| Mobile | `expo-overview`, `expo-project-structure`, `expo-router`, `expo-design-system`, `expo-native-ui`, `expo-animation`, `expo-data-fetching`, `expo-dev-client`, `expo-upgrade`, `eas-*` | Expo | Installed | Load `expo-overview` first. The Expo MCP server needs authorization before use. |
| Mobile | `react-native-skills` | Vercel | Not installed | Recommended by §35.2.2 — owner to approve |
| Infra | `terraform-style-guide` | HashiCorp | Not installed | Optional (§35.2.3) |

**Spec precedence reminders:** keep the custom `FloatingTabBar` and NativeWind even if Expo skills suggest native
tabs or `@expo/ui`; keep Vite + AWS rather than Next.js/Vercel-specific guidance.

## M1 discovery (2026-10-14)
No change to the installed set. Used: `dotnet-claude-kit:spec` (spec 002), Expo skills for the mobile sign-in work
(`expo-overview`, `expo-data-fetching`). Vercel web skills and `react-native-skills` remain recommended but not
installed; spec 003 (design system and UI shells) is the point to decide on them.
