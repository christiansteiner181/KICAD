// Erzeugt die STEP- und IGES-Testdateien dieses Ordners (Modul 2, Auftrag B3). Von Hand geschriebener B-Rep-Aufbau,
// keine Herstellerdaten. Aufruf (nur noetig, wenn sich der Aufbau aendern soll):
//   node tests/daten/step/testdateien_erzeugen.mjs
//
// Dateien:
//   quader_200x100x50.stp         AP214, mm: Quader x 0..200, y 0..100, z 0..50 (6 ebene Flaechen)
//   quader_stutzen.stp            AP214, mm: derselbe Quader mit Rohrstutzen auf der rechten Seitenflaeche (x = 200),
//                                 mittig (y = 50, z = 25), Achse +x, aussen D 21.3, innen D 16.1, 30 mm lang (bis x = 230).
//                                 Flaechen: 6 Quaderflaechen (rechte mit Kreisloch D 21.3), Boden der Bohrung (Kreis
//                                 D 16.1 in x = 200), Aussenzylinder, Innenzylinder, Ringflaeche in x = 230.
//                                 Zylinder ohne Nahtkante (zwei Kreisraender); OpenCascade ergaenzt die Naht beim Lesen.
//   quader_200x100x50_meter.stp   wie oben, aber Einheit SI METRE (Koordinaten 0.2 x 0.1 x 0.05)
//   quader_200x100x50.igs         IGES 5.3, Einheit MM: sechs bilineare B-Spline-Flaechen (Typ 128) als Quaderhuelle

import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ordner = dirname(fileURLToPath(import.meta.url));

// ---------- STEP ----------

function zahl(v) {
    if (Number.isInteger(v)) return `${v}.`;
    const s = String(v);
    return s.includes('e') ? v.toExponential().toUpperCase().replace('E+', 'E') : s;
}

class Step {
    constructor() { this.zeilen = []; this.n = 0; }
    neu(text) { this.n++; this.zeilen.push(`#${this.n} = ${text};`); return `#${this.n}`; }
    punkt(p) { return this.neu(`CARTESIAN_POINT('',(${p.map(zahl).join(',')}))`); }
    richtung(d) { return this.neu(`DIRECTION('',(${d.map(zahl).join(',')}))`); }
    achse(o, z, x) { return this.neu(`AXIS2_PLACEMENT_3D('',${this.punkt(o)},${this.richtung(z)},${this.richtung(x)})`); }
    ecke(p) { return this.neu(`VERTEX_POINT('',${this.punkt(p)})`); }
}

