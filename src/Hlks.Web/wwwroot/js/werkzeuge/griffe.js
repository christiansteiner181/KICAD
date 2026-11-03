// Griffe an gewählten Elementen: Ist eine Wand, Achse oder Detaillinie gewählt, erscheinen an ihren Punkten Griffe; Ziehen (mit
// Fangen, Shift = orthogonal, Esc bricht ab) sendet element_aendern mit der neuen Geometrie (Art, Dicke, Höhe bleiben). An einer
// gewählten Tür zeigen kleine Doppelpfeile «Aufschlag umdrehen» (seite) und «Band umdrehen» (band).
//
// Technik: Das ist ein *passives* Werkzeug im Sinn der Schnittstelle Ansicht ↔ Werkzeug – es wird mit `ansicht.setzeWerkzeug`
// gesetzt, solange kein Zeichenwerkzeug läuft, und verbraucht (`true`) nur Ereignisse auf Griffen. Alles andere gibt es mit
// `false` an die Ansicht zurück (Auswahl, Hover, Pan). Strg+Klick merkt es für die Mehrfachauswahl (auswahl.js).
// Annahme: Ecken verbundener Wände werden nicht mitgezogen (es gibt keinen Kernbefehl dafür); jede Wand ändert sich einzeln.
// Leitungen (Rohr, Kanal; Modul 3, C1): Ziehen eines Endes sendet dieselbe Geometrie-Änderung; die Nachführung verbundener Elemente und die
// Hinweise kommen vom Kern (Leitungsenden folgen Geräten und Formteilen, neue Befunde P13–P18 erscheinen als Meldung).
// Bearbeitungswerkzeuge A2: Beim Ziehen eines Leitungsendes werden Anschlüsse in der Nähe gefangen (gehen allem vor). Loslassen auf einem freien
// Anschluss setzt das Ende auf dessen Lage (mit Höhe) und verbindet im selben Paket (element_aendern mit «verbinden»); ein belegter Anschluss
// bricht das Ziehen mit Meldung ab.
// Bearbeitungswerkzeuge A1: Eine gewählte Gerade (Rohr, Kanal mit zwei Punkten) hat in der Mitte einen Griff zum Verschieben (elemente_verschieben,
// Fangen, Umschalt = orthogonal); Pfeiltasten verschieben die Auswahl um den Rasterwert in den Achsen der Ansicht. Formteile und Nachbarleitungen
// führt der Kern nach (Dehnen bzw. Versatz).

import { element, abonnieren, alleElemente } from '../zustand.js';
import { sende, ergebnisMelden } from '../oberflaeche/befehle.js';
import { meldung } from '../oberflaeche/dialoge.js';
import { planAnsicht, werkzeugAktiv, beiWerkzeugEnde, geschossVon } from './basis.js';
import { fangen, abstand, FANGRADIUS_PX } from './fangen.js';
import { griffPunkte, griffGeometrie, griffAnschliessen, tuerUmdrehen, verschiebenParameter, pfeilParameter } from './parameter.js';
import { rasterWert } from './optionen.js';
import { auswahlElemente, strgMerken, shiftMerken, beiAuswahlMenge, zugWaehlen } from './auswahl.js';
import * as S from './schicht.js';
import { neueBefundeTexte, istLeitungsklasse, anschluesseNahe, anschlussFangpunkte, anschlussText, anschlussFrei } from './leitung-logik.js';
import { alleAnschluesse, anschluesseLaden, elementeNahe } from './leitung-daten.js';
import { fangArten, einmalVerbraucht } from './fangwahl.js';
import { tempMasse, massVerschiebung } from './tempmass.js';
import { eingabe } from '../oberflaeche/dialoge.js';

// B5: temporäre Masse des gewählten Elements, neu gerechnet bei jeder Modelländerung
let modellStand = 0;
const masseCache = { id: null, stand: -1, liste: [] };

const ANSCHLUSS_PX = 60;     // Suchradius für Anschlüsse beim Ziehen eines Leitungsendes (wie Leitungswerkzeug)

const GRIFF_PX = 8;          // Kantenlänge der Griffe
const TREFFER_PX = 8;        // Fangradius der Maus auf Griffen

const mitGriffen = new Map();   // Ansicht-Instanz → Griffe

class Griffe {
  constructor(ansicht) {
    this.ansicht = ansicht;
    this.ziehen = null;
    this.gezeichnetBei = 0;
    this.objekt = { id: 'griffe', passiv: true, ereignis: (art, e, a) => this.ereignis(art, e, a), beenden: () => {} };
  }

