// Tests des Netzdatei-Lesers (netzdatei.js) für Fassung 2 (Modul 3b, B1b: Rolle bauteil | teil | oeffnung, Bezug, Füllungen)
// und die Rückwärtsverträglichkeit mit Fassung 1 – ohne Browser und ohne three.js.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/netzdatei.test.mjs   (oder node --test im Ordner ansichten)

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { readFileSync, existsSync } from 'node:fs';

const netzd = await import('./netzdatei.js');
const hier = path.dirname(fileURLToPath(import.meta.url));
const kernQuelle = (rel) => { const p = path.resolve(hier, '../../../../Hlks.Kern', rel); return existsSync(p) ? readFileSync(p, 'utf8') : null; };

const ROLLE = { bauteil: 0, teil: 1, oeffnung: 2 };

/** Netzdatei wie der Kern (IfcNetzErzeuger). produkte: { label, gid, ent, kote, uebernommen, rolle?, bezug?, fuellungen?, punkte, dreiecke }.
 * Fassung 1 schreibt Rolle, Bezug und Füllungen nicht. */
function baueNetz(produkte, { fassung = 2 } = {}) {
  const teile = [];
  const puffer = (n, fn) => { const b = new DataView(new ArrayBuffer(n)); fn(b); teile.push(new Uint8Array(b.buffer)); };
  const text = (t) => new Uint8Array([...t].map((c) => c.charCodeAt(0)));
  const gid = (t) => text((t ?? '').padEnd(22, ' ').slice(0, 22));
  const beschr = new TextEncoder().encode('Fassung ' + fassung);
  teile.push(text('HLKSNETZ'));
  puffer(8, (b) => { b.setInt32(0, fassung, true); b.setInt32(4, beschr.length, true); });
  teile.push(beschr);
  puffer(4 + 48, (b) => { b.setInt32(0, produkte.length, true); [0, 0, 0, 10000, 10000, 3000].forEach((v, i) => b.setFloat64(4 + 8 * i, v, true)); });
  for (const p of produkte) {
    puffer(4, (b) => b.setInt32(0, p.label, true));
    teile.push(gid(p.gid));
    teile.push(new Uint8Array([p.ent.length]), text(p.ent));
    puffer(9, (b) => { b.setFloat64(0, p.kote ?? NaN, true); b.setUint8(8, p.uebernommen ? 1 : 0); });
    if (fassung >= 2) {
      teile.push(new Uint8Array([ROLLE[p.rolle ?? 'bauteil']]), gid(p.bezug));
      const f = p.fuellungen ?? [];
      teile.push(new Uint8Array([f.length]));
      for (const x of f) teile.push(gid(x));
    }
    if (fassung >= 3) {   // Nachtrag B3c: Materialnamen, Typname, Fundstelle (je uint16 Länge + UTF-8)
      const t16 = (t) => { const b = new TextEncoder().encode(t ?? ''); puffer(2, (d) => d.setUint16(0, b.length, true)); teile.push(b); };
      const m = p.materialien ?? [];
      teile.push(new Uint8Array([m.length]));
      for (const x of m) t16(x);
      t16(p.typname); t16(p.materialquelle);
    }
    puffer(24 + 8, (b) => {
      for (let i = 0; i < 6; i++) b.setFloat32(4 * i, i, true);
      b.setInt32(24, p.punkte.length, true); b.setInt32(28, p.dreiecke.length, true);
    });
    puffer(12 * p.punkte.length, (b) => p.punkte.forEach((q, i) => q.forEach((v, j) => b.setFloat32(12 * i + 4 * j, v, true))));
    puffer(12 * p.dreiecke.length, (b) => p.dreiecke.forEach((q, i) => q.forEach((v, j) => b.setInt32(12 * i + 4 * j, v, true))));
  }
  const gesamt = new Uint8Array(teile.reduce((s, t) => s + t.length, 0));
  let o = 0; for (const t of teile) { gesamt.set(t, o); o += t.length; }
  return gesamt.buffer;
}