// Quader [0,X]x[0,Y]x[0,Z]; loch = optionaler Kreisrand (Kante) in der rechten Flaeche.
function quaderFlaechen(s, X, Y, Z, rechtsLoch) {
    const ecken = new Map();
    const e = (p) => { const k = p.join(','); if (!ecken.has(k)) ecken.set(k, { id: s.ecke(p), p }); return ecken.get(k); };
    const kanten = new Map();
    const kante = (a, b) => {
        const k1 = a.p.join(',') + '|' + b.p.join(','), k2 = b.p.join(',') + '|' + a.p.join(',');
        if (kanten.has(k1)) return `ORIENTED_EDGE('',*,*,${kanten.get(k1)},.T.)`;
        if (kanten.has(k2)) return `ORIENTED_EDGE('',*,*,${kanten.get(k2)},.F.)`;
        const d = b.p.map((v, i) => v - a.p[i]);
        const l = Math.hypot(...d);
        const linie = s.neu(`LINE('',${s.punkt(a.p)},${s.neu(`VECTOR('',${s.richtung(d.map(v => v / l))},${zahl(l)})`)})`);
        const id = s.neu(`EDGE_CURVE('',${a.id},${b.id},${linie},.T.)`);
        kanten.set(k1, id);
        return `ORIENTED_EDGE('',*,*,${id},.T.)`;
    };
    // Ecken je Flaeche gegen den Uhrzeigersinn, von aussen gesehen; Normale nach aussen.
    const def = [
        { n: [0, 0, -1], x: [1, 0, 0], p: [[0, 0, 0], [0, Y, 0], [X, Y, 0], [X, 0, 0]] },
        { n: [0, 0, 1], x: [1, 0, 0], p: [[0, 0, Z], [X, 0, Z], [X, Y, Z], [0, Y, Z]] },
        { n: [0, -1, 0], x: [1, 0, 0], p: [[0, 0, 0], [X, 0, 0], [X, 0, Z], [0, 0, Z]] },
        { n: [0, 1, 0], x: [1, 0, 0], p: [[0, Y, 0], [0, Y, Z], [X, Y, Z], [X, Y, 0]] },
        { n: [-1, 0, 0], x: [0, 1, 0], p: [[0, 0, 0], [0, 0, Z], [0, Y, Z], [0, Y, 0]] },
        { n: [1, 0, 0], x: [0, 1, 0], p: [[X, 0, 0], [X, Y, 0], [X, Y, Z], [X, 0, Z]], rechts: true },
    ];
    const flaechen = [];
    for (const f of def) {
        const es = f.p.map(e);
        const schleife = s.neu(`EDGE_LOOP('',(${es.map((a, i) => s.neu(kante(a, es[(i + 1) % 4]))).join(',')}))`);
        const grenzen = [s.neu(`FACE_OUTER_BOUND('',${schleife},.T.)`)];
        if (f.rechts && rechtsLoch) {
            const innen = s.neu(`EDGE_LOOP('',(${s.neu(`ORIENTED_EDGE('',*,*,${rechtsLoch},.F.)`)}))`);
            grenzen.push(s.neu(`FACE_BOUND('',${innen},.T.)`));
        }
        const ebene = s.neu(`PLANE('',${s.achse(f.p[0], f.n, f.x)})`);
        flaechen.push(s.neu(`ADVANCED_FACE('',(${grenzen.join(',')}),${ebene},.T.)`));
    }
    return flaechen;
}

function kreisKante(s, mitte, achse, xRef, r) {
    const v = s.ecke([mitte[0] + r * xRef[0], mitte[1] + r * xRef[1], mitte[2] + r * xRef[2]]);
    const kreis = s.neu(`CIRCLE('',${s.achse(mitte, achse, xRef)},${zahl(r)})`);
    return s.neu(`EDGE_CURVE('',${v},${v},${kreis},.T.)`);
}

function stepDatei(name, beschreibung, einheit, baue) {
    const s = new Step();
    const ctx0 = s.neu(`APPLICATION_CONTEXT('core data for automotive mechanical design processes')`);
    s.neu(`APPLICATION_PROTOCOL_DEFINITION('international standard','automotive_design',2000,${ctx0})`);
    const pc = s.neu(`PRODUCT_CONTEXT('',${ctx0},'mechanical')`);
    const prod = s.neu(`PRODUCT('${name}','${name}','',(${pc}))`);
    s.neu(`PRODUCT_RELATED_PRODUCT_CATEGORY('part',$,(${prod}))`);
    const pdf = s.neu(`PRODUCT_DEFINITION_FORMATION('','',${prod})`);
    const pdc = s.neu(`PRODUCT_DEFINITION_CONTEXT('part definition',${ctx0},'design')`);
    const pd = s.neu(`PRODUCT_DEFINITION('design','',${pdf},${pdc})`);
    const pds = s.neu(`PRODUCT_DEFINITION_SHAPE('','',${pd})`);
    const laenge = s.neu(einheit === 'm'
        ? `( LENGTH_UNIT() NAMED_UNIT(*) SI_UNIT($,.METRE.) )`
        : `( LENGTH_UNIT() NAMED_UNIT(*) SI_UNIT(.MILLI.,.METRE.) )`);
    const winkel = s.neu(`( NAMED_UNIT(*) PLANE_ANGLE_UNIT() SI_UNIT($,.RADIAN.) )`);
    const raumw = s.neu(`( NAMED_UNIT(*) SI_UNIT($,.STERADIAN.) SOLID_ANGLE_UNIT() )`);
    const unsicher = s.neu(`UNCERTAINTY_MEASURE_WITH_UNIT(LENGTH_MEASURE(${einheit === 'm' ? '1.E-09' : '1.E-06'}),${laenge},'distance_accuracy_value','confusion accuracy')`);
    const ctx = s.neu(`( GEOMETRIC_REPRESENTATION_CONTEXT(3) GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT((${unsicher})) GLOBAL_UNIT_ASSIGNED_CONTEXT((${laenge},${winkel},${raumw})) REPRESENTATION_CONTEXT('Context #1','3D Context with UNIT and UNCERTAINTY') )`);
    const flaechen = baue(s);
    const schale = s.neu(`CLOSED_SHELL('',(${flaechen.join(',')}))`);
    const koerper = s.neu(`MANIFOLD_SOLID_BREP('${name}',${schale})`);
    const lage = s.achse([0, 0, 0], [0, 0, 1], [1, 0, 0]);
    const darst = s.neu(`ADVANCED_BREP_SHAPE_REPRESENTATION('',(${lage},${koerper}),${ctx})`);
    s.neu(`SHAPE_DEFINITION_REPRESENTATION(${pds},${darst})`);
    const kopf = [
        'ISO-10303-21;',
        'HEADER;',
        `FILE_DESCRIPTION(('${beschreibung}'),'2;1');`,
        `FILE_NAME('${name}','2026-10-18T12:00:00',('HLKS-CAD'),('H+K Planungs AG'),'testdateien_erzeugen.mjs','HLKS-CAD Testdaten','');`,
        `FILE_SCHEMA(('AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }'));`,
        'ENDSEC;',
        'DATA;',
    ];
    return [...kopf, ...s.zeilen, 'ENDSEC;', 'END-ISO-10303-21;', ''].join('\n');
}

