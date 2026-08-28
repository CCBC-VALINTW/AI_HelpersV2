// Vendors the self-hosted TinyMCE distribution into wwwroot/lib/tinymce - run via `npm run
// build`. TinyMCE isn't esbuild-bundle friendly (its skins/icons/plugins are fetched dynamically
// at runtime from base_url, not statically imported), so the officially documented self-hosting
// approach is to copy the built package wholesale rather than bundle it, matching that guidance
// rather than risk missing-asset bugs from trimming it ourselves. No API key, no tiny.cloud - the
// runtime dependency is purely this vendored copy, matching the same "no CDN" reasoning as the
// esbuild pipeline it replaces.

import { cpSync, rmSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const dest = fileURLToPath(new URL('../wwwroot/lib/tinymce', import.meta.url))

rmSync(dest, { recursive: true, force: true })
cpSync(fileURLToPath(new URL('node_modules/tinymce', import.meta.url)), dest, {
    recursive: true,
    filter: (src) => !/\.(md|ts)$/.test(src) && !/(license\.md|notices\.txt|package\.json|bower\.json|composer\.json)$/.test(src),
})

console.log(`Vendored TinyMCE into ${dest}`)