  s() { const p = this.ansicht.pxJeMm?.(); return p > 0 ? 1 / p : 1; }
  schicht() { return this.ansicht.werkzeugSchicht ?? null; }
  aktiv() { return this.ansicht.werkzeug === this.objekt; }

  /** Gehört das Element zu dieser Ansicht (Geschoss bzw. Ansichtsbindung)? */
  gehoert(el) {
    const a = this.ansicht;
    if (el.schicht === 'beschriftung') {
      const b = el.bezuege?.ansicht;
      return !!b && element(b)?.id === a.ansicht?.id;
    }
    if (a.art !== 'grundriss') return false;
    if (el.klasse === 'Achse') return true;
    const g = el.geschoss;
    return !!g && element(g)?.id === element(geschossVon(a))?.id;
  }

  /** Die Griffe des gewählten Elements: Punktgriffe (ziehen) und Türpfeile (klicken). */
  griffe() {
    const alle = auswahlElemente();
    if (alle.length !== 1) return [];
    const el = alle[0];
    if (!this.gehoert(el)) return [];
    const s = this.s();
    if (['Wand', 'Achse', 'Detaillinie', 'Rohr', 'Kanal'].includes(el.klasse)) {   // Rohr, Kanal: Leitungsenden (Modul 3, C1)
      const pts = griffPunkte(el.geometrie);
      const aus = pts.map((p, i) => ({ art: 'punkt', el, index: i, p }));
      if (istLeitungsklasse(el.klasse) && pts.length === 2) aus.push({ art: 'mitte', el, p: [(pts[0][0] + pts[1][0]) / 2, (pts[0][1] + pts[1][1]) / 2] });   // A1
      for (const m of this.masse(el)) aus.push({ art: 'mass', el, mass: m, p: [(m.von[0] + m.bis[0]) / 2, (m.von[1] + m.bis[1]) / 2] });   // B5
      return aus;
    }
    if (el.klasse === 'Tuer') {
      const wand = element(el.bezuege?.wand);
      const pts = wand?.geometrie?.punkte, m = el.geometrie?.punkte?.[0];
      if (!pts || pts.length < 2 || !m) return [];
      const l = abstand(pts[0], pts[1]);
      if (l < 1e-6) return [];
      const u = [(pts[1][0] - pts[0][0]) / l, (pts[1][1] - pts[0][1]) / l], n = [-u[1], u[0]];
      const dicke = wand.geometrie.dicke ?? 0, breite = el.werte?.breite ?? 800;
      const vorzeichen = (el.werte?.seite ?? 'links') === 'rechts' ? -1 : 1;
      return [
        { art: 'tuer', el, was: 'seite', p: [m[0] + n[0] * vorzeichen * (dicke / 2 + 16 * s), m[1] + n[1] * vorzeichen * (dicke / 2 + 16 * s)], d: n },
        { art: 'tuer', el, was: 'band', p: [m[0] + u[0] * (breite / 2 + 16 * s), m[1] + u[1] * (breite / 2 + 16 * s)], d: u },
      ];
    }
    return [];
  }

  /** B5: temporäre Masse zu den nächsten parallelen Geraden der Ansicht (zwischengespeichert bis zur nächsten Modelländerung). */
  masse(el) {
    if (masseCache.id !== el.id || masseCache.stand !== modellStand) {
      const kand = alleElemente().filter((e) => e.id !== el.id && e.geometrie && this.gehoert(e));
      masseCache.id = el.id; masseCache.stand = modellStand; masseCache.liste = tempMasse(el, kand);
    }
    return masseCache.liste;
  }

  griffAn(maus) {
    const r = TREFFER_PX * this.s() * (1.2);
    let best = null;
    for (const g of this.griffe()) {
      const d = abstand(g.p, maus);
      if (d <= r * (g.art === 'tuer' ? 1.3 : 1) && (!best || d < best.d)) best = { ...g, d };
    }
    return best;
  }

  // ------------------------------------------------------------------------------------------ Zeichnen

