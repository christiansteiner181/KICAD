// Modul XR-C: Messwerkzeug mit Fang im Tablet («Messen»). Zwei Punkte mit dem Zeigefinger bzw. dem Strahl; die Punkte fangen (Anschluss, Ecke, Mitte, Kante, Leitungsachse,
// Fläche) mit sichtbarem Fangsymbol und Namen. Angezeigt werden Abstand und dx/dy/dz; «Lot zur Fläche» misst lotrecht zu einer Decke, Wand oder einem Boden. Messungen
// bleiben stehen, «Messungen löschen» entfernt sie. Fang und Abstand rechnet xr-mess-logik.js (dieselbe Logik wie die Masse zu Decke und Wand beim Greifen, XR-B).
// Darstellung im Hologramm-Stil (xr-holo.js). Nichts davon ändert das Modell.

import * as THREE from 'three';
import { fangenPunkt, fangenStrahl, masse, massText, massKurz, lotMass, mitteVon3, plus, minus, mal, skalar, FANGNAMEN_XR } from './xr-mess-logik.js';
import { etikett, fangSymbol, holoLinie } from './xr-holo.js';

const vec = (p) => new THREE.Vector3(p.x, p.y, p.z);

export const MESS_TEXT = Object.freeze({
  ersterPunkt: 'Ersten Punkt setzen',
  zweiterPunkt: 'Zweiten Punkt setzen',
  lotErster: 'Lot: Punkt oder Fläche setzen',
  lotBraucht: 'Lot braucht eine Fläche (Decke, Wand, Boden)',
});

/**
 * o: { szene, daten (Fangdaten), halter?: THREE.Object3D (Standard: szene), ausser?: () => Set von Ids }
 * Rückgabe { an, lot, anSetzen(b), lotSetzen(b), sonde(probe), setzen(), loeschen(), messungen(), letzte(), hinweis(), zustand(), freigeben() }
 */
