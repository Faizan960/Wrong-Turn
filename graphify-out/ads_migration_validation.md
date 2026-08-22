# Architecture Validation — AdMob → Unity Ads migration

Rebuilt from `Assets/Scripts` (directed, AST). Compared against the pre-migration baseline.

## Graph size
| Metric | Baseline (class-level dep graph) | After (module ref graph) |
|---|---|---|
| Project types | 105 | 105 (unchanged set; `AdMobProvider` removed, `UnityAdsProvider` added → net 0) |
| Class-level ref edges | 380 | 380 ± provider swap (AdsManager loses `AdMobProvider`, gains `UnityAdsProvider`) |
| AST nodes / edges | — | 1359 / 2388 |

## Cycles
- **Module-level cycles: 1 → `Managers → UI`.**
- Sole bridging edge: `UIManager → UIScreen` (pre-existing UI state routing). **Not introduced by the ad migration** and not a class-level cycle (`UIScreen` does not point back into `Managers`).
- **New cycles introduced by ads migration: 0.**

## SDK isolation (the key invariant)
Source-level scan for `UnityEngine.Advertisements` / `Advertisement.` / `IUnityAds*`:
- **Only match: `Assets/Scripts/Analytics/UnityAdsProvider.cs`.**
- `GameManager`, `RuleEngine`, `DifficultyManager`, `ChaosManager`, `GameplayHUD`, `GameOverScreen` — **zero** SDK references.
- `GameOverScreen → AdsManager` remains the only gameplay↔ads edge (the sanctioned boundary).

## Resulting layering (unchanged, verified)
```
GameOverScreen ──▶ AdsManager ──▶ IAdProvider ──▶ UnityAdsProvider ──▶ UnityEngine.Advertisements
                       │                     └────▶ MockAdProvider (editor/QA)
                       └── owns ALL policy (rewarded on tap, interstitial cadence, first-session guard)
```

## Presentation → Manager violations
- New violations from ads migration: **0** (`GameOverScreen`'s dependency on the generic `AdsManager` is the same sanctioned edge that existed under AdMob).
