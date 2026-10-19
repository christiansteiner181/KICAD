// HLKS-CAD Modul 2 (Auftrag B3): STEP/IGES ueber occt-import-js 0.0.23 (LGPL-2.1, OpenCascade als WebAssembly) lesen
// und als JSON nach stdout schreiben. Das Paket wird unveraendert benutzt und laeuft als eigener Node-Prozess, getrennt
// vom .NET-Kern (siehe LIESMICH.md). Die Auswertung (Flaechen, Stutzen, Ansichten) macht der Kern (NetzAnalyse.cs).
//
// Aufruf:
//   node step_lesen.mjs --datei <pfad> [--art step|iges] [--paket <ordner mit node_modules>]
//   node step_lesen.mjs --pruefen [--paket <ordner>]      (prueft nur, ob das Paket geladen werden kann)
//
// Paketordner: --paket, sonst HLKS_STEP_PAKET, sonst %USERPROFILE%\HLKS_CAD_build\step (node_modules nie in OneDrive).
//
// Ausgabe (format 1), Laengen in mm:
//   { format, ok: true, art, einheit: "mm", occt: "<version>", vernetzung: {...}, baum: {name, netze, kinder},
//     netze: [ { name, farbe, ecken: [x,y,z,...], normalen: [...], dreiecke: [a,b,c,...],
//                flaechen: [ { erstes, letztes, farbe } ] } ], meldungen: [...] }
//   Fehler: { format, ok: false, fehler, vorschlag, meldungen } und Exit-Code 2 (Datei), 3 (Paket fehlt), 4 (Aufruf), 5 (sonst).
//   Nach einem Leseversuch endet der Prozess hart (siehe ausgeben); dann gilt nur "ok" im JSON, nicht der Exit-Code.
//
// Einheit: occt-import-js rechnet mit linearUnit "millimeter" jede Datei (STEP: SI-Einheit bzw. Umrechnungseinheit
// der Datei; IGES: Einheitenschalter im Global-Abschnitt) auf mm um. Geprueft 18.10.2026 mit
// tests/daten/step/quader_200x100x50_meter.stp (Datei in m -> 200 mm).

import { readFileSync, existsSync } from 'node:fs';
import { createRequire } from 'node:module';
import { join, resolve, extname } from 'node:path';
import { homedir } from 'node:os';

const FORMAT = 1;
// Vernetzung: Sehnenfehler 0.1 % der mittleren Huellgroesse, Winkel 0.5 rad. Die Ecken liegen immer auf der exakten
// Flaeche; die Einpassung (Ebene, Zylinder) im Kern ist deshalb unabhaengig von der Feinheit.
const VERNETZUNG = {
    linearUnit: 'millimeter',
    linearDeflectionType: 'bounding_box_ratio',
    linearDeflection: 0.001,
    angularDeflection: 0.5,
};

const meldungen = [];
let gelesen = false;

function ausgeben(obj, code) {
    obj.format = FORMAT;
    obj.meldungen = meldungen;
    process.exitCode = code;
    process.stdout.write(JSON.stringify(obj), () => {
        // Nach dem Lesen wartet V8 beim geordneten Beenden 5-7 s auf Hintergrund-Uebersetzungen (TurboFan) des
        // WebAssembly-Codes (gemessen 18.10.2026, Node 24). Darum nach vollstaendiger Ausgabe hart beenden.
        // Massgebend ist dann "ok" im JSON; der Exit-Code ist unter Windows 1.
        if (gelesen) process.kill(process.pid, 'SIGKILL');
    });
}

function fehler(code, text, vorschlag) {
    ausgeben({ ok: false, fehler: text, vorschlag: vorschlag ?? null }, code);
}

function argumente(argv) {
    const a = {};
    for (let i = 0; i < argv.length; i++) {
        const k = argv[i];
        if (k === '--pruefen') a.pruefen = true;
        else if (k.startsWith('--') && i + 1 < argv.length) a[k.slice(2)] = argv[++i];
        else a.unbekannt = k;
    }
    return a;
}