  zeichnen() {
    const sch = this.schicht();
    if (!sch || !this.aktiv()) return;
    sch.classList.add('wz-schicht');
    sch.style.pointerEvents = 'none';
    const s = this.s();
    S.drehungSetzen(this.ansicht.drehung ?? 0);   // N9: Masse und Fangsymbole beim Ziehen bleiben in gedrehten Ansichten waagrecht
    this.gezeichnetBei = this.ansicht.pxJeMm?.() ?? 0;
    let h = '';
    try {
      const alle = auswahlElemente();
      if (alle.length > 1) h += S.umrisseSvg(alle, (p) => p, s, 'wz-auswahl');
      if (this.ziehen) h += this.zeichneZiehen(s);
      else for (const g of this.griffe()) {
        if (g.art === 'tuer') h += S.doppelpfeil(g.p, g.d, s);
        else if (g.art === 'mass') h += S.linie(g.mass.von, g.mass.bis, 'wz-mass') + S.text(g.p, `${g.mass.abstand}`, s, { anker: 'middle', versatzPx: [0, -4], klasse: 'wz-text wz-masstext wz-mass-klick' });
        else h += S.griff(g.p, s, g.art === 'mitte' ? 'wz-griff wz-griff-mitte' : 'wz-griff', GRIFF_PX);
      }
    } finally { S.drehungSetzen(0); }   // andere Nutzer der Schicht-Bausteine zeichnen ungedreht
    sch.innerHTML = h;
  }

  zeichneZiehen(s) {
    const z = this.ziehen, g = z.g;
    if (z.mitte) {   // A1: ganze Gerade verschieben
      const dx = z.fang.punkt[0] - z.start[0], dy = z.fang.punkt[1] - z.start[1];
      return S.polylinie(griffPunkte(g.el.geometrie).map((p) => [p[0] + dx, p[1] + dy]), 'wz-linie') + S.tempMass(z.start, z.fang.punkt, s)
        + S.griff(z.fang.punkt, s, 'wz-griff wz-griff-zieht', GRIFF_PX) + S.fangsymbol(z.fang, s);
    }
    const neu = griffGeometrie(g.el.geometrie, g.index, z.fang.punkt);
    const pts = neu.punkte.map((p) => [p[0], p[1]]);
    let h = '';
    if (g.el.klasse === 'Wand' && neu.dicke > 0) h += S.band(pts[0], pts[1], neu.dicke, 0);
    else h += S.polylinie(pts, 'wz-linie');
    if (z.anker) h += S.tempMass(z.anker, z.fang.punkt, s);
    if (z.leitungsende) {
      const gefangen = z.fang.art === 'anschluss' ? z.fang.anschluss : null;
      for (const a of this.anschluesseNahe(z, ANSCHLUSS_PX * 0.8)) {
        const gross = gefangen && gefangen.element === a.element && gefangen.name === a.name;
        h += S.kreis(a.p, (gross ? 6 : 4) * s, a.verbunden ? 'wz-anschluss wz-anschluss-belegt' : 'wz-anschluss');
      }
      if (gefangen) h += S.text(gefangen.p, anschlussText(gefangen), s, { versatzPx: [12, 14], klasse: 'wz-text wz-fangtext' });
    }
    h += S.griff(z.fang.punkt, s, 'wz-griff wz-griff-zieht', GRIFF_PX);
    return h + S.fangsymbol(z.fang, s);
  }

  /** Anschlüsse anderer Elemente nahe der Maus (nur beim Ziehen eines Leitungsendes). */
  anschluesseNahe(z, pixel) {
    if (!z.maus) return [];
    return anschluesseNahe(alleAnschluesse(), z.maus, pixel * this.s()).filter((a) => a.element !== z.g.el.kennzeichen);
  }

  // ------------------------------------------------------------------------------------------ Ereignisse

  fangFuer(e) {
    const z = this.ziehen;
    const maus = this.ansicht.zuModell(e.clientX, e.clientY);
    const daten = this.ansicht.fangdaten?.() ?? { punkte: [], kanten: [] };
    // Das gezogene Element selbst und seine Fangobjekte nicht fangen (einmal je Ziehen gefiltert; N9: so rechnet fangen die Drehung nur einmal)
    const eigen = z.g.el.id;
    if (z.daten?.von !== daten) z.daten = { von: daten, punkte: (daten.punkte ?? []).filter((q) => q.id !== eigen), kanten: (daten.kanten ?? []).filter((k) => k.id !== eigen) };
    const gefiltert = z.daten;
    z.maus = maus;
    let extraPunkte = [];
    if (z.leitungsende) {
      anschluesseLaden(elementeNahe(maus, 3000, geschossVon(this.ansicht)), () => { if (this.ziehen === z) this.zeichnen(); });
      extraPunkte = anschlussFangpunkte(this.anschluesseNahe(z, ANSCHLUSS_PX));
    }
    return fangen({ punkt: maus, daten: gefiltert, radius: FANGRADIUS_PX * this.s(), letzter: z.anker, raster: rasterWert(), ortho: !!e.shiftKey,
      extraPunkte, arten: fangArten(), drehung: this.ansicht.drehung ?? 0 });   // B5: Fangeinstellungen und Fang-Kürzel   // N9: Orthogonal und Raster in den Achsen der Ansicht
  }

