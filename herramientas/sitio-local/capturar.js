// Captura pantallas del sitio local con Edge sin ventana (CDP), para revisar el diseño cuando la
// captura de la extensión no funciona. Inicia sesión con admin.transporte; la contraseña se lee de
// privado/CREDENCIALES_USUARIOS.txt y nunca se imprime.
//
//   node herramientas/sitio-local/capturar.js <salida.png> <ruta> [ancho=1366] [esperaMs=1500] [js] [archivos]
//   node herramientas/sitio-local/capturar.js lista.png /Views/ListaDespachos.aspx 1366 2000
//   varias a la vez: ... a.png /Views/A.aspx -- b.png /Views/B.aspx 390   (SGV_ALTO=1200 limita el alto de la imagen)
//
// [js] se ejecuta en la página antes de capturar (p. ej. un clic); su resultado se imprime.
// [archivos] = "selectorCss|ruta1|ruta2": pone esos archivos en el <input type=file> antes de ejecutar [js]
// (para probar una subida: el [js] pulsa el botón).
const fs = require('fs'), path = require('path'), os = require('os'), { spawn } = require('child_process');

const BASE = process.env.SGV_SITIO || 'http://localhost:50291';
const USUARIO = process.env.SGV_USUARIO || 'admin.transporte';
const repo = path.resolve(__dirname, '..', '..');
const esperar = ms => new Promise(r => setTimeout(r, ms));

function clave() {
    const texto = fs.readFileSync(path.join(repo, 'privado', 'CREDENCIALES_USUARIOS.txt'), 'utf8');
    const m = texto.match(new RegExp(USUARIO.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '\\s+(\\S+)'));
    if (!m) throw new Error('No está ' + USUARIO + ' en privado/CREDENCIALES_USUARIOS.txt');
    return m[1];
}

(async () => {
    const trabajos = [];
    let actual = [];
    for (const a of process.argv.slice(2)) { if (a === '--') { trabajos.push(actual); actual = []; } else actual.push(a); }
    if (actual.length) trabajos.push(actual);
    if (!trabajos.length || trabajos.some(t => t.length < 2)) { console.error('Uso: capturar.js <salida.png> <ruta> [ancho] [esperaMs] [js]'); process.exit(2); }

    const edge = ['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', 'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
                  'C:/Program Files/Google/Chrome/Application/chrome.exe'].find(fs.existsSync);
    if (!edge) throw new Error('No se encontró Edge ni Chrome.');
    const puerto = 9300 + Math.floor(Math.random() * 500);
    const perfil = fs.mkdtempSync(path.join(os.tmpdir(), 'sgv-captura-'));
    const proc = spawn(edge, ['--headless=new', '--remote-debugging-port=' + puerto, '--user-data-dir=' + perfil, '--no-first-run',
                              '--ignore-certificate-errors', '--hide-scrollbars', 'about:blank'], { stdio: 'ignore' });
    try {
        let destino;
        for (let i = 0; i < 40 && !destino; i++) {
            await esperar(250);
            try { destino = (await (await fetch('http://127.0.0.1:' + puerto + '/json')).json()).find(t => t.type === 'page'); } catch (e) { }
        }
        if (!destino) throw new Error('Edge no respondió.');
        const ws = new WebSocket(destino.webSocketDebuggerUrl);
        await new Promise((ok, mal) => { ws.onopen = ok; ws.onerror = mal; });
        let id = 0; const pendientes = new Map(), errores = [];
        ws.onmessage = ev => {
            const m = JSON.parse(ev.data);
            if (m.id && pendientes.has(m.id)) { pendientes.get(m.id)(m); pendientes.delete(m.id); }
            else if (m.method === 'Runtime.exceptionThrown') errores.push(m.params.exceptionDetails.exception?.description || m.params.exceptionDetails.text);
            else if (m.method === 'Runtime.consoleAPICalled' && m.params.type === 'error') errores.push(m.params.args.map(a => a.value || a.description).join(' '));
        };
        const cdp = (method, params = {}) => new Promise((ok, mal) => {
            const n = ++id;
            pendientes.set(n, m => m.error ? mal(new Error(method + ': ' + m.error.message)) : ok(m.result));
            ws.send(JSON.stringify({ id: n, method, params }));
        });
        const evaluar = async expr => (await cdp('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true })).result.value;
        const ir = async url => { await cdp('Page.navigate', { url }); for (let i = 0; i < 80; i++) { await esperar(250); if (await evaluar('document.readyState') === 'complete') return; } };

        await cdp('Page.enable'); await cdp('Runtime.enable');
        await ir(BASE + '/Views/Login.aspx');
        await evaluar(`(function(){var u=document.querySelector('[id$=txtUsername]'),p=document.querySelector('[id$=txtPassword]');
            u.value=${JSON.stringify(USUARIO)};p.value=${JSON.stringify(clave())};document.querySelector('[id$=btnLogin]').click();})()`);
        await esperar(2500);
        if (await evaluar('!!document.querySelector("[id$=txtPassword]")')) throw new Error('No se pudo iniciar sesión.');

        for (const [salida, ruta, ancho = '1366', espera = '1500', js, archivos] of trabajos) {
            errores.length = 0;
            await cdp('Emulation.setDeviceMetricsOverride', { width: +ancho, height: 900, deviceScaleFactor: 1, mobile: +ancho < 600 });
            await ir(BASE + ruta);
            await esperar(+espera);
            if (archivos) {
                const [selector, ...rutas] = archivos.split('|');
                const raiz = (await cdp('DOM.getDocument')).root.nodeId;
                const nodo = (await cdp('DOM.querySelector', { nodeId: raiz, selector })).nodeId;
                if (!nodo) throw new Error('No hay ' + selector + ' en ' + ruta);
                await cdp('DOM.setFileInputFiles', { nodeId: nodo, files: rutas.map(r => path.resolve(r)) });
                await esperar(300);
            }
            if (js) { console.log('js →', JSON.stringify(await evaluar(js))); await esperar(+espera); }
            const alto = Math.min(+process.env.SGV_ALTO || 6000, await evaluar('Math.max(document.documentElement.scrollHeight, document.body.scrollHeight)'));
            await cdp('Emulation.setDeviceMetricsOverride', { width: +ancho, height: alto, deviceScaleFactor: 1, mobile: +ancho < 600 });
            await esperar(400);
            const img = await cdp('Page.captureScreenshot', { format: 'png' });
            fs.writeFileSync(salida, Buffer.from(img.data, 'base64'));
            console.log(salida + '  ' + ancho + '×' + alto + (errores.length ? '  ERRORES JS: ' + errores.join(' | ') : ''));
        }
        ws.close();
    } finally {
        proc.kill();
        await esperar(500);
        try { fs.rmSync(perfil, { recursive: true, force: true }); } catch (e) { }
    }
})().catch(e => { console.error('ERROR: ' + e.message); process.exit(1); });
