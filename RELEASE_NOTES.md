# Release Notes

## v1.3.0 — 2026-02-11

- Replace CodexBar CLI with native provider APIs for direct HTTP calls
- Improve compact view alignment and add CLI auto-refresh
- Improve CLI token refresh with multiple strategies
- Add fix to gitignore

## v1.4.0 — 2026-02-11

- Rename display modes: "Vertical" → "Normal", "Compact" → "Vertical" for clarity
- Fix Sonnet percentage calculation bug (1% was displayed as 100%)
- Show reset time inline with progress bar in HH:mm format (e.g., "01:00 Th")
- Improve budget emphasis: highlight daily budget only for Weekly (Claude/Codex) or Pro (Gemini)
- Dim percentage values, keep budget brackets bright for key windows
- Improve vertical view alignment, remove unnecessary indentation
- Shorten footer labels in vertical view (Ref, Opt instead of Refresh, Options)
- Split keyboard shortcuts to separate line in normal view