function quader(f) {
    return (s) => quaderFlaechen(s, 200 * f, 100 * f, 50 * f, null);
}

// Rohrstutzen auf x = 200: Achse +x durch (y 50, z 25), aussen r 10.65, innen r 8.05, bis x = 230.
function quaderMitStutzen(s) {
    const ra = 21.3 / 2, ri = 16.1 / 2;
    const ax = [1, 0, 0], xr = [0, 0, 1];
    const m0 = [200, 50, 25], m1 = [230, 50, 25];
    const ka0 = kreisKante(s, m0, ax, xr, ra), ki0 = kreisKante(s, m0, ax, xr, ri);
    const ka1 = kreisKante(s, m1, ax, xr, ra), ki1 = kreisKante(s, m1, ax, xr, ri);
    const fl = quaderFlaechen(s, 200, 100, 50, ka0);
    const oe = (k, o) => s.neu(`ORIENTED_EDGE('',*,*,${k},${o ? '.T.' : '.F.'})`);
    const grenze = (k, o, aussen) => s.neu(`${aussen ? 'FACE_OUTER_BOUND' : 'FACE_BOUND'}('',${s.neu(`EDGE_LOOP('',(${oe(k, o)}))`)},.T.)`);
    // Boden der Bohrung in x = 200, Normale +x (in die Bohrung)
    const ebene0 = s.neu(`PLANE('',${s.achse(m0, ax, xr)})`);
    fl.push(s.neu(`ADVANCED_FACE('',(${grenze(ki0, true, true)}),${ebene0},.T.)`));
    // Aussenzylinder (Normale nach aussen)
    const zylA = s.neu(`CYLINDRICAL_SURFACE('',${s.achse(m0, ax, xr)},${zahl(ra)})`);
    fl.push(s.neu(`ADVANCED_FACE('',(${grenze(ka0, true, true)},${grenze(ka1, false, false)}),${zylA},.T.)`));
    // Innenzylinder (Normale zur Achse hin)
    const zylI = s.neu(`CYLINDRICAL_SURFACE('',${s.achse(m0, ax, xr)},${zahl(ri)})`);
    fl.push(s.neu(`ADVANCED_FACE('',(${grenze(ki0, false, true)},${grenze(ki1, true, false)}),${zylI},.F.)`));
    // Ringflaeche in x = 230, Normale +x
    const ebene1 = s.neu(`PLANE('',${s.achse(m1, ax, xr)})`);
    fl.push(s.neu(`ADVANCED_FACE('',(${grenze(ka1, true, true)},${grenze(ki1, false, false)}),${ebene1},.T.)`));
    return fl;
}

// ---------- IGES ----------

