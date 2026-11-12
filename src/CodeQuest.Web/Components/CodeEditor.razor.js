const monacoBase = "https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/vs";

let monacoPromise;
let nextId = 1;
const editors = new Map();

function loadMonaco() {
    monacoPromise ??= new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = `${monacoBase}/loader.js`;
        script.onerror = () => reject(new Error("Could not load the Monaco loader."));
        script.onload = () => {
            window.MonacoEnvironment = {
                getWorkerUrl: () => `data:text/javascript;charset=utf-8,${encodeURIComponent(
                    `self.MonacoEnvironment = { baseUrl: "${monacoBase}/" }; importScripts("${monacoBase}/base/worker/workerMain.js");`)}`
            };
            window.require.config({ paths: { vs: monacoBase } });
            window.require(["vs/editor/editor.main"], () => resolve(window.monaco), reject);
        };
        document.head.appendChild(script);
    }).catch(error => {
        monacoPromise = undefined;
        throw error;
    });
    return monacoPromise;
}

export async function create(element, code) {
    const monaco = await loadMonaco();
    const editor = monaco.editor.create(element, {
        value: code,
        language: "csharp",
        theme: "vs-dark",
        automaticLayout: true,
        minimap: { enabled: false },
        quickSuggestions: false,
        suggestOnTriggerCharacters: false
    });
    const id = nextId++;
    editors.set(id, editor);
    return id;
}

export function getValue(id) {
    return editors.get(id).getValue();
}

export function setValue(id, code) {
    editors.get(id).setValue(code);
}

export function insert(id, text) {
    const editor = editors.get(id);
    editor.executeEdits("code-quest", [{ range: editor.getSelection(), text, forceMoveMarkers: true }]);
    editor.focus();
}

export function setErrors(id, errors) {
    const model = editors.get(id).getModel();
    const markers = errors.map(error => {
        const line = Math.min(Math.max(error.line, 1), model.getLineCount());
        return {
            severity: window.monaco.MarkerSeverity.Error,
            message: error.message,
            startLineNumber: line,
            startColumn: Math.min(error.column, model.getLineMaxColumn(line)),
            endLineNumber: line,
            endColumn: model.getLineMaxColumn(line)
        };
    });
    window.monaco.editor.setModelMarkers(model, "code-quest", markers);
}

export function dispose(id) {
    const editor = editors.get(id);
    if (!editor) {
        return;
    }
    const model = editor.getModel();
    editor.dispose();
    model?.dispose();
    editors.delete(id);
}
