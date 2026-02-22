# Tech Debt Cleanup Process

A structured, repeatable process for cleaning up technical debt at any stage of the development cycle. Run this after completing a major feature sprint, integration milestone, or before a release.

---

## Prerequisites

Before starting, ensure:
- All features in the current milestone are implemented and merged
- The solution builds with **zero errors**
- All existing tests **pass**
- You have a clear understanding of:
  - The **implementation plan** (what is done, what is planned)
  - Future tickets/features that may depend on current code

> **Critical Rule:** Never delete code that is scaffolding for a planned future feature. Review the implementation plan and requirements documents to identify what is "dead" vs. "reserved for future use."

---

## Phase 1: Repository & Plan Review

**Goal:** Build a mental map of the entire codebase before making changes.

1. Read all requirements and planning documents (implementation plan, requirements, plugin specs, test plans)
2. Identify which features are **completed**, which are **in-progress**, and which are **planned/future**
3. Create a list of code that *looks* unused but is reserved for future features — these are **protected** from removal
4. Note any architectural patterns, conventions, and naming standards used throughout the project

**Output:** A clear understanding of what can be safely modified without breaking current or future functionality.

---

## Phase 2: Dead Code Audit & Removal

**Goal:** Remove genuinely dead code to reduce maintenance burden and cognitive load.

### Audit Process

For each layer of the architecture (core interfaces → services → view models → views → plugin host → tests):

1. **Search for unused public/internal members** — methods, properties, fields, classes
2. **Verify zero callers** — use "Find All References" or grep for each candidate
3. **Cross-reference with future plan** — if the member is scaffolding for a planned ticket, mark it **protected** and skip
4. **Check test-only usage** — if a member exists only because tests reference it, the member and its tests are both candidates for removal

### What to Remove

| Category | Action |
|----------|--------|
| Methods with zero production callers and no future-ticket justification | Remove |
| Properties that are set but never read (write-only fields) | Remove |
| Fields/dictionaries that are populated but never queried | Remove |
| No-op methods that do nothing (empty bodies with no interface requirement) | Remove if not interface-mandated |
| Entire classes with no production usage | Remove (and their test files) |
| Constructor parameters that are accepted but never stored/used | Remove |
| `[NotifyPropertyChangedFor]` attributes referencing removed properties | Remove |
| Factory methods/constructors with parameters for removed properties | Simplify signature |

### What NOT to Remove

- Interface method implementations (even if the body is empty) — future plugins may use them
- Code explicitly tied to a planned future ticket (e.g., settings panel scaffolding)
- Event subscriptions/patterns that will be needed when a feature is wired in
- API surface that forms a logical pair (e.g., `Register`/`Unregister`) even if one side has no current callers

### After Removal

1. **Build** — fix any compilation errors in test files referencing removed code
2. **Run all tests** — remove or update tests that test removed functionality
3. **Verify the app launches and behaves identically** to before the cleanup

---

## Phase 3: Code Simplification

**Goal:** Reduce complexity, eliminate duplication, and improve readability without changing behavior.

### Patterns to Look For

| Pattern | Fix |
|---------|-----|
| **Duplicated try/catch blocks** with identical error handling | Extract a shared helper (e.g., `SafeWireContribution(pluginId, area, action)`) |
| **Repeated sort-after-add** (`list.Add(); list.Sort()`) | Replace with `InsertSorted()` using `BinarySearch` for O(log n) insertion |
| **Wrapper methods** that just delegate to another method with no added logic | Inline the call and remove the wrapper |
| **Duplicated shared logic** across similar methods | Extract a shared helper (e.g., `LoadMediaCoreAsync()` for common load steps) |
| **Unused method parameters** | Remove and update all call sites |
| **Constructor clash avoidance** (internal constructors that conflict with public ones) | Convert to a static factory method (e.g., `ForTesting()`) |
| **Computed properties on immutable objects** that allocate on every access | Precompute in constructor and store as a readonly field |

### Rules

- Each simplification must be **behavior-preserving** — no functional changes
- Prefer **small, incremental** edits over sweeping rewrites
- Build and run tests after each category of simplification
- When consolidating, ensure error messages remain specific and useful for debugging

---

## Phase 4: Performance Optimization

**Goal:** Identify and fix measurable performance issues in hot paths.

### Where to Look

1. **High-frequency event handlers** — position updates, frame callbacks, scroll events
2. **Per-frame allocations** — anything inside a decode loop or render callback
3. **Collection copies** — `.ToList()` on every query vs. cached snapshots
4. **Computed properties** accessed by virtualized UI controls (e.g., log entry formatters)
5. **String allocations** in hot paths — interpolation, `.ToString()`, `.Format()`

