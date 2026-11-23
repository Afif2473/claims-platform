# Task 09 — Kafka Claim Events & Claimant Notifications

## 1. REST vs Kafka: What Uses Which

| Interaction | Transport | Why |
| :--- | :--- | :--- |
| Submit claim, workflow actions | **REST** | The user waits for the result: validation errors, the new claim number, a 409 when someone else got there first. These must be synchronous. |
| Claim lists, detail, dashboard, notifications | **REST** | Reads of current state. |
| "Something happened to a claim" (submitted, assigned, info requested, decided, settled) | **Kafka**, topic `claim-events` | Side effects such as notifying the claimant shouldn't slow down or fail the officer's request, and they can retry on their own. New consumers (email/SMS, a reporting read model, fraud checks) can subscribe without changing the claims code. |

The frontend never talks to Kafka. It reads notifications over REST.

```
 Officer ──REST──▶ ClaimWorkflowService ──SaveChanges (claim + ClaimActivity, 1 txn)──▶ SQLite
                          │ after commit
                          ▼
                  KafkaClaimEventPublisher ──▶ [claim-events, key = claimId, 3 partitions]
                                                          │
                                   ClaimEventConsumer (group claims-notifications)
                                                          │
                                  ClaimNotificationHandler ──▶ Notifications table
 Claimant ──REST──▶ GET /api/notifications ◀──────────────────────────┘
```

## 2. Event Contract

One event per `ClaimActivity` (audit entry). `eventId` = the activity id.

```json
{
  "eventId": "…", "eventType": "DecisionRecorded",
  "claimId": "…", "claimNumber": "CLM-SG-2026-A457DB46", "claimantId": "…",
  "status": "Approved", "currencyCode": "SGD", "approvedAmount": 4200.00,
  "actorUserId": "…", "occurredAt": "2026-11-23T07:10:00+00:00"
}
```

- **Key = claimId.** All events for one claim go to the same partition, so they are consumed in order
  (checked: offsets 0–5 on one partition, in lifecycle order).
- The event carries everything a consumer needs, so consumers don't call back into the API.

## 3. Claimant Notifications

| Event | Message |
| :--- | :--- |
| ClaimSubmitted | We have received your claim … |
| ClaimAssigned | Your claim … is now being reviewed by a claims officer. |
| InformationRequested | Action needed: we need more information … |
| DecisionRecorded (Approved) | Good news: claim … has been approved for SGD 4,200.00. |
| DecisionRecorded (Rejected) | Claim … has been rejected. Open the claim to see the reason. |
| StatusChanged (Settled) | Payment of SGD 4,200.00 for claim … has been released. |
| ReserveSet, InformationProvided | none (internal, or the claimant's own action) |

`GET /api/notifications` returns the current user's notifications, newest first. They're written
asynchronously, so one may appear a moment after the action that caused it (eventual consistency).

## 4. Reliability

| Concern | Handling |
| :--- | :--- |
| Event for a change that was rolled back | Events are published **only after** `SaveChanges` succeeds. A 409 publishes nothing (checked). |
| Kafka slow or down | The producer is non-blocking (`Produce` + delivery callback). The request still returns 201 quickly (0.02s with the broker down). Failures are logged after `MessageTimeoutMs` = 10s. |
| Producer retries creating duplicates | `EnableIdempotence = true`, `Acks = All`. |
| Consumer crashes mid-message | Auto-commit is off. The offset is committed **after** the notification is saved, giving at-least-once delivery. |
| Redelivered message | `Notification.EventId` has a unique index and the handler skips known events. Checked: a new consumer group replayed all 6 events and the count stayed at 5. |
| Poison (malformed) message | Logged and skipped so it can't block the partition. Production: dead-letter topic. |
| Transient processing error | Not committed. The consumer seeks back and retries after 5s. |
| Topic missing | Created on startup with 3 partitions, if absent. |
| Log noise while the broker is down | Transient reconnect errors go to Debug, so only "all brokers down" and fatal errors are visible (from 174 lines to 3 in 13s). |

**Known gap: publish after commit is not atomic.** If the process dies, or the broker stays down beyond
the 10s timeout, after the database commit, that event is lost. The claim itself is correct and the
claimant still sees the decision on the claim page, but the notification is missing. The production fix is
a **transactional outbox**: write the event to an `Outbox` table in the same transaction, and have a relay
publish it. That's left out to stay within the timebox.

## 5. Running

```bash
docker compose up -d                                       # apache/kafka:3.9.0, KRaft, localhost:9092
dotnet run --project src/Claims.Api --launch-profile http
```

Without Docker: `Kafka__Enabled=false dotnet run --project src/Claims.Api --launch-profile http`.
The API works fully except that notifications are not created.

Inspect the topic:
```bash
docker exec claims-kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 \
  --topic claim-events --from-beginning --property print.key=true
```

## 6. Design Decisions

- **Consumer runs in the API process.** It's one deployable for local running. It only depends on Kafka
  and the database, so it can move into a separate worker service unchanged.
- **The notification logic (`ClaimNotificationHandler`) is separate from the Kafka loop (`ClaimEventConsumer`)**,
  so which events notify, and with what message, can be tested without Kafka.
- **Seeded demo claims don't produce events**, because they're inserted directly. Only real actions do.

## 7. Verification

| Check | Result |
| :--- | :--- |
| Full lifecycle against the real broker | 6 events in order on one partition, 5 notifications, consumer lag 0 |
| Stale-version 409 | 409, no event published |
| Replay with a new consumer group | Notification count unchanged (idempotent) |
| Clean broker | Topic created, `PartitionCount: 3` |
| Broker down | 201 in 0.02s, event failure logged after timeout |
| `Kafka__Enabled=false` | 201, no Kafka activity |
| `has-pending-model-changes` after migration | No changes |
| Unhandled server errors | 0 |
