# Claims Platform — Angular frontend

One Angular 21 app for both claimants and claims staff. The navigation and actions adapt to the role of
the signed-in user.

## Run

The API must be running on `http://localhost:5129` (see the root README).

```bash
npm install
npm start            # ng serve with proxy.conf.json → http://localhost:4200
```

`proxy.conf.json` forwards `/api` to the API, so the browser sees one origin and the backend needs no CORS setup.

## Structure

```
src/app/
  core/                      shared, no UI state
    models.ts                TypeScript mirror of the API DTOs (backend is the source of truth: /openapi/v1.json)
    api.service.ts           typed HTTP client; every state change sends the claim version
    session.service.ts       "signed-in" user (signal, persisted in localStorage)
    user-id.interceptor.ts   adds X-User-Id to every request
    auth.guard.ts            sends visitors to /login until a user is chosen
    problem.ts               ProblemDetails → readable message
    status-badge.ts          status pill
  pages/
    login.ts                 "sign in as" a seeded user (demo stand-in for real auth)
    claim-list.ts            claimant: my claims · staff: queue with Unassigned / Mine / All + status filter
    new-claim.ts             report an incident
    claim-detail.ts          details, decision, info requests, timeline, role-based actions
    dashboard.ts             workload, performance, liability exposure (staff)
    notifications.ts         claimant updates from Kafka, polled every 5s
```

Standalone components, signals for state, and zoneless change detection (the Angular 21 default).
There is no UI library, only a small stylesheet in `src/styles.css`.
