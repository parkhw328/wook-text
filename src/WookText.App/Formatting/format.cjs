// Only bundled parsers are loaded. Never load document code or project configuration.
const prettier = require('./prettier/standalone.js');
const plugins = ['babel', 'estree', 'typescript', 'html', 'postcss', 'markdown', 'yaml', 'graphql']
    .map(name => require(`./prettier/plugins/${name}.js`));
let input = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', chunk => input += chunk);
process.stdin.on('end', async () => {
    try {
        const request = JSON.parse(input);
        const text = await prettier.format(request.text, {
            parser: request.parser, plugins, tabWidth: request.tabWidth, useTabs: request.useTabs, printWidth: 100,
            endOfLine: 'lf', embeddedLanguageFormatting: 'off',
        });
        process.stdout.write(JSON.stringify({ text }));
    } catch (error) {
        process.stderr.write(error.message);
        process.exitCode = 1;
    }
});
