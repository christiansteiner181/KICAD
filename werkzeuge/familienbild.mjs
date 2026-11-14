// Bildtafel einer Familie: je Typ die Ansichten vorne, oben, links, iso als SVG (Renderer der Oberfläche: plangrafik.js primitivSvg).
// Christian 14.11.2026: «familien bitte immer mit bild». Braucht einen laufenden KICAD-Server (Vorgabe Desktop 5130, HLKS_PORT ändert ihn) mit der Familie im offenen Projekt.
// Aufruf: node werkzeuge/familienbild.mjs <familie> <ausgabe.svg> [typ1|typ2|…]
import { writeFileSync } from 'node:fs';
import { pathToFileURL, fileURLToPath } from 'node:url';
import { join, dirname } from 'node:path';
const { primitivSvg } = await import(pathToFileURL(join(dirname(fileURLToPath(import.meta.url)), '..', 'src', 'Hlks.Web', 'wwwroot', 'js', 'ansichten', 'plangrafik.js')).href);
const [familie, ausgabe, typenArg] = process.argv.slice(2);
const api = async (name, body) => (await fetch(`http://localhost:${process.env.HLKS_PORT ?? 5130}/api/abfrage/${name}`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) })).json();
const def = await api('familie', { familie });
const typen = typenArg ? typenArg.split('|') : (def.typen ?? def.definition?.typen ?? []).map((t) => t.name ?? t);
const ansichten = ['vorne', 'links', 'oben', 'iso'];
const zelle = 260, kopf = 34, rand = 14;
let inhalt = '';
for (const [zi, typ] of typen.entries()) {
  inhalt += `<text x="${rand}" y="${kopf + zi * (zelle + kopf) - 10}" font-size="15" font-family="Arial" font-weight="bold" fill="#222">${typ}</text>`;
  for (const [si, ansicht] of ansichten.entries()) {
    const r = await api('familie_ansicht', { familie, typ, ansicht, detailgrad: 'fein' });
    const pg = r.plangrafik; if (!pg) continue;
    const [x0, y0, x1, y1] = pg.begrenzung;
    const ctx = { massstab: pg.massstab ?? 10, schraffuren: new Map(), muster: new Map() };
    const svg = (pg.teile ?? []).flatMap((t) => t.primitive ?? []).map((p) => primitivSvg(p, ctx)).join('');
    const w = x1 - x0, h = y1 - y0, f = (zelle - 30) / Math.max(w, h, 1);
    const ox = rand + si * (zelle + rand), oy = kopf + zi * (zelle + kopf);
    inhalt += `<g transform="translate(${ox},${oy})"><rect width="${zelle}" height="${zelle}" fill="#fff" stroke="#ccc"/>`
      + `<text x="6" y="16" font-size="12" font-family="Arial" fill="#666">${ansicht}</text>`
      + `<g transform="translate(${15 + (zelle - 30 - w * f) / 2},${15 + (zelle - 30 - h * f) / 2}) scale(${f}) translate(${-x0},${y1}) scale(1,-1)">${svg}</g></g>`;
  }
}
const breite = rand + ansichten.length * (zelle + rand), hoehe = kopf + typen.length * (zelle + kopf);
const css = '<style>*{vector-effect:non-scaling-stroke} line,polyline,path,polygon,circle,rect.p{stroke:#222;fill:none;stroke-width:1}</style>';
writeFileSync(ausgabe, `<svg xmlns="http://www.w3.org/2000/svg" width="${breite}" height="${hoehe}" viewBox="0 0 ${breite} ${hoehe}"><rect width="100%" height="100%" fill="#f4f4f4"/>${css}${inhalt}</svg>`);
console.log(`${typen.length} Typ(en) → ${ausgabe}`);
