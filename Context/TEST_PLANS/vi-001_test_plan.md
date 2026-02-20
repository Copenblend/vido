# vi-001: Solution Scaffold & Empty Window — Test Plan

## Manual Tests

### MT-1: Solution Builds Successfully
**Steps:**
1. Open a terminal at `c:\source\vido`
2. Run `dotnet build Vido.sln`
**Expected Result:** Build succeeds with 0 warnings and 0 errors.

### MT-2: Application Launches
**Steps:**
1. Run `dotnet run --project src/Vido.App`
2. Observe the window that appears
**Expected Result:** A dark frameless window appears, centered on screen, with default size ~1280x720. The window title (taskbar) reads "Vido". A faint "Vido" watermark is centered in the window.

### MT-3: Window is Movable
**Steps:**
1. Launch the app
2. Click and drag the top ~30px of the window (caption area)
3. Move the mouse while holding the button
**Expected Result:** The window follows the mouse and repositions on screen.

### MT-4: Window is Resizable from Edges and Corners
**Steps:**
1. Launch the app
2. Hover the mouse over each edge (left, right, top, bottom) and each corner
3. Click and drag to resize
**Expected Result:** The resize cursor appears at edges/corners. The window resizes smoothly in the dragged direction.

### MT-5: Minimum Window Size is Enforced
**Steps:**
1. Launch the app
2. Drag a corner or edge to make the window as small as possible
**Expected Result:** The window cannot be resized smaller than 800x600 pixels (accounting for DPI scaling).

### MT-6: Window Background is Dark
**Steps:**
1. Launch the app
2. Observe the window background color
**Expected Result:** The background is a dark gray (#1f1f1f) with a subtle border (#2b2b2b), matching VS Code Dark Modern theme aesthetics.

### MT-7: All Tests Pass
**Steps:**
1. Run `dotnet test Vido.sln`
**Expected Result:** All tests pass (1 passed, 0 failed, 0 skipped).

### MT-8: DI Container Initializes
**Steps:**
1. Launch the app
2. Confirm the window appears (MainWindow is resolved from DI)
**Expected Result:** The app starts without exceptions. The MainWindow is created via dependency injection and displayed.

## Regression Tests

_No prior functionality exists — this is the first ticket. These tests form the baseline for future regression._

### RT-1: Solution Structure Integrity
**Precondition:** Solution has been built at least once.
**Steps:**
1. Verify all 7 projects exist in the solution: Vido.Core, Vido.Services, Vido.ViewModels, Vido.Views, Vido.PluginHost, Vido.App, Vido.Tests
2. Run `dotnet build Vido.sln`
**Expected Result:** All projects compile. Inter-project references resolve correctly.

## Unit Tests
- [x] SmokeTests.TestInfrastructure_IsWorking — Verifies the test infrastructure (xUnit, project references) is wired up correctly
