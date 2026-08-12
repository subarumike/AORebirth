# AORebirth Development Authority

Status: Permanent project policy.

## Authority

AORebirth has one authoritative source tree and one authoritative codebase.
Windows is the authoritative development platform and source of truth. Linux is
the production deployment platform.

There is no separate Linux implementation and no separate Windows
implementation. Linux hosts builds produced from the same AORebirth source tree;
it must never diverge into an independent implementation.

This policy governs future AI agents, developers, contributors, validation, and
production promotion. It complements the system description in
`docs/project/ARCHITECTURE.md` and the approved commands in
`docs/ai/WORKFLOW.md`.

## Windows authority

All development work originates on Windows, including:

- architecture and design;
- programming and debugging;
- unit and integration testing;
- regression verification;
- acceptance testing;
- approval of work for deployment.

All feature work begins in the authoritative Windows source tree. Windows
validation remains the acceptance gate for production.

## Linux role

Linux exists to:

- host production services;
- execute validated builds;
- run production infrastructure;
- perform deployment verification;
- perform platform-compatibility validation.

Linux is not the primary development or implementation environment. Linux
verification is an additional production gate, not a replacement for Windows
development, testing, or acceptance.

Linux-specific work must remain limited to operating-system concerns such as
deployment, hosting, packaging, service management, monitoring, permissions,
filesystem layout, and platform integration. It must not create Linux-only core
behavior.

## Authoritative workflow

```text
Windows development
        |
Implementation
        |
Unit testing
        |
Integration testing
        |
Regression testing
        |
Acceptance testing
        |
Approved Windows-validated source/build
        |
Linux deployment build
        |
Linux compatibility and deployment verification
        |
Production promotion
```

Every production deployment must originate from Windows-validated source.
Linux deployment verification may block promotion, but it does not supersede
the Windows acceptance result.

## Permanent rules

1. Windows is the authoritative development platform.
2. Linux is the production deployment target.
3. All feature work begins on Windows.
4. A bug discovered on Linux must be corrected in the Windows source tree first,
   unless evidence proves it is exclusively a Linux deployment or hosting issue.
5. Linux must never become the primary implementation environment.
6. Authentication logic must remain functionally identical across platforms.
7. Gameplay behavior must remain functionally identical across platforms.
8. Packet behavior must remain functionally identical across platforms.
9. Database behavior must remain functionally identical across platforms unless
   a proven platform-specific infrastructure requirement applies.
10. Linux-specific source changes are restricted to deployment, hosting,
    packaging, service management, monitoring, permissions, filesystem layout,
    and other operating-system boundaries.
11. The production Linux server must never diverge into an independent
    implementation.
12. Every production deployment must originate from Windows-validated source.
13. Windows validation is the production acceptance gate.
14. Linux verification is an additional gate, not a replacement for Windows
    validation.
15. A Linux deployment issue that requires source changes must be corrected in
    the authoritative Windows source tree first.
16. AORebirth shall retain exactly one authoritative codebase.

Platform-specific adapters are permitted only when required by the operating
system. They must implement the same shared contracts and preserve observable
authentication, gameplay, packet, and database semantics.

## Linux issue classification

When Linux verification fails, first classify the failure using evidence:

- A deployment-only issue may be corrected in Linux packaging, configuration,
  service management, permissions, monitoring, or filesystem layout.
- A compatibility defect requiring source code must be repaired in the
  authoritative Windows source tree, validated on Windows, rebuilt for Linux,
  and verified again on Linux.
- A behavior difference in authentication, gameplay, packets, or database
  semantics is a cross-platform defect, not an acceptable Linux variation.

Production hot-fixes must not create an untracked Linux-only source fork. Any
emergency deployment change must be represented in the authoritative source
tree and pass the normal gates before it becomes the maintained solution.

## Guidance for AI agents

All AI assistants working on AORebirth must follow this policy. They must:

- implement in the authoritative Windows source tree;
- validate on Windows using repository-approved workflows;
- promote only Windows-validated work to Linux;
- treat Linux checks as deployment and compatibility verification;
- keep platform-specific code narrow and contract-compatible;
- return every required source fix to the Windows source tree first.

AI assistants must not:

- create Linux-only implementations of core systems;
- treat Linux as the authoritative environment;
- introduce separate Windows and Linux behavior;
- replace or bypass the Windows development and acceptance workflow;
- maintain production-only source edits outside the authoritative repository.

## Change control

This is a permanent project-governance policy. Changes to it must be deliberate,
documentation-only governance changes approved by the project owner. Runtime or
deployment work must not silently weaken, reinterpret, or bypass it.
