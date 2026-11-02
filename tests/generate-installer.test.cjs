const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { test } = require('node:test');

function fixture(t) {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'wtext-catalog-'));
    t.after(() => fs.rmSync(root, { recursive: true, force: true }));
    const payload = path.join(root, 'payload');
    fs.mkdirSync(payload);
    const output = path.join(root, 'generated', 'payload.nsh');
    const run = () => spawnSync(process.execPath, [path.join(__dirname, '../scripts/generate-installer.cjs'), payload, output], { encoding: 'utf8' });
    return { root, payload, output, run };
}

test('Upgrade catalog retains old owned files and separates obsolete files from the new payload', t => {
    const { payload, output, run } = fixture(t);
    fs.writeFileSync(path.join(payload, 'wText.exe'), 'current application');
    fs.mkdirSync(path.join(payload, 'new component'));
    fs.writeFileSync(path.join(payload, 'new component', 'engine.dll'), 'new payload');
    assert.equal(run().status, 0);
    const catalog = fs.readFileSync(output, 'utf8');
    const obsolete = catalog.match(/!macro ObsoleteFiles Action\n([\s\S]*?)!macroend/)[1];
    assert.match(obsolete, /"WebView2Runtime\\msedge\.dll"/);
    assert.doesNotMatch(obsolete, /"wText\.exe"|"new component\\engine\.dll"/);
    assert.match(catalog, /StrCmp \$Candidate "new component\\engine\.dll" found/);
    assert.match(catalog, /StrCmp \$Candidate "\.wtext-retained" found/);
    assert.doesNotMatch(catalog, /RMDir \/r|Delete "\$INSTDIR\\\*"/);
});

test('A filename containing NSIS variable syntax fails before emitting an include', t => {
    const { payload, output, run } = fixture(t);
    fs.writeFileSync(path.join(payload, '$INSTDIR.dll'), 'unsafe filename');
    assert.notEqual(run().status, 0);
    assert.equal(fs.existsSync(output), false);
});

test('Payload symlinks cannot make unrelated files part of the ownership catalog', t => {
    const { root, payload, output, run } = fixture(t);
    const unrelated = path.join(root, 'user-document.txt');
    fs.writeFileSync(unrelated, 'unrelated data');
    try { fs.symlinkSync(unrelated, path.join(payload, 'linked.dll')); }
    catch (error) { if (process.platform === 'win32' && error.code === 'EPERM') { t.skip('Creating symlinks requires Windows developer mode.'); return; } throw error; }
    assert.notEqual(run().status, 0);
    assert.equal(fs.existsSync(output), false);
    assert.equal(fs.readFileSync(unrelated, 'utf8'), 'unrelated data');
});