  ereignis(art, e, ansicht) {
    if (ansicht && ansicht !== this.ansicht) return false;
    switch (art) {
      case 'pointerdown': return this.drueck(e);
      case 'pointermove': return this.bewegung(e);
      case 'pointerup': return this.los(e);
      case 'keydown':
        if (e.key === 'Escape' && this.ziehen) { this.ziehen = null; this.zeichnen(); return true; }
        return this.pfeil(e) || this.tab(e);
      default: return false;
    }
  }

  drueck(e) {
    if (e.button !== 0) return false;
    const maus = this.ansicht.zuModell?.(e.clientX, e.clientY);
    if (!maus) return false;
    const g = this.griffAn(maus);
    if (!g) { if (e.ctrlKey || e.metaKey) strgMerken(); else if (e.shiftKey) shiftMerken(); return false; }
    if (g.art === 'tuer') {
      sende('element_aendern', tuerUmdrehen(g.el, g.was)).then((r) => ergebnisMelden(r, `${g.el.kennzeichen}: ${g.was === 'seite' ? 'Aufschlag' : 'Bandseite'} umgedreht.`));
      return true;
    }
    if (g.art === 'mass') { this.massEingeben(g); return true; }   // B5: Zahl anklicken, neuen Abstand eingeben
    if (g.art === 'mitte') {
      this.ziehen = { g, anker: g.p, fang: { punkt: g.p, art: 'frei', ortho: false }, start: g.p, mitte: true };
      window.addEventListener('pointerup', this.fenster, true);
      this.zeichnen();
      return true;
    }
    const pts = g.el.geometrie.punkte;
    // Anker für Lot/orthogonal: der Nachbarpunkt (bei Linien der andere Endpunkt)
    const nachbar = pts[g.index === 0 ? Math.min(1, pts.length - 1) : g.index - 1];
    const leitungsende = istLeitungsklasse(g.el.klasse) && (g.index === 0 || g.index === pts.length - 1);
    this.ziehen = { g, anker: nachbar ? [nachbar[0], nachbar[1]] : null, fang: { punkt: g.p, art: 'frei', ortho: false }, start: g.p, leitungsende };
    window.addEventListener('pointerup', this.fenster, true);
    this.zeichnen();
    return true;
  }

  fenster = (e) => { window.removeEventListener('pointerup', this.fenster, true); if (this.ziehen) this.los(e); };

  bewegung(e) {
    if (this.ziehen) {
      this.ziehen.fang = this.fangFuer(e);
      this.zeichnen();
      return true;
    }
    if (!this.aktiv()) return false;
    const maus = this.ansicht.zuModell?.(e.clientX, e.clientY);
    const svg = this.schicht()?.ownerSVGElement;
    const ueber = maus ? this.griffAn(maus) : null;
    if (svg) svg.style.cursor = ueber ? (ueber.art === 'tuer' || ueber.art === 'mass' ? 'pointer' : 'move') : '';
    if ((this.ansicht.pxJeMm?.() ?? 0) !== this.gezeichnetBei) this.zeichnen();
    return false;
  }

  los(e) {
    const z = this.ziehen;
    if (!z) return false;
    this.ziehen = null;
    window.removeEventListener('pointerup', this.fenster, true);
    const neu = z.fang.punkt;
    einmalVerbraucht();
    this.zeichnen();
    if (abstand(neu, z.start) < 1 && !(z.leitungsende && z.fang.art === 'anschluss')) return true;
    const g = z.g;
    if (z.mitte) { verschieben([g.el.kennzeichen], verschiebenParameter([g.el.kennzeichen], z.start, neu)); return true; }
    let par;
    const an = z.leitungsende && z.fang.art === 'anschluss' ? z.fang.anschluss : null;
    if (an && !anschlussFrei(an)) { meldung(`${an.element}.${an.name} ist schon verbunden (${an.verbunden}) – zuerst trennen.`, 'warnung'); return true; }
    if (an) par = griffAnschliessen(g.el, g.index, an);
    if (!par) {
      try { par = { element: g.el.kennzeichen, geometrie: griffGeometrie(g.el.geometrie, g.index, neu) }; } catch (err) { meldung(String(err.message ?? err), 'warnung'); return true; }
    }
    sende('element_aendern', par).then((r) => {
      ergebnisMelden(r, `${g.el.kennzeichen} geändert.`);
      // Leitungsende gezogen: der Kern führt verbundene Elemente nach und meldet, was nicht mehr passt (P13–P18, Hinweise)
      if (r.angenommen && istLeitungsklasse(g.el.klasse)) {
        const b = neueBefundeTexte(r);
        if (b.length) meldung(b.join(' · '), 'warnung');
      }
    });
    return true;
  }

