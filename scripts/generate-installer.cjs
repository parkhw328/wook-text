const fs = require('node:fs');
const path = require('node:path');
const [payload, output] = process.argv.slice(2);
if (!payload || !output) throw new Error('Usage: generate-installer.cjs <publish folder> <output .nsh>');
const root = path.resolve(payload);
function walk(folder) {
    return fs.readdirSync(folder, { withFileTypes: true }).flatMap(entry => {
        const full = path.join(folder, entry.name);
        if (entry.isSymbolicLink()) throw new Error('Payload links are not allowed: ' + full);
        return entry.isDirectory() ? walk(full) : [path.relative(root, full).split(path.sep).join('/')];
    });
}
const current = walk(root).sort();
const legacy = fs.readFileSync(path.join(__dirname, '../installer/legacy-files.txt'), 'utf8').split(/\r?\n/).filter(s => s && !s.startsWith('#'));
const known = [...new Set([...current, ...legacy])].sort();
for (const name of known) {
    if (!/^[\x20-\x7e]+$/.test(name) || /["$:*?<>|\\]/.test(name) || name.startsWith('/') || name.split('/').some(p => !p || p === '.' || p === '..')) throw new Error('Unsafe payload name: ' + name);
}
const directories = [...new Set(known.flatMap(name => {
    const parts = name.split('/'); parts.pop();
    return parts.map((_, i) => parts.slice(0, i + 1).join('/'));
}))].sort((a, b) => b.length - a.length || a.localeCompare(b));
const nsis = name => name.replaceAll('/', '\\');
const lines = ['; Generated from the current payload and the retained ownership catalog.'];
for (const [macro, names] of [['KnownFiles', known], ['ObsoleteFiles', known.filter(n => !current.includes(n))], ['KnownDirectories', directories]]) {
    lines.push(`!macro ${macro} Action`);
    for (const name of names) lines.push(`  !insertmacro \${Action} "${nsis(name)}"`);
    lines.push('!macroend');
}
lines.push('Function IsKnownPath', '  StrCpy $KnownPath 1');
for (const name of [...known, ...directories, 'Uninstall.exe', '.wtext-retained']) lines.push(`  StrCmp $Candidate "${nsis(name)}" found`);
lines.push('  StrCpy $KnownPath 0', '  found:', 'FunctionEnd');
fs.mkdirSync(path.dirname(path.resolve(output)), { recursive: true });
fs.writeFileSync(output, lines.join('\n') + '\n', 'utf8');
console.log(`Generated installer ownership catalog: ${current.length} current, ${known.length} known files.`);
