const vscode = require('vscode');
const fs = require('node:fs/promises');
const path = require('node:path');

exports.run = async function () {
    const root = process.env.RAFTER_EDITOR_WORKSPACE;
    const resultPath = process.env.RAFTER_EDITOR_RESULT;
    if (!root || !resultPath) {
        throw new Error('Set RAFTER_EDITOR_WORKSPACE and RAFTER_EDITOR_RESULT to the isolated review paths.');
    }
    const result = { vscode: vscode.version, extensions: [], checks: [], consumers: [] };
    try {
        for (const id of ['ms-dotnettools.csharp', 'ms-dotnettools.csdevkit']) {
            const extension = vscode.extensions.getExtension(id);
            result.extensions.push({ id, version: extension?.packageJSON.version ?? null });
        }
        for (const sourcePath of [path.join(root, 'Program.cs'), process.env.RAFTER_EDITOR_FILEAPP].filter(Boolean)) {
            const document = await vscode.workspace.openTextDocument(sourcePath);
            await vscode.window.showTextDocument(document);
            const deadline = Date.now() + 120000;
            let diagnostics = [];
            do {
                diagnostics = vscode.languages.getDiagnostics(document.uri);
                if (['RAF001', 'RAF002', 'RAF003'].every(id => diagnostics.some(item => diagnosticId(item) === id))) {
                    break;
                }
                await new Promise(resolve => setTimeout(resolve, 500));
            } while (Date.now() < deadline);
            const evidence = { file: sourcePath, diagnostics: diagnostics.map(item => ({
                id: diagnosticId(item), severity: item.severity, message: item.message,
                line: item.range.start.line + 1, column: item.range.start.character + 1
            })) };
            for (const [id, severity] of [['RAF001', 0], ['RAF002', 1], ['RAF003', 0]]) {
                const passed = diagnostics.some(item => diagnosticId(item) === id && item.severity === severity);
                result.checks.push({ file: sourcePath, name: `${id} live diagnostic`, passed });
            }
            result.checks.push({ file: sourcePath, name: 'Automatic import resolves Rafter', passed: !diagnostics.some(item =>
                diagnosticId(item) === 'CS0103' || diagnosticId(item) === 'CS0246') });

            const text = document.getText();
            const offset = text.indexOf('command.Option') + 'command.'.length;
            const completions = await vscode.commands.executeCommand('vscode.executeCompletionItemProvider',
                document.uri, document.positionAt(offset));
            const labels = (completions?.items ?? []).map(item => typeof item.label === 'string' ? item.label : item.label.label);
            evidence.completions = labels;
            result.consumers.push(evidence);
            result.checks.push({ file: sourcePath, name: 'Command member completion', passed: ['Option<>', 'Target', 'RunAsync'].every(name => labels.includes(name)) });
        }
        result.passed = result.checks.every(check => check.passed);
        if (!result.passed) {
            throw new Error('Editor evidence did not satisfy all checks; see the JSON report.');
        }
    } catch (error) {
        result.error = String(error.stack ?? error);
        throw error;
    } finally {
        await fs.writeFile(resultPath, JSON.stringify(result, null, 2));
    }
};

function diagnosticId(diagnostic) {
    return String(typeof diagnostic.code === 'object' ? diagnostic.code.value : diagnostic.code);
}