  /** B5: neuen Abstand zum Bezug eingeben → elemente_verschieben (rechtwinklig). */
  async massEingeben(g) {
    const m = g.mass;
    const t = await eingabe(`Abstand ${g.el.kennzeichen} – ${m.ref} (${m.was})`, 'Abstand in mm', String(m.abstand));
    if (t === null) return;
    const neu = Number(t.replace(',', '.'));
    if (!Number.isFinite(neu) || neu < 0) { meldung('Bitte einen Abstand in mm ≥ 0 eingeben.', 'warnung'); return; }
    const v = massVerschiebung(m, neu);
    if (Math.abs(v.dx) < 0.001 && Math.abs(v.dy) < 0.001) return;
    verschieben([g.el.kennzeichen], { elemente: [g.el.kennzeichen], ...v });
  }

  /** B4: Tab wählt den Leitungszug des gewählten Elements. */
  tab(e) {
    if (e.key !== 'Tab' || this.ziehen || e.shiftKey || e.ctrlKey || e.altKey) return false;
    const alle = auswahlElemente();
    if (alle.length !== 1) return false;
    zugWaehlen(alle[0]).then((n) => { if (!n) meldung('Hier gibt es keinen Leitungszug (Tab wählt Leitungen samt Bögen).', 'info'); });
    return true;
  }

  /** A1: Pfeiltasten verschieben die Auswahl um den Rasterwert (ohne Umschalt, Strg, Alt). */
  pfeil(e) {
    if (this.ziehen || e.shiftKey || e.ctrlKey || e.metaKey || e.altKey) return false;
    const alle = auswahlElemente().filter((el) => el.geometrie && this.gehoert(el));
    if (!alle.length) return false;
    const kz = alle.map((el) => el.kennzeichen);
    const par = pfeilParameter(kz, e.key, rasterWert(), this.ansicht.drehung ?? 0);
    if (!par) return false;
    verschieben(kz, par);
    return true;
  }
}

/** elemente_verschieben senden; Hinweise des Kerns zum Netz (folgt, Versatz) erscheinen in der Meldung. */
function verschieben(kz, par) {
  sende('elemente_verschieben', par).then((r) => {
    const netz = (r.hinweise ?? []).filter((h) => /Versatz|folgt|getrennt/.test(h));
    ergebnisMelden(r, [`${kz.length === 1 ? kz[0] : `${kz.length} Elemente`} verschoben.`, ...netz.slice(0, 3)].join(' '));
    const b = neueBefundeTexte(r);
    if (r.angenommen && b.length) meldung(b.join(' · '), 'warnung');
  });
}

// ------------------------------------------------------------------------------------------------ Verwaltung

function ansichtenAufraeumen() {
  for (const [a] of mitGriffen) if (a.werkzeugSchicht && a.werkzeugSchicht.isConnected === false) mitGriffen.delete(a);
}

/** Setzt das Griff-Werkzeug in die aktive Plan-Ansicht (wenn kein anderes Werkzeug läuft) und zeichnet die Griffe neu. */
export function griffeAktualisieren() {
  ansichtenAufraeumen();
  const a = planAnsicht();
  if (a && !werkzeugAktiv()) {
    let g = mitGriffen.get(a);
    if (!g) { g = new Griffe(a); mitGriffen.set(a, g); }
    if (!a.werkzeug) a.setzeWerkzeug(g.objekt);
  }
  // Die Ansicht schaltet bei jedem Werkzeug auf Fadenkreuz und unterdrückt den Hover; für das passive Griff-Werkzeug nicht.
  for (const [ansicht, g] of mitGriffen) if (ansicht.werkzeug === g.objekt) ansicht.werkzeugSchicht?.ownerSVGElement?.classList.remove('werkzeug-aktiv');
  for (const [ansicht, g] of mitGriffen) {
    if (ansicht.werkzeug === g.objekt && !g.ziehen) g.zeichnen();
  }
}

let eingerichtet = false;
export function griffeEinrichten() {
  if (eingerichtet) return;
  eingerichtet = true;
  const spaeter = () => setTimeout(griffeAktualisieren, 0);
  abonnieren('ansicht', spaeter);
  abonnieren('auswahl', spaeter);
  abonnieren('modell', () => { modellStand++; spaeter(); });
  beiAuswahlMenge(spaeter);
  beiWerkzeugEnde(spaeter);
  // Ansichten werden asynchron geöffnet; kurze Nachprüfung nach dem Start und bei Fenster-Änderungen
  window.addEventListener('resize', spaeter);
  setTimeout(griffeAktualisieren, 800);
}

