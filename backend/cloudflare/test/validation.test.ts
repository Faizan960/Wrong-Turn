import { describe, it, expect } from "vitest";
import { sanitizeName, validateRun, ValidationError } from "../src/validation";
import { resolveRegion } from "../src/regions";

describe("sanitizeName", () => {
  it("accepts a valid name", () => expect(sanitizeName("Nova")).toBe("Nova"));
  it("trims + collapses whitespace", () => expect(sanitizeName("  a   b  ")).toBe("a b"));
  it("strips TMP markup vectors", () => expect(sanitizeName("<b>Ace</b>")).toBe("bAce/b"));
  it("strips control chars", () => expect(sanitizeName("Ace")).toBe("Ace"));
  it("rejects too short", () => expect(() => sanitizeName("ab")).toThrow(ValidationError));
  it("rejects too long", () => expect(() => sanitizeName("a".repeat(17))).toThrow(ValidationError));
  it("rejects profanity", () => expect(() => sanitizeName("shithead")).toThrow(ValidationError));
});

describe("validateRun", () => {
  const base = { finalScore: 1000, maxCombo: 20, correctAnswers: 40, wrongAnswers: 2, runDuration: 60, easyMode: false, daily: false };
  it("accepts a plausible NORMAL run", () => expect(() => validateRun({ ...base })).not.toThrow());
  it("rejects EASY", () => expect(() => validateRun({ ...base, easyMode: true })).toThrow("NON_COMPETITIVE_MODE"));
  it("rejects Daily", () => expect(() => validateRun({ ...base, daily: true })).toThrow("NON_COMPETITIVE_MODE"));
  it("rejects negative", () => expect(() => validateRun({ ...base, finalScore: -1 })).toThrow("NEGATIVE_OR_NAN"));
  it("rejects NaN", () => expect(() => validateRun({ ...base, finalScore: NaN })).toThrow("NEGATIVE_OR_NAN"));
  it("rejects absurd ceiling", () => expect(() => validateRun({ ...base, finalScore: 9_999_999, correctAnswers: 99999 })).toThrow("SCORE_CEILING"));
  it("rejects combo>correct", () => expect(() => validateRun({ ...base, maxCombo: 41 })).toThrow("COMBO_EXCEEDS_CORRECT"));
  it("rejects impossible answer rate", () => expect(() => validateRun({ ...base, correctAnswers: 2000, maxCombo: 1, runDuration: 10 })).toThrow("IMPOSSIBLE_ANSWER_RATE"));
  it("rejects score inconsistent with answers", () => expect(() => validateRun({ ...base, finalScore: 500000, correctAnswers: 40 })).toThrow("SCORE_INCONSISTENT_WITH_ANSWERS"));
});

describe("resolveRegion", () => {
  it("resolves country only", () => {
    const r = resolveRegion("IN", null);
    expect(r).toEqual({ countryCode: "IN", countryDisplay: "India", cityId: null, cityDisplay: null });
  });
  it("resolves country + city and derives display server-side", () => {
    const r = resolveRegion("in", "mumbai_in");
    expect(r).toEqual({ countryCode: "IN", countryDisplay: "India", cityId: "mumbai_in", cityDisplay: "Mumbai" });
  });
  it("rejects a fake country", () => expect(() => resolveRegion("ZZ", null)).toThrow("BAD_COUNTRY"));
  it("rejects a fake city", () => expect(() => resolveRegion("IN", "atlantis_in")).toThrow("BAD_CITY"));
  it("rejects city from the wrong country", () => expect(() => resolveRegion("US", "mumbai_in")).toThrow("BAD_CITY"));
});
