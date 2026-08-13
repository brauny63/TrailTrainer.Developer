# DEV-0001 – Bootstrap TrailTrainer.Developer Solution

## Goal

Create the initial .NET solution and project structure for `TrailTrainer.Developer` according to the architecture defined in:

`docs/architecture/README.md`

The result must provide a clean, compilable and testable foundation for future Developer Toolkit tasks.

## Scope

Create the following solution:

```text
TrailTrainer.Developer.sln
```

Create the source projects:

```text
src/
├── TrailTrainer.Developer.Core/
├── TrailTrainer.Developer.Git/
├── TrailTrainer.Developer.Tasks/
└── TrailTrainer.Developer.CLI/
```

Create the test project:

```text
tests/
└── TrailTrainer.Developer.Tests/
```

## Target Framework

Use:

```text
net10.0
```

for all projects.

## Project Types

Create the following projects:

```text
TrailTrainer.Developer.Core
    Class Library

TrailTrainer.Developer.Git
    Class Library

TrailTrainer.Developer.Tasks
    Class Library

TrailTrainer.Developer.CLI
    Console Application

TrailTrainer.Developer.Tests
    xUnit Test Project
```

## Project References

Configure the initial dependency structure as follows:

```text
TrailTrainer.Developer.Git
    -> TrailTrainer.Developer.Core

TrailTrainer.Developer.Tasks
    -> TrailTrainer.Developer.Core

TrailTrainer.Developer.CLI
    -> TrailTrainer.Developer.Core
    -> TrailTrainer.Developer.Git
    -> TrailTrainer.Developer.Tasks

TrailTrainer.Developer.Tests
    -> TrailTrainer.Developer.Core
    -> TrailTrainer.Developer.Git
    -> TrailTrainer.Developer.Tasks
```

`TrailTrainer.Developer.Core` must not reference any other TrailTrainer.Developer project.

## Initial Cleanup

Remove generated placeholder files that are not required, such as:

```text
Class1.cs
```

Do not add application functionality as part of this task.

## Verification

The complete solution must build successfully:

```text
dotnet build
```

All tests must pass:

```text
dotnet test
```

The test project may initially contain only the default/bootstrap test required to verify the test infrastructure.

## Constraints

Do not implement:

- Git operations
- Developer Task parsing
- GitHub integration
- Codex integration
- workflow orchestration
- terrain-related functionality

These belong to later Developer Tasks.

## Acceptance Criteria

DEV-0001 is complete when:

1. `TrailTrainer.Developer.sln` exists.
2. All five projects exist.
3. All projects target `net10.0`.
4. Project references follow the architecture defined above.
5. `TrailTrainer.Developer.Core` has no project dependency on another toolkit project.
6. Unnecessary generated placeholder files have been removed.
7. `dotnet build` succeeds.
8. `dotnet test` succeeds.
9. No functionality outside the defined scope has been implemented.