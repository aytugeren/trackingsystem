// Offline-safe fallback: avoid fetching Google Fonts during build
// Next.js attempted to fetch Inter from Google and timed out in restricted builds.
// Use default system font stack by exporting an empty CSS variable.
export const inter: { variable: string } = { variable: '' }
