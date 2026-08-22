# Wrong Turn — Firebase Leaderboard Backend

Server-authoritative global / country / city / around-me leaderboards.
Full design: `../FIREBASE_ARCHITECTURE.md`.

## Layout
```
firebase.json              project config (functions + firestore)
firestore.rules            deny-by-default; no client score writes
firestore.indexes.json     composite indexes for Top-N / around-me
functions/src/index.ts     Cloud Functions (the ONLY score writer)
```

## Cloud Functions (callable v2)
- `ensureProfile({defaultName?})` → profile (+ generated alias TURNER####)
- `updateDisplayName({displayName})` → sanitized name
- `updateRegion({countryCode,countryDisplay,cityId,cityDisplay})` → 30-day cooldown
- `submitScore({finalScore,maxCombo,correctAnswers,wrongAnswers,runDuration,appVersion,rulesetVersion,nonce,easyMode,daily})`
  → plausibility gate + transactional best-only update + refreshed rank card
- `getRankCard({board?,rulesetVersion?})` → world/country/city rank on demand

Rank/Top-N/around-me READS are done client-side over the public `leaderboard`
collection (no function cost). Only mutations are functions.

## ONE-TIME SETUP (must be done by a human — no Firebase MCP in this session)
1. Create a Firebase project at https://console.firebase.google.com
   (suggested id: `wrong-turn-leaderboards`).
2. **Upgrade to the Blaze plan** (required for Cloud Functions; ~$0 at our scale,
   2M free invocations/mo).
3. **Authentication → Sign-in method → Anonymous → Enable.**
4. **Firestore Database → Create database** (production mode, region e.g.
   `asia-south1` / Mumbai).
5. Install tooling locally: `npm i -g firebase-tools`, then `firebase login`.
6. From this `firebase/` folder:
   ```
   firebase use --add            # select the project, alias "default"
   cd functions && npm install && npm run build && cd ..
   firebase deploy --only firestore:rules,firestore:indexes,functions
   ```
7. Give Claude the **client-safe** values to paste into Unity:
   - Project ID
   - Web API key  (Project settings → General → Web API Key)
   - Functions region (default `us-central1`)
   NEVER share the service-account JSON / admin key — that lives only in Functions.

## Notes
- If a query reports a missing composite index at runtime, Firebase logs a
  one-click console URL to create it; add it and re-`deploy --only
  firestore:indexes`. The provided set covers Top-N + around-me for all scopes.
- Emulator for local QA: `firebase emulators:start --only functions,firestore,auth`.
