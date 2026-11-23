# Task 10 — Automated Tests

```bash
dotnet test ClaimsManagement.slnx
# Passed!  - Failed: 0, Passed: 57, Skipped: 0, Total: 57, Duration: ~2 s
```

No Docker or Kafka is needed. Tests use an in-memory SQLite database and a recording event publisher.

---

## 1. What Is Tested and Why

Tests target the rules that would cost money or trust if they broke: the lifecycle, concurrency,
liability numbers, access rules, and duplicate notifications.

| File | Layer | Covers |
| :--- | :--- | :--- |
| `Domain/ClaimWorkflowTests.cs` | Pure | Every legal transition, key illegal ones (approve before review, reverse a decision, leave a terminal state), the definition of "open" |
| `Domain/LiabilityExposureTests.cs` | Pure | Approved → reserve → claimed precedence; closed claims = 0 |
| `Domain/DashboardBuildTests.cs` | Pure | Status counts, queue age, officer workload and average days, per-market exposure, empty data |
| `Domain/InformationRequestTests.cs` | Pure | Answer once only, no blank answers |
| `Domain/NotificationMessageTests.cs` | Pure | Which events notify the claimant and what the message says |
| `Services/ClaimWorkflowServiceTests.cs` | Service + SQLite | Full lifecycle (status, version, 7 audit entries, 7 events with matching ids); stale version gives 409 with nothing saved or published; concurrent write caught by the token; assigned officer only; role checks; illegal transition; approve ≤ claimed; another claimant gets 404; late answer after rejection |
| `Services/ClaimNotificationHandlerTests.cs` | Handler + SQLite | Redelivered event creates one notification; it goes to the claimant; internal events are ignored |

## 2. Design Decisions

1. **Real SQLite in memory, not EF's InMemory provider.** InMemory enforces neither concurrency tokens
   nor foreign keys, so the concurrency test would pass even with the token removed. The schema is
   created by **running the real migrations**, so the migrations are exercised too.
2. **Service tests use a fresh `DbContext` per call**, like separate HTTP requests. A shared context
   would serve tracked entities from memory and hide bugs.
3. **Fakes instead of a mocking library.** `FixedCurrentUser` and `RecordingEventPublisher` are about
   5 lines each, so there's no extra dependency.
4. **No controller or HTTP tests.** Controllers only forward to services. The HTTP status mapping was
   checked by the scripted end-to-end runs in Tasks 06–09. A `WebApplicationFactory` suite would be the next addition.

## 3. Proving the Tests Can Fail (mutation check)

Every test passed on the first run, so I deliberately broke the code to confirm the tests actually
detect bugs. Each break was then reverted.

| Deliberate bug | Caught by |
| :--- | :--- |
| Remove `.IsConcurrencyToken()` | `Concurrent_write_between_read_and_save_is_caught_by_the_version_token` |
| Allow Approved → Rejected | `Rejects_illegal_transitions` |
| Exposure always uses the claimed amount | `Reserve_overrides…`, `Approved_amount_overrides…`, `Exposure_is_grouped…` |
| Skip the client version check | `Stale_version_is_rejected_and_nothing_is_saved_or_published` |
| Drop the "already handled" check in the notification handler | `Redelivered_event_creates_only_one_notification` |

The last one also confirmed the **second line of defence**: with the check removed, the unique index on
`Notification.EventId` still rejected the duplicate insert. In the real consumer, that error leads to a
retry, which then sees the existing notification and skips it.

## 4. Not Covered (deliberate)

HTTP-level tests (status codes, validation attributes, ProblemDetails shape), Kafka integration tests
(verified manually against a real broker in Task 09), and frontend tests.
