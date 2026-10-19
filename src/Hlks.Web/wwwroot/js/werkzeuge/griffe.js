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

import { element, abonnieren } from '../zustand.js';
import { sende, ergebnisMelden } from '../oberflaeche/befehle.js';
import { meldung } from '../oberflaeche/dialoge.js';
import { planAnsicht, werkzeugAktiv, beiWerkzeugEnde, geschossVon } from './basis.js';
import { fangen, abstand, FANGRADIUS_PX } from './fangen.js';
import { griffPunkte, griffGeometrie, tuerUmdrehen } from './parameter.js';
import { rasterWert } from './optionen.js';
import { auswahlElemente, strgMerken, beiAuswahlMenge } from './auswahl.js';
import * as S from './schicht.js';
import { neueBefundeTexte, istLeitungsklasse } from './leitung-logik.js';

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
      return griffPunkte(el.geometrie).map((p, i) => ({ art: 'punkt', el, index: i, p }));
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
    this.gezeichnetBei = this.ansicht.pxJeMm?.() ?? 0;
    let h = '';
    const alle = auswahlElemente();
    if (alle.length > 1) h += S.umrisseSvg(alle, (p) => p, s, 'wz-auswahl');
    if (this.ziehen) h += this.zeichneZiehen(s);
    else for (const g of this.griffe()) h += g.art === 'punkt' ? S.griff(g.p, s, 'wz-griff', GRIFF_PX) : S.doppelpfeil(g.p, g.d, s);
    sch.innerHTML = h;
  }

  zeichneZiehen(s) {
    const z = this.ziehen, g = z.g;
    const neu = griffGeometrie(g.el.geometrie, g.index, z.fang.punkt);
    const pts = neu.punkte.map((p) => [p[0], p[1]]);
    let h = '';
    if (g.el.klasse === 'Wand' && neu.dicke > 0) h += S.band(pts[0], pts[1], neu.dicke, 0);
    else h += S.polylinie(pts, 'wz-linie');
    if (z.anker) h += S.tempMass(z.anker, z.fang.punkt, s);
    h += S.griff(z.fang.punkt, s, 'wz-griff wz-griff-zieht', GRIFF_PX);
    return h + S.fangsymbol(z.fang, s);
  }

  // ------------------------------------------------------------------------------------------ Ereignisse

  fangFuer(e) {
    const z = this.ziehen;
    const maus = this.ansicht.zuModell(e.clientX, e.clientY);
    const daten = this.ansicht.fangdaten?.() ?? { punkte: [], kanten: [] };
    // Das gezogene Element selbst und seine Fangobjekte nicht fangen
    const eigen = z.g.el.id;
    const gefiltert = { punkte: (daten.punkte ?? []).filter((q) => q.id !== eigen), kanten: (daten.kanten ?? []).filter((k) => k.id !== eigen) };
    return fangen({ punkt: maus, daten: gefiltert, radius: FANGRADIUS_PX * this.s(), letzter: z.anker, raster: rasterWert(), ortho: !!e.shiftKey });
  }

  ereignis(art, e, ansicht) {
    if (ansicht && ansicht !== this.ansicht) return false;
    switch (art) {
      case 'pointerdown': return this.drueck(e);
      case 'pointermove': return this.bewegung(e);
      case 'pointerup': return this.los(e);
      case 'keydown':
        if (e.key === 'Escape' && this.ziehen) { this.ziehen = null; this.zeichnen(); return true; }
        return false;
      default: return false;
    }
  }

  drueck(e) {
    if (e.button !== 0) return false;
    const maus = this.ansicht.zuModell?.(e.clientX, e.clientY);
    if (!maus) return false;
    const g = this.griffAn(maus);
    if (!g) { if (e.ctrlKey || e.metaKey) strgMerken(); return false; }
    if (g.art === 'tuer') {
      sende('element_aendern', tuerUmdrehen(g.el, g.was)).then((r) => ergebnisMelden(r, `${g.el.kennzeichen}: ${g.was === 'seite' ? 'Aufschlag' : 'Bandseite'} umgedreht.`));
      return true;
    }
    const pts = g.el.geometrie.punkte;
    // Anker für Lot/orthogonal: der Nachbarpunkt (bei Linien der andere Endpunkt)
    const nachbar = pts[g.index === 0 ? Math.min(1, pts.length - 1) : g.index - 1];
    this.ziehen = { g, anker: nachbar ? [nachbar[0], nachbar[1]] : null, fang: { punkt: g.p, art: 'frei', ortho: false }, start: g.p };
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
    if (svg) svg.style.cursor = ueber ? (ueber.art === 'tuer' ? 'pointer' : 'move') : '';
    if ((this.ansicht.pxJeMm?.() ?? 0) !== this.gezeichnetBei) this.zeichnen();
    return false;
  }

  los(e) {
    const z = this.ziehen;
    if (!z) return false;
    this.ziehen = null;
    window.removeEventListener('pointerup', this.fenster, true);
    const neu = z.fang.punkt;
    this.zeichnen();
    if (abstand(neu, z.start) < 1) return true;
    const g = z.g;
    let geometrie;
    try { geometrie = griffGeometrie(g.el.geometrie, g.index, neu); } catch (err) { meldung(String(err.message ?? err), 'warnung'); return true; }
    sende('element_aendern', { element: g.el.kennzeichen, geometrie }).then((r) => {
      ergebnisMelden(r, `${g.el.kennzeichen} geändert.`);
      // Leitungsende gezogen: der Kern führt verbundene Elemente nach und meldet, was nicht mehr passt (P13–P18, Hinweise)
      if (r.angenommen && istLeitungsklasse(g.el.klasse)) {
        const b = neueBefundeTexte(r);
        if (b.length) meldung(b.join(' · '), 'warnung');
      }
    });
    return true;
  }
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
  abonnieren('modell', spaeter);
  beiAuswahlMenge(spaeter);
  beiWerkzeugEnde(spaeter);
  // Ansichten werden asynchron geöffnet; kurze Nachprüfung nach dem Start und bei Fenster-Änderungen
  window.addEventListener('resize', spaeter);
  setTimeout(griffeAktualisieren, 800);
}

