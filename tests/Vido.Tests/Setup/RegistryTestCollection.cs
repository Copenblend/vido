using Xunit;

namespace Vido.Tests.Setup;

/// <summary>
/// xUnit collection that serialises all test classes which share the same
/// Windows registry paths (UninstallGuid, InstallPath, ProgID, etc.).
/// Without this, xUnit runs test classes in parallel and concurrent reads/writes
/// to the same HKCU keys cause intermittent failures.
/// </summary>
[CollectionDefinition("Registry")]
public sealed class RegistryTestCollection;
