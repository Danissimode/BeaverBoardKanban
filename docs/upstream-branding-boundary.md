# Upstream Branding Boundary

Beaver Board is the product brand. KittyClaw remains the technical upstream-compatible namespace.

## Rule

Do not rename solution, projects, namespaces, database schema, or core file paths unless explicitly approved. All user-visible branding must go through centralized branding configuration.

## What stays as KittyClaw

- `KittyClaw.slnx` — solution file
- `KittyClaw.Core/` — core library
- `KittyClaw.Web/` — web application
- `KittyClaw.QaRunner/` — QA runner
- `KittyClaw.ClaudeMock/` — mock CLI
- `namespace KittyClaw.*` — all C# namespaces
- Class names that are core/upstream
- Database/table names tied to migrations
- API routes already in use
- Storage paths (`%APPDATA%/KittyClaw/`)
- Old docs describing upstream mechanics

## What becomes Beaver Board

- Browser title
- Header/app title in UI
- Sidebar/navigation labels
- Favicon/logo (user-visible assets)
- README intro for fork
- Docs landing page
- Packaging display name
- Theme colors
- Branding assets
- About page
- Empty states / onboarding copy
- Localization strings rendered to users

## Why this works

Many forks/products live this way. The engine has one name, the product has another. This allows pulling upstream updates while maintaining a distinct product identity.

## Centralized config

All branding values live in `branding/branding.json`. The UI reads from this config. Do not hardcode product names in components — reference the config.
