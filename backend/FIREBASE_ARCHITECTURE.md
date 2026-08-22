# WRONG TURN — FIREBASE LEADERBOARD ARCHITECTURE DECISION

Backend migrated from Supabase (blocked by free-project quota) to Firebase.
Audit, anti-cheat spec, ranking requirements, UI plan and the `ILeaderboardProvider`
boundary are unchanged. This document is the design to approve **before** any
implementation.

**Verdict up front:** Firestore is *efficient* for Top-N / country / city /
around-me (all indexed queries). Its one weak spot — there is no native `RANK()`
— is solved acceptably for launch scale with `count()` aggregation + caching, and
has a documented bucket-counter path for 100k+ MAU. **Not fundamentally
inefficient → proceed.**

---

## 1. Firestore data model

Three collections, each with a distinct trust level:

### `players/{uid}` — private profile (own-read only)
```
displayName      string
countryCode      string?   'IN'
countryDisplay   string?   'India'
cityId           string?   'mumbai_in'
cityDisplay      string?   'Mumbai'
createdAt        timestamp
regionChangedAt  timestamp?   // 30-day cooldown anchor
```

### `leaderboard/{uid}` — PUBLIC ranking doc (public read, **Cloud-Function-only write**)
```
displayName      string    // denormalized for query-free rendering
score            number    // BEST high_score, NORMAL only
achievedAt       timestamp // tie-break: earlier wins
comboScore       number    // BEST longest_combo (secondary board)
countryCode      string?   // denormalized from profile
countryDisplay   string?
cityId           string?
cityDisplay      string?
rulesetVersion   number    // seasons/rebalance-safe (§22)
updatedAt        timestamp
```
Only safe fields live here — no email, IP, device id, or raw run data. **The doc
is created only when a player submits their first valid score.** A player with no
score simply isn't in `leaderboard`, so empty/small boards honestly reflect real
data (§27) with zero fake rows.

### `private_runs/{uid}` — anti-cheat / validation data (**no client access at all**)
```
lastRun { score, maxCombo, correct, wrong, duration, appVersion, nonce, submittedAt }
flags   [ ... ]   // plausibility flags for later moderation
```
Sensitive run internals are never publicly readable (§ security rules).

**One doc per player** serves global/country/city via `where` filters — no
per-region duplication. Region/name changes update `players` **and** the
denormalized `leaderboard` fields inside the same Cloud Function.

---

## 2–5. Ranking queries (indexed, no full scans)

| View | Query | Composite index |
|---|---|---|
| **Global Top-N** | `leaderboard.orderBy(score desc, achievedAt asc).limit(N)` | `(score desc, achievedAt asc)` |
| **Country Top-N** | `+ where(countryCode == 'IN')` | `(countryCode asc, score desc, achievedAt asc)` |
| **City Top-N** | `+ where(cityId == 'mumbai_in')` | `(cityId asc, score desc, achievedAt asc)` |
| **Around-me (above)** | `where(score > myScore).orderBy(score asc, achievedAt desc).limit(K)` | reuses indexes |
| **Around-me (below)** | `where(score < myScore).orderBy(score desc, achievedAt asc).limit(K)` | reuses indexes |

Top-N = N reads (10). Around-me = ~2K reads (~10). No client ever downloads the
whole collection. Same tie semantics (`score desc, achievedAt asc`) on every
scope (§ ties).

## 6. Exact rank strategy (the Firestore-specific bit)

No native rank → **`count()` aggregation** (returns a number, does **not** download docs):

```
rank = 1
     + count(score > myScore)                              // strictly better
     + count(score == myScore AND achievedAt < myAchievedAt) // tie-break, earlier first
```
per scope, with the scope's `where` filter added. Billing: 1 read per 1000 index
entries matched (a count over 90k better docs ≈ 90 reads, not 90k). Run **only**
on rank-card open and after a confirmed PB, then cached. This is cheap at 1k–10k
MAU; see §11 for the 100k-MAU flag and the bucket-counter escalation path.

Around-me neighbors (§ above/below queries) + this rank number render the
`#1,482 YOU` card with correct neighbors.

## 7. Cloud Functions (callable v2, TypeScript, `firebase-admin`)

Authoritative writes only — UID comes from the verified Firebase Auth context,
never the client body.

- **`submitScore`** — validates run → plausibility gate → **transactional**
  best-only update of `leaderboard/{uid}` (a lower score is a no-op) → writes
  `private_runs/{uid}` → returns refreshed rank card. Client calls this **only
  when the run beats the local best** (§32 cost control).
- **`ensureProfile`** — first launch: creates `players/{uid}` with a generated
  alias `TURNER####`; returns it. (No `leaderboard` doc until a score exists.)
- **`updateDisplayName`** — 3–16 chars, trims, strips `<>`/control chars,
  profanity gate; updates `players` + denormalized `leaderboard.displayName`.