### Optimization Tiers

| Tier | Criteria | Action |
|------|----------|--------|
| **Safe (do now)** | Internal-only, no API changes, measurable impact | Implement |
| **Medium (evaluate)** | Small API surface change, benefits outweigh risks | Evaluate case-by-case |
| **Deferred (document)** | Requires contract changes (e.g., `ArrayPool` for frame buffers), planned perf ticket exists | Document for the performance pass ticket |

### Common Fixes

- **Precompute immutable strings** — if all inputs are set once (constructor), compute the result string once
- **Use `BinarySearch` + `Insert`** instead of `Add` + `Sort` for sorted collections
- **Pool large allocations** — `ArrayPool<byte>.Shared` for per-frame buffers (but requires consumer-side return)
- **Avoid repeated `Comparer<T>.Create()`** — cache the comparer if called frequently

### Rules

- Only optimize code that runs frequently (per-frame, per-event, per-scroll) — not startup-only code
- Measure or reason about impact before adding complexity
- Never sacrifice readability for micro-optimizations on cold paths
- Document deferred optimizations so they're picked up in the performance pass ticket

---

## Phase 5: Test Review & Cleanup

**Goal:** Ensure all tests are meaningful, correctly written, and aligned with current code.

### Tests to Remove

| Category | Example |
|----------|---------|
| **Smoke tests** | `Assert.True(true)` — provides zero value |
| **Tautological tests** | Testing that `enum.Value == 0` or that a property setter sets a property |
| **Tests of .NET runtime behavior** | Verifying that an auto-property stores and returns a value |
| **Tests for removed code** | Tests that reference deleted methods/properties |
| **Duplicate coverage** | Same behavior tested in two places (consolidate) |
| **Constant value tests** | Asserting that a string constant equals a specific string |

### Tests to Fix

| Issue | Fix |
|-------|-----|
| **Buggy assertions** | e.g., `mock.Property = value` (assignment) instead of `mock.Received().Property = value` (verification) |
| **Misleading names** | Rename to match what the test actually verifies |
| **Outdated assertions** | Remove assertions for properties/behaviors that no longer exist |
| **Duplicate test methods** | Consolidate into a shared test class (e.g., `TimeFormatterTests` instead of testing formatting in every VM test) |

### Rules

- Review every test file, not just files for changed code
- A test should fail if the behavior it describes breaks — if it can never fail, delete it
- Test names should describe the scenario and expected outcome: `MethodName_Scenario_ExpectedResult`

---

## Phase 6: Write Additional Tests

**Goal:** Fill coverage gaps identified during the audit, especially for newly refactored code.

### Where to Look for Gaps

1. **Refactored code** — any method you simplified or extracted needs test coverage
2. **Public API methods** with zero test callers
3. **Error/edge-case paths** — what happens when input is null, empty, or invalid?
4. **State transitions** — load → play → pause → stop → resume lifecycles
5. **Property change side effects** — setting volume should auto-unmute, setting speed should persist, etc.
6. **Command guard conditions** — commands that no-op when preconditions aren't met

### Test Writing Guidelines

- Follow existing test patterns in the project (same mocking framework, naming conventions, setup patterns)
- Each test should verify **one behavior** — if you need multiple assertions, they should be about the same logical outcome
- Use `[Theory]` with `[InlineData]` for parameterized edge cases (e.g., clamping ranges)
- Prefer raising mock events (`Raise.Event<>()`) over reflection to simulate engine state
- Always clean up temp files/directories in a `finally` block

---

## Phase 7: Final Verification

1. **Build** — zero errors, zero new warnings
2. **Run all tests** — zero failures
3. **Launch the application** — verify the UI looks and behaves identically
4. **Compare test count** — document how many tests were removed, added, and the net change
5. **Review the diff** — ensure no accidental behavioral changes slipped in

---

## Summary Checklist

```
[ ] Phase 1: Read implementation plan and identify protected code
[ ] Phase 2: Audit and remove dead code (build + test after)
[ ] Phase 3: Simplify duplicated/complex patterns (build + test after)
[ ] Phase 4: Optimize hot-path performance (build + test after)
[ ] Phase 5: Review and clean up all existing tests (build + test after)
[ ] Phase 6: Write tests for coverage gaps (build + test after)
[ ] Phase 7: Final verification (build + test + manual launch)
```