function igesDatei(dateiname) {
    const X = 200, Y = 100, Z = 50;
    const seiten = [
        [[0, 0, 0], [0, Y, 0], [X, Y, 0], [X, 0, 0]],
        [[0, 0, Z], [X, 0, Z], [X, Y, Z], [0, Y, Z]],
        [[0, 0, 0], [X, 0, 0], [X, 0, Z], [0, 0, Z]],
        [[0, Y, 0], [0, Y, Z], [X, Y, Z], [X, Y, 0]],
        [[0, 0, 0], [0, 0, Z], [0, Y, Z], [0, Y, 0]],
        [[X, 0, 0], [X, Y, 0], [X, Y, Z], [X, 0, Z]],
    ];
    const z = (v) => (Number.isInteger(v) ? `${v}.` : String(v));
    const zeile = (text, abschnitt, nr) => text.padEnd(72, ' ') + abschnitt + String(nr).padStart(7, ' ');
    const S = [zeile('HLKS-CAD Testdatei: Quader 200 x 100 x 50 mm, 6 B-Spline-Flaechen (128)', 'S', 1)];
    const h = (t) => `${t.length}H${t}`;
    const jetzt = h('20261018.120000');
    const glob = `1H,,1H;,${h('Quader1')},${h(dateiname)},${h('HLKS-CAD 1')},${h('1.00')},32,38,6,308,15,${h('Quader1')},1.,2,${h('MM')},1,1.,${jetzt},0.001,1000.,${h('HLKS-CAD')},${h('H+K Planungs AG')},11,0,${jetzt};`;
    const G = [];
    for (let i = 0; i < glob.length; i += 72) G.push(zeile(glob.slice(i, i + 72), 'G', G.length + 1));
    const D = [], P = [];
    seiten.forEach((p, k) => {
        const de = 2 * k + 1;
        // Steuerpunkte: P(0,0), P(1,0), P(0,1), P(1,1); u von p0 nach p1, v von p0 nach p3 -> Normale nach aussen
        const cp = [p[0], p[1], p[3], p[2]].flat().map(z);
        const teile = ['128', '1', '1', '1', '1', '0', '0', '1', '0', '0', '0.', '0.', '1.', '1.', '0.', '0.', '1.', '1.',
            '1.', '1.', '1.', '1.', ...cp, '0.', '1.', '0.', '1.'];
        const text = teile.join(',') + ';';
        const pStart = P.length + 1;
        let rest = text;
        while (rest.length > 0) {
            let n = Math.min(64, rest.length);
            if (n < rest.length) n = rest.lastIndexOf(',', n - 1) + 1;
            P.push((rest.slice(0, n).padEnd(64, ' ') + ' ' + String(de).padStart(7, ' ')) + 'P' + String(P.length + 1).padStart(7, ' '));
            rest = rest.slice(n);
        }
        const f = (v) => String(v).padStart(8, ' ');
        D.push(zeile([128, pStart, 0, 0, 0, 0, 0, 0].map(f).join('') + '00000000', 'D', de));
        D.push(zeile([128, 0, 0, P.length - pStart + 1, 0, '', '', `SEITE${k + 1}`, 0].map(f).join(''), 'D', de + 1));
    });
    const T = [zeile(`S${String(S.length).padStart(7)}G${String(G.length).padStart(7)}D${String(D.length).padStart(7)}P${String(P.length).padStart(7)}`, 'T', 1)];
    return [...S, ...G, ...D, ...P, ...T].join('\n') + '\n';
}

const dateien = {
    'quader_200x100x50.stp': stepDatei('Quader', 'HLKS-CAD Testdatei: Quader 200 x 100 x 50 mm', 'mm', quader(1)),
    'quader_stutzen.stp': stepDatei('QuaderStutzen', 'HLKS-CAD Testdatei: Quader 200 x 100 x 50 mm mit Rohrstutzen D 21.3/16.1, 30 mm, auf x = 200', 'mm', quaderMitStutzen),
    'quader_200x100x50_meter.stp': stepDatei('QuaderMeter', 'HLKS-CAD Testdatei: Quader 0.2 x 0.1 x 0.05 m (Einheit METRE)', 'm', quader(0.001)),
    'quader_200x100x50.igs': igesDatei('quader_200x100x50.igs'),
};
for (const [n, inhalt] of Object.entries(dateien)) {
    writeFileSync(join(ordner, n), inhalt, 'ascii');
    console.log(n, inhalt.length, 'Zeichen');
}
