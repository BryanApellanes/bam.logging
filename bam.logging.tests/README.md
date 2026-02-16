# bam.logging.tests

Unit tests for the bam.logging library using the BAM menu-driven test runner.

## Overview

`bam.logging.tests` is an executable test project that validates the logging infrastructure provided by `bam.logging`. It uses the BAM framework's own menu-driven test runner (`BamConsoleContext.StaticMain`) rather than xUnit or NUnit. Tests are defined using the `[UnitTestMenu]` attribute with `Selector` properties for interactive selection, and use the `When.A<T>()` fluent assertion API.

The project currently contains a single test class, `LoggableShould`, which validates that the `Loggable` base class correctly fires generic event handlers via its `FireEvent` method. The test creates a `TestLoggable` instance, subscribes to a custom event, fires it with a random value, and asserts that the event was received with the expected data.

Supporting test infrastructure includes `TestLoggable` (a minimal `Loggable` subclass that exposes a `TestEvent`) and `TestEventArgs` (a simple `EventArgs` subclass carrying a string `Value` property).

## Key Classes

| Class | Description |
|---|---|
| `Program` | Entry point that delegates to `BamConsoleContext.StaticMain(args)` for menu-driven test execution. |
| `LoggableShould` | Test class with selector `"ls"`. Contains `ShouldFireGenericEventHandler` test validating `Loggable.FireEvent`. |
| `TestLoggable` | Minimal `Loggable` subclass exposing a `TestEvent` of type `EventHandler<TestEventArgs>` and a `TestFire` method. |
| `TestEventArgs` | Simple `EventArgs` with a `Value` string property, used to pass data through test events. |

## Dependencies

### Project References
- `bam.console` -- `BamConsoleContext` for the menu-driven test runner entry point
- `bam.test` -- `UnitTestMenuContainer`, `[UnitTestMenu]`, `[UnitTest]` attributes, `When.A<T>()` fluent API

### Package References
- None

## Target Framework
- `net10.0`
- Output type: `Exe`

## Usage Examples

### Running the tests
```bash
# Run all unit tests (use --ut, not /ut in Git Bash)
dotnet run --project submodules/bam.logging/bam.logging.tests/bam.logging.tests.csproj -- --ut
```

### Running interactively
```bash
# Launch the menu-driven runner and select "ls" for LoggableShould tests
dotnet run --project submodules/bam.logging/bam.logging.tests/bam.logging.tests.csproj
```

### Test structure example
```csharp
[UnitTestMenu("Loggable Should", Selector = "ls")]
public class LoggableShould : UnitTestMenuContainer
{
    [UnitTest]
    public void ShouldFireGenericEventHandler()
    {
        When.A<Loggable>("Uses Fire() to fire a generic event", () => new TestLoggable(), (testLoggable) =>
        {
            // Arrange, Act, Return results
        })
        .It
        .ShouldPass(because =>
        {
            // Assert with because.ItsTrue(...)
        })
        .SoBeHappy()
        .UnlessItFailed();
    }
}
```

## Known Gaps / Not Yet Implemented

- **Limited test coverage**: Only one test exists (`ShouldFireGenericEventHandler`). The `TextFileLogger`, `MultiTargetLogger`, `LogProvider`, `LogEventCollection`, `ErrorMessage`, and `WarningMessage` classes have no corresponding tests.