const dreieck = (x) => ({ punkte: [[x, 0, 0], [x + 1000, 0, 0], [x, 1000, 0]], dreiecke: [[0, 1, 2]] });
const PRODUKTE = [
  { label: 1, gid: 'WAND-MIT-KOERPER', ent: 'IfcWall', kote: 0, uebernommen: false, ...dreieck(0) },
  { label: 2, gid: 'SCHICHT-BETON', ent: 'IfcBuildingElementPart', kote: 0, uebernommen: false, rolle: 'teil', bezug: 'WAND-MIT-KOERPER', ...dreieck(2000) },
  { label: 3, gid: 'OEFFNUNG-LEER', ent: 'IfcOpeningElement', kote: 0, uebernommen: false, rolle: 'oeffnung', bezug: 'WAND-MIT-KOERPER', ...dreieck(4000) },
  { label: 4, gid: 'OEFFNUNG-FENSTER', ent: 'IfcOpeningElement', kote: 0, uebernommen: false, rolle: 'oeffnung', bezug: 'WAND-MIT-KOERPER', fuellungen: ['FENSTER-1'], ...dreieck(6000) },
  { label: 5, gid: 'DECKE-UEBERNOMMEN', ent: 'IfcSlab', kote: 3000, uebernommen: true, ...dreieck(8000) },
];

test('Netzdatei Fassung 2: Rolle, Bezug und Füllungen gelesen; Öffnungen nie im Netz, Teile wie Fassung 1 nach Kennzeichen', () => {
  const n = netzd.leseNetz(baueNetz(PRODUKTE));
  assert.equal(n.fassung, 2);
  assert.equal(n.produkte.length, 5);
  assert.deepEqual(n.produkte.map((p) => [p.globalId.trim(), p.rolle, p.bezug, p.fuellungen]), [
    ['WAND-MIT-KOERPER', 'bauteil', null, []],
    ['SCHICHT-BETON', 'teil', 'WAND-MIT-KOERPER', []],
    ['OEFFNUNG-LEER', 'oeffnung', 'WAND-MIT-KOERPER', []],
    ['OEFFNUNG-FENSTER', 'oeffnung', 'WAND-MIT-KOERPER', ['FENSTER-1']],
    ['DECKE-UEBERNOMMEN', 'bauteil', null, []],
  ]);
  assert.equal(n.produkte[4].kote, 3000);
  assert.equal(n.produkte[4].punkte, 3);
  // Hintergrund: Wand + Schicht (Kennzeichen 0), ohne die zwei Öffnungen (auch Kennzeichen 0) und ohne die übernommene Decke
  assert.equal(n.dreiecke, 2);
  assert.equal(n.punkte, 6);
  assert.deepEqual([...n.positionen.slice(3, 6)], [1, 0, -0]);          // zweiter Punkt der Wand (1000, 0, 0) → (1, 0, −0)
  assert.deepEqual([...n.positionen.slice(9, 12)], [2, 0, -0]);          // erster Punkt der Schicht (2000, 0, 0)
  assert.deepEqual([...n.indizes], [0, 1, 2, 3, 4, 5]);
  // alle: auch die übernommene Decke, nie die Öffnungen; nur übernommene: nur die Decke
  assert.equal(netzd.leseNetz(baueNetz(PRODUKTE), { nur: 'alle' }).dreiecke, 3);
  assert.equal(netzd.leseNetz(baueNetz(PRODUKTE), { nur: 'uebernommen' }).dreiecke, 1);
  // eine Datei nur mit Öffnungen zeichnet nichts
  assert.equal(netzd.leseNetz(baueNetz([PRODUKTE[2], PRODUKTE[3]]), { nur: 'alle' }).dreiecke, 0);
});

test('Netzdatei Fassung 1 bleibt lesbar: gleiche Produkte, Rolle bauteil, ohne Bezug; Netz wie zuvor', () => {
  const alt = PRODUKTE.filter((p) => !p.rolle || p.rolle !== 'oeffnung').map(({ rolle, bezug, fuellungen, ...rest }) => rest);
  const n = netzd.leseNetz(baueNetz(alt, { fassung: 1 }));
  assert.equal(n.fassung, 1);
  assert.equal(n.produkte.length, 3);
  assert.ok(n.produkte.every((p) => p.rolle === 'bauteil' && p.bezug === null && p.fuellungen.length === 0));
  assert.equal(n.dreiecke, 2);   // Wand + Schicht (Hintergrund) wie in Fassung 2
  // dieselben Hintergrund-Dreiecke wie die Fassung-2-Datei mit Öffnungen
  const neu = netzd.leseNetz(baueNetz(PRODUKTE));
  assert.deepEqual([...n.positionen], [...neu.positionen]);
  assert.deepEqual([...n.indizes], [...neu.indizes]);
});