function runden(arr, faktor) {
    const aus = new Array(arr.length);
    for (let i = 0; i < arr.length; i++) aus[i] = Math.round(arr[i] * faktor) / faktor;
    return aus;
}

function baum(knoten) {
    if (!knoten) return null;
    return { name: knoten.name ?? '', netze: knoten.meshes ?? [], kinder: (knoten.children ?? []).map(baum) };
}

async function main() {
    const a = argumente(process.argv.slice(2));
    if (a.unbekannt) return fehler(4, `Unbekanntes Argument: ${a.unbekannt}`, 'node step_lesen.mjs --datei <pfad> [--art step|iges] [--paket <ordner>]');

    const paket = resolve(a.paket ?? process.env.HLKS_STEP_PAKET ?? join(homedir(), 'HLKS_CAD_build', 'step'));
    const modul = join(paket, 'node_modules', 'occt-import-js');
    if (!existsSync(join(modul, 'dist', 'occt-import-js.wasm')) || !existsSync(join(modul, 'package.json')))
        return fehler(3, `occt-import-js fehlt im Ordner ${paket}.`, 'werkzeuge/step/einrichten.ps1 ausfuehren (installiert occt-import-js 0.0.23 mit npm ci).');
    const version = JSON.parse(readFileSync(join(modul, 'package.json'), 'utf8')).version;

    const require = createRequire(join(paket, 'package.json'));
    const fabrik = require('occt-import-js');
    // Emscripten schreibt Meldungen der OpenCascade-Leser (z. B. "Total number of loaded entities") sonst auf stdout.
    const occt = await fabrik({
        print: (t) => meldungen.push(String(t)),
        printErr: (t) => meldungen.push(String(t)),
    });

    if (a.pruefen) return ausgeben({ ok: true, occt: version, paket }, 0);

    if (!a.datei) return fehler(4, 'Argument --datei fehlt.', 'node step_lesen.mjs --datei <pfad> [--art step|iges]');
    const datei = resolve(a.datei);
    if (!existsSync(datei)) return fehler(2, `Datei nicht gefunden: ${datei}`, 'Pfad pruefen.');
    let art = (a.art ?? '').toLowerCase();
    if (!art) {
        const e = extname(datei).toLowerCase();
        art = e === '.igs' || e === '.iges' ? 'iges' : 'step';
    }
    if (art !== 'step' && art !== 'iges') return fehler(4, `Unbekannte Art "${art}".`, 'Art step oder iges angeben.');

    const inhalt = readFileSync(datei);
    let r;
    gelesen = true;
    try {
        r = art === 'iges' ? occt.ReadIgesFile(inhalt, VERNETZUNG) : occt.ReadStepFile(inhalt, VERNETZUNG);
    } catch (e) {
        return fehler(2, `OpenCascade konnte die ${art.toUpperCase()}-Datei nicht lesen: ${e?.message ?? e}`, 'Datei im Herstellerprogramm neu exportieren (STEP AP214/AP242 bzw. IGES 5.3).');
    }
    if (!r || !r.success)
        return fehler(2, `Die ${art.toUpperCase()}-Datei ist ungueltig oder beschaedigt (OpenCascade: kein Ergebnis).`, 'Datei im Herstellerprogramm neu exportieren (STEP AP214/AP242 bzw. IGES 5.3).');

    const netze = (r.meshes ?? []).map((m) => ({
        name: m.name ?? '',
        farbe: m.color ?? null,
        ecken: runden(m.attributes.position.array, 1e6),
        normalen: m.attributes.normal ? runden(m.attributes.normal.array, 1e7) : null,
        dreiecke: Array.from(m.index.array),
        flaechen: (m.brep_faces ?? []).map((f) => ({ erstes: f.first, letztes: f.last, farbe: f.color ?? null })),
    }));
    ausgeben({ ok: true, art, einheit: 'mm', occt: version, vernetzung: VERNETZUNG, baum: baum(r.root), netze }, 0);
}

main().catch((e) => fehler(5, `Unerwarteter Fehler: ${e?.stack ?? e}`, null));