export function erzeugeMessen(o) {
  const halter = new THREE.Group();
  halter.name = 'vr-messen';
  (o.halter ?? o.szene).add(halter);
  let an = false, lot = false;
  let wartend = null;            // erster Punkt { p, art, flaeche }
  let sondeErg = null;           // letztes Fangergebnis
  const liste = [];              // fertige Messungen { typ, a, b, fuss?, m, linie, etikett }
  let hinweisText = '';
  const symbole = new Map();
  const symbolFuer = (art) => {
    if (!symbole.has(art)) { const s = fangSymbol(art); s.sprite.visible = false; halter.add(s.sprite); symbole.set(art, s); }
    return symbole.get(art);
  };
  const name = etikett('', { breite: 0.16 }); name.sprite.visible = false; halter.add(name.sprite);
  const vorschau = holoLinie(); vorschau.linie.visible = false; halter.add(vorschau.linie);
  const wartePunkt = fangSymbol('anschluss'); wartePunkt.sprite.visible = false; wartePunkt.sprite.scale.set(0.03, 0.03, 1); halter.add(wartePunkt.sprite);

  function verstecken() { for (const s of symbole.values()) s.sprite.visible = false; name.sprite.visible = false; vorschau.linie.visible = false; }

  function hinweisSetzen() { hinweisText = !an ? '' : wartend ? (lot ? 'Lot: zweiten Punkt setzen' : MESS_TEXT.zweiterPunkt) : (lot ? MESS_TEXT.lotErster : MESS_TEXT.ersterPunkt); }

  function fertig(a, b) {
    let typ = 'abstand', fuss = null, m = masse(a.p, b.p), text = '';
    if (lot) {
      const f = b.flaeche ? { f: b.flaeche, von: a.p, zu: b.p } : a.flaeche ? { f: a.flaeche, von: b.p, zu: a.p } : null;
      if (!f) { hinweisText = MESS_TEXT.lotBraucht; return false; }
      typ = 'lot';
      const l = lotMass(f.f, f.von);
      fuss = l.fuss;
      m = { ...masse(f.von, fuss), abstandMm: l.abstandMm };
      a = { ...a, p: f.von }; b = { ...b, p: fuss };
      text = `Lot ${massText(m.abstandMm)}`;
    } else text = `${massText(m.abstandMm)}`;
    const linie = holoLinie(); linie.setzen(a.p, b.p); halter.add(linie.linie);
    const e = etikett(text, { breite: 0.22 }); e.sprite.position.copy(vec(mitteVon3(a.p, b.p))).y += 0.04; halter.add(e.sprite);
    const punkte = [a.p, b.p].map((p) => { const s = fangSymbol('anschluss'); s.sprite.scale.set(0.02, 0.02, 1); s.sprite.position.copy(vec(p)); halter.add(s.sprite); return s; });
    liste.push({ typ, a: a.p, b: b.p, fuss, m, linie, etikett: e, punkte });
    return true;
  }

  return {
    get an() { return an; },
    get lot() { return lot; },
    anSetzen(b) {
      an = !!b;
      if (!an) { wartend = null; wartePunkt.sprite.visible = false; verstecken(); }
      hinweisSetzen();
    },
    lotSetzen(b) { lot = !!b; wartend = null; wartePunkt.sprite.visible = false; hinweisSetzen(); },
    /** probe: { art: 'punkt', p } (Zeigefinger) | { art: 'strahl', u, r } (Controller, Hand-Strahl) | null; liefert das Fangergebnis und zeigt Fangsymbol, Namen und die Vorschaulinie. */
    sonde(probe) {
      if (!an || !probe) { sondeErg = null; verstecken(); return null; }
      const opt = { ausser: o.ausser?.() };
      sondeErg = probe.art === 'strahl' ? fangenStrahl(o.daten, probe.u, probe.r, opt) : fangenPunkt(o.daten, probe.p, opt);
      verstecken();
      if (!sondeErg.p) return sondeErg;
      const s = symbolFuer(sondeErg.art);
      s.sprite.position.copy(vec(sondeErg.p)); s.sprite.visible = true;
      name.setzen(sondeErg.art === 'frei' ? 'frei' : FANGNAMEN_XR[sondeErg.art]);
      name.sprite.position.copy(vec(sondeErg.p)).y += 0.05; name.sprite.visible = true;
      if (wartend) { vorschau.setzen(wartend.p, sondeErg.p); vorschau.linie.visible = true; }
      return sondeErg;
    },
    /** Setzt den Punkt an der Sonde (Pinch bzw. Abzug). Rückgabe: true, wenn damit eine Messung fertig wurde. */
    setzen() {
      if (!an || !sondeErg?.p) return false;
      const pkt = { p: sondeErg.p, art: sondeErg.art, flaeche: sondeErg.flaeche ?? null };
      if (!wartend) {
        wartend = pkt; wartePunkt.sprite.position.copy(vec(pkt.p)); wartePunkt.sprite.visible = true; hinweisSetzen();
        return false;
      }
      const a = wartend;
      const ok = fertig(a, pkt);
      if (ok) { wartend = null; wartePunkt.sprite.visible = false; hinweisSetzen(); }
      return ok;
    },
    loeschen() {
      for (const m of liste) { m.linie.linie.removeFromParent(); m.linie.freigeben(); m.etikett.sprite.removeFromParent(); m.etikett.freigeben(); for (const p of m.punkte) { p.sprite.removeFromParent(); p.freigeben(); } }
      liste.length = 0; wartend = null; wartePunkt.sprite.visible = false; hinweisSetzen();
    },
    messungen: () => liste.map((m) => ({ typ: m.typ, a: m.a, b: m.b, fuss: m.fuss, abstandMm: m.m.abstandMm, dx: m.m.dx, dy: m.m.dy, dz: m.m.dz })),
    /** Zeilen für das Tablet: letzte Messung (Abstand, dx/dy/dz) bzw. die Anweisung. */
    letzte() {
      const m = liste[liste.length - 1];
      if (!m) return [];
      return [`${m.typ === 'lot' ? 'Lot' : 'Abstand'} ${massText(m.m.abstandMm)}`, `dx ${massKurz(m.m.dx)} · dy ${massKurz(m.m.dy)} · dz ${massKurz(m.m.dz)}`];
    },
    hinweis: () => hinweisText,
    zustand: () => ({ an, lot, wartend: wartend ? { ...wartend.p } : null, sonde: sondeErg ? { art: sondeErg.art, id: sondeErg.id, p: sondeErg.p } : null, anzahl: liste.length, hinweis: hinweisText }),
    freigeben() {
      this.loeschen();
      for (const s of symbole.values()) s.freigeben();
      name.freigeben(); vorschau.freigeben(); wartePunkt.freigeben();
      halter.removeFromParent();
    },
  };
}
