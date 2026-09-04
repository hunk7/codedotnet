# ADR-003: Monaco Editor

## Status

Accepted

## Decision

Use Monaco Editor as the code editor for `Program.cs`.

## Reasons

- Professional, IDE-grade editor experience (the same editor used by Visual Studio Code).
- C# colorization/syntax highlighting out of the box.
- Built-in support for diagnostics and inline markers, used to surface Roslyn compiler
  diagnostics directly in the editor gutter/underlines.
- Configurable editor behavior (font size, minimap, word wrap, etc.) exposed via the app's
  Settings panel.
- Familiar interaction patterns and keyboard shortcuts (Find/Replace, Undo/Redo, multi-cursor)
  that most developers already know.