- **`updateRegion`** — 30-day cooldown via `regionChangedAt`; updates `players` +
  denormalized `leaderboard` region fields.

**Rank/Top-N/around-me READS are done client-side via Firestore REST** against the
public `leaderboard` docs — no function invocation cost per rankings open. Only
the four *mutations* are functions.

## 8. Authentication

Firebase **Anonymous Auth** → stable UID per install. No device id / ad id /
hardware id / email as identity. Future Google Play Games / Google linking stays
possible (anonymous accounts are upgradeable) — not implemented this phase.

## 9. Security rules (deny-by-default)

```
match /leaderboard/{uid} {
  allow read: if true;            // public safe fields only
  allow write: if false;          // ONLY Cloud Functions (admin) write
}
match /players/{uid} {
  allow read: if request.auth != null && request.auth.uid == uid;  // own only
  allow write: if false;          // name/region via validated functions only
}
match /private_runs/{uid} {
  allow read, write: if false;    // never client-accessible
}
```
No `allow write: if request.auth != null` anywhere near scores — a modified APK
still cannot write an authoritative score. Rules tested with the emulator
(§ QA). `apply_best_score`-style bypass is structurally impossible: the client
has no write path and functions verify UID + plausibility.

## 10. Offline / cache

Local JSON cache (top-N, around-me, rank card, `lastUpdated`). On open: show cache
instantly, then fetch. New PB while offline → store the **single highest** pending
run; submit on reconnect; backend stays authoritative. Show `OFFLINE` /
`LAST UPDATED …`. Gameplay never blocks on the network. No Firebase SDK
persistence needed — we own the cache.

## 11. Cost / read pattern (Firestore Spark free: 50k reads, 20k writes /day)

Assumptions: player opens Rankings ~2×/day; visits ~2 tabs; ~1 new PB/week.
Per rankings session ≈ Top-10 ×2 tabs (20) + around-me (10) + rank-card counts
(~50 avg) ≈ **~80 reads**. Writes ≈ new PBs only.

| MAU | Reads/day | Writes/day | Est. cost |
|---|---|---|---|
| 1,000 | ~160k | ~150 | ~$0 (a few ¢ over free reads) |
| 10,000 | ~1.6M | ~1.5k | ~$5–8/mo |
| 100,000 | ~16M + growing count() | ~15k | **$50–150/mo — flag** |

**Cost flags & mitigations:**
- **Cloud Functions require the Blaze (pay-as-you-go) plan** — billing/card must
  be enabled even though Blaze's free allotment (2M invocations/mo) covers us.
  This is the one unavoidable account requirement.
- Because we only `submitScore` on a **local-best beat**, writes/function-calls
  stay tiny.
- At ~50k+ MAU, replace per-open `count()` rank with a **sharded score-bucket
  counter** (Cloud Function increments range buckets on each best write; rank =
  sum of higher buckets → O(#buckets) reads regardless of MAU). The `leaderboard`
  schema already supports adding this layer with no migration.
- Cache TTLs + refresh-on-demand keep reads bounded; no listeners over large sets.

## 12. Firebase products & Unity packages

- **Backend:** Firebase Authentication (Anonymous), Cloud Firestore, Cloud
  Functions (Node 20).
- **Unity client:** **ZERO new Unity packages.** Pure REST via `UnityWebRequest`:
  - Auth REST (`identitytoolkit` `signUp` anonymous + `securetoken` refresh)
  - Firestore REST (`:runQuery`, `:runAggregationQuery`, documents GET)
  - Callable functions via HTTPS POST `{data:{…}}` + `Authorization: Bearer <idToken>`

### SDK vs REST — decision: **REST, no Firebase Unity SDK**
The Firebase Unity SDK pulls native Android AARs + EDM4U Gradle resolution, which
is exactly where it could collide with **LevelPlay 9.5.0** and inflate the APK.
REST via `UnityWebRequest` adds **no** Android dependencies, **no** Gradle
changes, **no** APK bloat, and can't destabilize ad init. We already own cache
and refresh (so we don't need SDK persistence/listeners). This directly satisfies
"install only what is required" and "DO NOT BREAK LEVELPLAY."

---

## What I need from you (I cannot provision Firebase from here)
Unlike Supabase, there are no Firebase MCP tools in this session, so **you** create
the project; I write every rule/function/index + a precise runbook and the full
Unity client.

1. Create a Firebase project (e.g. `wrong-turn-leaderboards`) at
   console.firebase.google.com.
2. **Enable Blaze plan** (required for Cloud Functions; near-$0 at our scale).
3. Enable **Anonymous** sign-in (Auth → Sign-in method).
4. Give me the client-safe config: **Project ID**, **Web API key**, and the
   Firestore REST host. (Never the service account / admin key — that stays in
   Functions only.)
5. I hand you `firebase deploy` commands for rules, indexes, and functions.

Unity only ever receives the **Web API key + project id** (client-safe). The
privileged admin credential lives exclusively inside Cloud Functions.
