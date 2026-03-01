# Vido.Core Migration Guide (0.13.0)

This guide covers migration from `Vido.Core` 0.12.x to 0.13.0.

## Summary of impact

`0.13.0` includes contract and API updates intended for the optimized runtime path. Consumer plugins should be rebuilt and validated against this version.

## 1) Event types are now value types

The following event contracts are now `readonly record struct`:

- `PlaybackPositionChangedEvent`
- `PlaybackStateChangedEvent`
- `PlayFileRequestedEvent`
- `VideoLoadedEvent`
- `VideoUnloadedEvent`

### What changes for consumers

- Event payloads are value types, not reference types.
- `null` checks for these payloads are no longer meaningful.
- Use `default(TEvent)` expectations where needed in tests.
- `with` expressions are supported on all event record structs.

## 2) Null-coalescing semantics on string/object payloads

Some event properties now safely coalesce null-initialized backing values:

- `PlayFileRequestedEvent.FilePath` → `string.Empty` when unset/null
- `VideoLoadedEvent.FilePath` → `string.Empty` when unset/null
- `VideoLoadedEvent.Metadata` → empty metadata sentinel when unset/null

## 3) FrozenSet changes

These static sets are now `FrozenSet<string>`:

- `FileNode.VideoExtensions`
- `SettingContribution.ValidTypes`
- `AppSettings.OfficialRegistryUrls`

### Consumer guidance

- `Contains` behavior remains the same (including case-insensitive lookups where configured).
- These sets are immutable; mutation (`Add`/`Remove`) is not supported.

## 4) DropClassifier.ClassifyAll return type

`DropClassifier.ClassifyAll` now returns:

- `(DropClassification Classification, string Path)[]`

instead of a `List<(DropClassification, string)>`.

### Consumer guidance

- Update explicitly-typed list variables to array or `var`.
- Existing index/foreach usage remains equivalent.

## 5) Record conversion: UpdateCheckResult

`UpdateCheckResult` is now a `sealed record`.

### Consumer guidance

- Value equality now compares property values.
- `with` expressions are supported.
- Existing object-initializer usage remains valid.

## 6) Recommended upgrade workflow

1. Update package reference to `Vido.Core` 0.13.0.
2. Rebuild plugin/consumer projects.
3. Run tests and resolve any compile-time contract assumptions.
4. Redeploy plugin binaries and verify runtime activation.
