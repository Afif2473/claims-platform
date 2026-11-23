# Task 11 — Angular Frontend

One Angular app serves both user types, as the brief requires. The scope follows the priority agreed
for Step 11: a user picker, then the claimant journey, then the officer journey, then the dashboard.
All four were built.

## 1. What a User Can Do

| Claimant | Claims officer | Manager |
| :--- | :--- | :--- |
| Report an incident (form) | See the queue: Unassigned / Assigned to me / All, with a status filter | Same queue (read-only) |
| See my claims and their status | Open a claim: details, timeline, info requests | Team dashboard |
| Open a claim: decision and reason, timeline | Pick up, set reserve, ask the claimant, approve, reject, settle | |
| Answer an information request | Team dashboard | |
| See updates (notifications from Kafka) | | |

## 2. Key Decisions

| Decision | Why |
| :--- | :--- |
| **Angular 21.2 (CLI 21.2.29, framework 21.2.26), exact versions** | Current supported line. Every version is at least 2 weeks old (22.x and 21.2.30 were released days ago). Pinned like the backend packages. |
| **Standalone components, signals, zoneless** | Angular 21 defaults. State is small and per-page, so there's no store library. |
| **Dev-server proxy (`/api` → 5129)** | Same origin in the browser, so no CORS code in the API. In production a reverse proxy or gateway does the same job. |
| **Contract: backend DTOs → `models.ts`** | The backend defines the contract (OpenAPI at `/openapi/v1.json`). The TypeScript types are a hand-written mirror. Generating them from OpenAPI is the next step if the API grows. |
| **Version sent on every action** | `ApiService` sends `claim.version` with each command. That's the frontend half of optimistic concurrency. |
| **409 handling** | The latest claim is reloaded, the officer's typed input is **kept**, and a plain message is shown ("Someone else updated this claim…"). |
| **Async updates by polling** | Notifications come from the Kafka consumer, so they arrive slightly after the action. The Updates page polls every 5s. Server push (SSE/WebSocket) is the scale-up option. |
| **Buttons reflect role and status, but the API decides** | Hiding buttons is a convenience. Every rule is still enforced and reported by the API (403/409 shown inline). |
| **No UI library** | Fewer dependencies. One small stylesheet with design tokens. |

## 3. Bugs Caught During This Step

| Bug | How it was found | Fix |
| :--- | :--- | :--- |
| **Incident date "in the future" for today.** Sending noon UTC for today's date is still in the future for a morning submission in UTC+8 (SG/MY/HK), so the API would reject it. `toISOString()` also gave the UTC date, which could block picking today. | Code review while writing the form, confirmed with Node in the WSL timezone | Send the start of the chosen day in local time, and compute "today" from the local date |
| **Notification polling stops after one failed request.** An error reaching the outer `timer` stream ends it for good. | Code review | `catchError` inside `switchMap`, per request |
| **409 message contradicted itself** ("Reload it… has been refreshed…") | Headless browser screenshot | The UI writes its own message for concurrency conflicts |
| **409 wiped what the officer had typed** | Headless browser screenshot | Reload the claim without resetting the forms |
| **Missing space** ("asked(23 Nov") | Screenshot | `&nbsp;` (Angular strips whitespace between elements) |

## 4. Verification

- `ng build`: strict template type-checking passes, ~90 kB initial transfer.
- **Headless Chrome walkthrough** through the real UI (driven via the DevTools protocol, not committed):
  sign in by clicking → submit a claim through the form → officer queue → pick up → ask a question →
  claimant answers → a version change behind the officer's back causes a 409, then auto-refresh, then a
  successful approve → claimant Updates shows 4 Kafka notifications → manager dashboard → phone-width (390px) layout.
  **0 JavaScript errors.**
- `npm audit`: 16 moderate findings, all from `glob`/`minimatch` inside the **Angular CLI** (dev tooling).
  `npm audit --omit=dev` reports **0** for the code shipped to the browser. `npm audit fix --force`
  was not run because it forces a breaking CLI change.

## 5. Not Built (deliberate)

Frontend unit tests (the backend tests cover the rules), a notification badge in the nav, paging,
document/photo upload, real login, and generated API types.
