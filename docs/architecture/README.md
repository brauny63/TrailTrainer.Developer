# TrailTrainer.Developer – Architecture

## 1. Purpose

`TrailTrainer.Developer` is a standalone developer toolkit for managing and automating the development workflow of TrailTrainer projects.

The toolkit is intentionally separated from `TrailTrainer.TerrainEngine`.

`TrailTrainer.TerrainEngine` contains domain functionality for terrain processing, while `TrailTrainer.Developer` provides tooling for the software development process itself.

The long-term goal is to provide a structured workflow for:

- Developer Tasks
- Git repositories
- Feature branches
- Build and test execution
- Codex-assisted implementation
- Commits and pushes
- Pull Requests
- GitHub integration

The architecture should remain generic enough that the toolkit can later be used for projects other than TrailTrainer.

---

## 2. Design Principles

The following principles apply to the entire solution.

### 2.1 Separation from product code

`TrailTrainer.Developer` must not contain terrain, GPX, DEM, mesh, VR or other TrailTrainer domain functionality.

It operates on software projects but does not implement their business logic.

### 2.2 Small components

Functionality should be divided into small components with clearly defined responsibilities.

### 2.3 Testability

Core functionality must be testable without requiring GitHub, Codex or other external services.

External systems should therefore be accessed through abstractions.

### 2.4 CLI first

The initial user interface will be a command-line application.

Other interfaces may be added later without changing the core architecture.

### 2.5 Explicit workflows

Development workflows should be represented explicitly rather than hidden inside large scripts.

A Developer Task should have a clearly defined lifecycle.

---

## 3. Solution Structure

The initial solution structure is:

```text
TrailTrainer.Developer
│
├── src
│   ├── TrailTrainer.Developer.Core
│   ├── TrailTrainer.Developer.Git
│   ├── TrailTrainer.Developer.Tasks
│   └── TrailTrainer.Developer.CLI
│
├── tests
│   └── TrailTrainer.Developer.Tests
│
└── docs
    ├── architecture
    └── developer-tasks
```

---

## 4. Components

### TrailTrainer.Developer.Core

Contains the fundamental domain model and abstractions used by the toolkit.

Examples:

- repository descriptions
- workflow state
- command results
- process abstractions
- common error/result types

`Core` must not depend on GitHub, Codex or concrete Git implementations.

---

### TrailTrainer.Developer.Git

Provides Git-related functionality.

Responsibilities may include:

- repository status
- current branch detection
- branch creation
- commit information
- commits
- pushes
- repository validation

Git command execution should be encapsulated behind an abstraction.

---

### TrailTrainer.Developer.Tasks

Handles Developer Tasks.

A Developer Task represents a small, clearly defined development unit such as:

```text
DEV-0001 – Bootstrap TrailTrainer.Developer Solution
DEV-0002 – Implement Git Repository Status
DEV-0003 – Implement Feature Branch Creation
```

Responsibilities include:

- locating task files
- parsing task metadata
- validating tasks
- determining task state
- executing task workflows

Developer Task definitions are stored under:

```text
docs/developer-tasks/
```

---

### TrailTrainer.Developer.CLI

Provides the command-line interface.

The CLI should contain as little business logic as possible.

Its responsibilities are primarily:

1. Parse command-line arguments.
2. Call application services.
3. Display results.

Possible future commands include:

```text
traildev status

traildev task list

traildev task show DEV-0001

traildev task start DEV-0001

traildev task verify DEV-0001
```

---

## 5. Dependency Direction

Dependencies must point inward toward abstractions and domain functionality.

Conceptually:

```text
                 CLI
                  │
          ┌───────┴───────┐
          ▼               ▼
        Tasks            Git
          │               │
          └───────┬───────┘
                  ▼
                 Core
```

`Core` must not reference the other projects.

External integrations should depend on abstractions defined by the appropriate inner layer.

---

## 6. Developer Task Lifecycle

A Developer Task may eventually follow this workflow:

```text
Created
   │
   ▼
Validated
   │
   ▼
Started
   │
   ▼
Feature Branch
   │
   ▼
Implementation
   │
   ▼
Build
   │
   ▼
Tests
   │
   ▼
Commit
   │
   ▼
Push
   │
   ▼
Pull Request
   │
   ▼
Completed
```

Not all of these steps need to be automated initially.

Automation should be introduced incrementally.

---

## 7. External Integrations

The architecture should allow later integration with external systems.

Potential integrations include:

### Git

Local repository operations.

### GitHub

Possible functionality:

- repository information
- Pull Request creation
- Pull Request status
- CI status

### Codex

Possible functionality:

- provide Developer Task instructions
- execute implementation work
- analyze implementation results

These integrations are not required for the initial bootstrap package.

---

## 8. Architecture Rule

A fundamental architecture rule is:

> TrailTrainer.Developer orchestrates development activities but does not contain the product functionality being developed.

This keeps the toolkit independent from `TrailTrainer.TerrainEngine` and other future TrailTrainer projects.

---

## 9. Initial Development Strategy

Development will proceed in small packages.

Each package should:

1. Define its goal.
2. Produce one or more Developer Tasks.
3. Be implemented on a dedicated feature branch.
4. Include automated tests where appropriate.
5. Be reviewed through a Pull Request.

The first implementation task will be:

```text
DEV-0001 – Bootstrap TrailTrainer.Developer Solution
```

This task creates the initial solution and project structure defined in this document.