test('Netzdatei Fassung 2: Fehler deutsch (unbekannte Rolle, abgeschnitten in Bezug und Füllungen, unbekannte Fassung)', () => {
  const ganz = baueNetz(PRODUKTE);
  // Rolle des ersten Produkts liegt nach Kopf (8 + 4 + 4 + Beschreibung «Fassung 2» 9 + 4 + 48), Label 4, GlobalId 22, n 1 + 7, Kote 8, Kennzeichen 1
  const lage = 8 + 4 + 4 + 9 + 4 + 48 + 4 + 22 + 1 + 'IfcWall'.length + 8 + 1;
  const kaputt = new Uint8Array(ganz.slice(0));
  assert.equal(kaputt[lage], 0);
  kaputt[lage] = 7;
  assert.throws(() => netzd.leseNetz(kaputt.buffer), /unbekannte Rolle 7/);
  for (const kuerzer of [lage, lage + 5, lage + 23, ganz.byteLength - 1])
    assert.throws(() => netzd.leseNetz(ganz.slice(0, kuerzer)), /abgeschnitten/, `bei ${kuerzer} Byte`);
  assert.throws(() => netzd.leseNetz(baueNetz(PRODUKTE, { fassung: 4 })), /Fassung 4 wird nicht unterstützt \(bekannt: 1, 2, 3\)/);
  assert.deepEqual(netzd.FASSUNGEN, [1, 2, 3]);
  assert.deepEqual(netzd.ROLLEN, ['bauteil', 'teil', 'oeffnung']);
});

test('Netzdatei Fassung 3 (Nachtrag B3c): Materialnamen, Typname und Fundstelle je Produkt; Netz wie Fassung 2; Fassung 2 ohne Material', () => {
  // FIKTIVE Namen (UTF-8 mit Umlaut), leere Angaben → null
  const mit = PRODUKTE.map((p, i) => ({
    ...p,
    materialien: i === 0 ? ['Beton Sichtbeton, hellgrau', 'Wärmedämmung'] : [],
    typname: i === 0 ? 'Beton, Stahlbeton 250' : null,
    materialquelle: i === 0 ? 'IfcMaterialList #12' : null,
  }));
  const n3 = netzd.leseNetz(baueNetz(mit, { fassung: 3 }), { nur: 'alle' });
  const n2 = netzd.leseNetz(baueNetz(PRODUKTE, { fassung: 2 }), { nur: 'alle' });
  assert.equal(n3.fassung, 3);
  assert.deepEqual(n3.produkte[0].materialien, ['Beton Sichtbeton, hellgrau', 'Wärmedämmung']);
  assert.equal(n3.produkte[0].typname, 'Beton, Stahlbeton 250');
  assert.equal(n3.produkte[0].materialquelle, 'IfcMaterialList #12');
  assert.deepEqual([n3.produkte[1].materialien, n3.produkte[1].typname, n3.produkte[1].materialquelle], [[], null, null]);
  // Rollen, Bezüge und das Netz lesen sich wie in Fassung 2
  assert.deepEqual(n3.produkte.map((p) => [p.globalId, p.rolle, p.bezug]), n2.produkte.map((p) => [p.globalId, p.rolle, p.bezug]));
  assert.deepEqual(Array.from(n3.positionen), Array.from(n2.positionen));
  assert.deepEqual(Array.from(n3.indizes), Array.from(n2.indizes));
  // Fassung 2: ohne Material
  assert.ok(n2.produkte.every((p) => p.materialien.length === 0 && p.typname === null && p.materialquelle === null));
  // abgeschnitten im Materialteil
  const ganz = baueNetz(mit, { fassung: 3 });
  assert.throws(() => netzd.leseNetz(ganz.slice(0, 8 + 4 + 4 + 9 + 4 + 48 + 4 + 22 + 1 + 'IfcWall'.length + 8 + 1 + 1 + 22 + 1 + 1 + 2 + 5)), /abgeschnitten/);
});

test('Netzdatei: der Leser kennt die Fassung des Kern-Schreibers, die Rollen-Nummern stimmen mit dem Kern überein', () => {
  const cs = kernQuelle('Import/IfcNetz.cs');
  if (!cs) return;
  const f = Number(cs.match(/public const int Fassung = (\d+);/)?.[1]);
  assert.ok(netzd.FASSUNGEN.includes(f), `Kern schreibt Fassung ${f}`);
  assert.match(cs, /LesbareFassungen = \[1, 2, 3\]/);
  assert.match(cs, /uint8 m \+ m\*\(uint16 L \+ L Byte UTF-8\) Materialnamen/);   // Fassung 3 (Nachtrag B3c)
  assert.match(cs, /enum IfcNetzRolle : byte \{ Bauteil = 0, Teil = 1, Oeffnung = 2 \}/);
  assert.match(cs, /uint8 Rolle \(0 = bauteil, 1 = teil, 2 = oeffnung\), 22 Byte Bezug/);
});
