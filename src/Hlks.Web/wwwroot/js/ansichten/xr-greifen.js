// Modul XR, Teile XR-B (Greifen) und XR-C (Tablet, Messwerkzeug): der Anschluss an die VR-Sitzung (vr3d.js). Die Rechenregeln stehen in xr-greifen-logik.js und
// xr-mess-logik.js (Node-Tests: xr-b.test.mjs), das Tablet in xr-tablet.js, das Messwerkzeug in xr-messen.js, der Stil in xr-holo.js.
//
// Eingaben: Hand-Tracking (Gelenke) oder Controller (Grifftaste, Abzug, Strahl). Beides ist hier gekapselt: api.haende() liefert je Seite die Rohwerte
//   { art: 'hand' | 'controller', ort, gelenke, griffTaste, abzug, menue, strahl: { u, r } }  – Test-Naht der Browser-Reihe (xr-b.browser.mjs);
// ohne Naht liest der Standard die Hände aus renderer.xr.getHand(i) bzw. die Controller aus den XRInputSources. Kommt `hand` über Air Link nicht an, gelten nur die Controller.
//
// Greifen: Ein Element in Reichweite (0.6 m) wird mit der Faust bzw. der Grifftaste gegriffen, folgt der Hand (1-cm-Raster, Fang an Ecken und Anschlüssen anderer Elemente)
// und wird beim Loslassen mit EINEM Befehl des Kerns verschoben (Paket, Rückgängig). Leitung: erst antippen (Abzug/Pinch auf die Leitung), dann greifen; sie bewegt sich nur quer
// zur Längsachse, in 1-cm-Schritten, mit den Massen zu Decke und Wänden. Weit Entferntes: Zeigestrahl plus Abzug/Pinch holt es heran.
// Reservierung: Der Kern kennt (Stand 21.10.2026) keine Reservierung auf Zuruf – er sperrt ein Element nur, während ein Befehl läuft. Hier wird beim Greifen geprüft, ob ein anderer
// das Element gerade reserviert hat (/api/status); dann gibt es einen Hinweis und kein Greifen. Dazwischen kann ein anderer noch ändern; der Befehl beim Loslassen lehnt das ab.
//
// Tablet: linke Hand offen, Handfläche zum Kopf (ersatzweise Taste Y links, die Menütaste der Quest gehört dem System); Tippen mit dem rechten Zeigefinger bzw. dem Strahl.

import * as THREE from 'three';
import {
  GREIF_REICHWEITE, istFaust, istPinch, handflaechenMitte, handflaechenNormale, tabletGeste, tabletLage, tabletBeruehrung, tabletStrahl, elementInReichweite, abstandZuElement,
  querVerschiebung, querAchsen, befehlsMasse, aufSchritt, eigenschaftenZeilen, heranholAnteil, HERANHOLEN_ABSTAND, SCHRITT_M,
} from './xr-greifen-logik.js';
import { erzeugeFangdaten, fangenPunkt, fangenStrahl, deckeUndWaende, v3, plus, minus, mal, einheit, laenge, massKurz, mitteVon3 } from './xr-mess-logik.js';
import { erzeugeTablet } from './xr-tablet.js';
import { erzeugeMessen } from './xr-messen.js';
import { etikett, holoLinie } from './xr-holo.js';

const GELENKE = ['wrist', 'thumb-metacarpal', 'thumb-phalanx-proximal', 'thumb-phalanx-distal', 'thumb-tip', 'index-finger-metacarpal', 'index-finger-phalanx-proximal', 'index-finger-phalanx-intermediate',
  'index-finger-phalanx-distal', 'index-finger-tip', 'middle-finger-metacarpal', 'middle-finger-phalanx-proximal', 'middle-finger-phalanx-intermediate', 'middle-finger-phalanx-distal', 'middle-finger-tip',
  'ring-finger-metacarpal', 'ring-finger-phalanx-proximal', 'ring-finger-phalanx-intermediate', 'ring-finger-phalanx-distal', 'ring-finger-tip', 'pinky-finger-metacarpal', 'pinky-finger-phalanx-proximal',
  'pinky-finger-phalanx-intermediate', 'pinky-finger-phalanx-distal', 'pinky-finger-tip'];
/** Ebenen, die man greifen kann (Räume, Decken und Öffnungen nicht: Decken ändert man am PC, Öffnungen wandern mit ihrer Wand). */
const GREIFBAR = new Set(['leitung', 'formteil', 'armatur', 'apparat', 'wand', 'sonst']);
/** Ebenen, die der Zeigestrahl antippt bzw. heranholt. */
const FERN = new Set(['leitung', 'formteil', 'armatur', 'apparat']);
const VORRANG = { leitung: 3, formteil: 3, armatur: 3, apparat: 3, wand: 1, sonst: 1 };
const SCHREIB_MS = 250;
const wv = (p) => new THREE.Vector3(p.x, p.y, p.z);
const rund = (x, n = 3) => { const f = 10 ** n; return Math.round(x * f) / f; };
const pt = (v) => (v ? { x: rund(v.x), y: rund(v.y), z: rund(v.z) } : null);

/**
 * o: { wurzel, renderer, szene, rig() → THREE.Group, controller() → [c0, c1], quellen() → XRInputSource[], kopf() → Welt-Kopf | null, koerperDaten() → Map id → Körper,
 *      element(id|kennzeichen), gewaehlt(), auswaehlen(id), befehl(name, par), abfrage(name, par), status(), verbindungen() → Liste, meldung(text, art), anfordern(),
 *      aktionen: { hoehe(dm), schnitt(), schnittAn(), schnittVorhanden(), ifc(), ifcAn(), ende() }, zeit?(),
 *      sitzung?: Sitzungsraum XR-H (xr-sitzung.js): { aktiv, aktion(id) → bool, layout(), tastenZustand(), kopfZeilen(), bild(dt, seiten) } }
 */
export function erzeugeXr(o) {
  const api = { haende: null };
  const daten = erzeugeFangdaten();
  const quelleKoerper = new Map();     // id → Körper-Objekt, das zuletzt in die Fangdaten ging
  let letzterSync = -1e9;
  let uhr = 0, letztesSchreiben = -1e9;
  const mem = { left: { griff: false, pinch: false, menue: false, beruehrung: false }, right: { griff: false, pinch: false, menue: false, beruehrung: false } };
  let seiten = { left: null, right: null };   // ausgewertet
  let grab = null;                    // laufendes Greifen
  let startet = false;                // Reservierung wird geprüft
  let tabletLageJetzt = null;
  let tabletGlatt = null;
  let tabletUrsache = null;           // 'geste' | 'taste'
  let letztesGreifen = null;          // zuletzt gesendeter Befehl { befehl, parameter, angenommen }
  let faehigkeiten = null;            // Zwischenspeicher der Abfrage faehigkeiten
  const hinweisZeit = { text: '', t: -10 };
  const handMesh = {};
  let massAnzeige = null;
  const wurzelGruppe = new THREE.Group(); wurzelGruppe.name = 'vr-xr';
  o.szene.add(wurzelGruppe);

  // ---------------------------------------------------------------------------------------------- Fangdaten
  function fangSync(erzwingen = false) {
    if (!erzwingen && uhr - letzterSync < 0.5) return;
    letzterSync = uhr;
    const alle = o.koerperDaten();
    for (const [id, k] of alle) if (quelleKoerper.get(id) !== k) { daten.setzen(k); quelleKoerper.set(id, k); }
    for (const id of [...quelleKoerper.keys()]) if (!alle.has(id)) { daten.entfernen(id); quelleKoerper.delete(id); }
  }

  // ---------------------------------------------------------------------------------------------- Messwerkzeug und Tablet
  const messen = erzeugeMessen({ szene: wurzelGruppe, daten, ausser: () => (grab ? new Set([grab.id]) : undefined) });
  const hinweis = (text, art = 'info') => { hinweisZeit.text = text; hinweisZeit.t = uhr; o.meldung?.(text, art); };
  const tablet = erzeugeTablet({
    szene: wurzelGruppe, zeit: () => uhr,
    layout: () => o.sitzung?.layout?.() ?? null,   // XR-H: im Sitzungsraum zeigt das Tablet die Etagen
    eigenschaften: () => o.sitzung?.kopfZeilen?.() ?? eigenschaftenZeilen(o.element(grab?.id ?? o.gewaehlt())),
    messZeilen: () => {
      if (!messen.an) return [];
      const l = messen.letzte();
      return [messen.hinweis(), ...l].filter(Boolean);
    },
    tastenZustand: () => ({ messen: messen.an, lot: messen.lot, schnitt: !!o.aktionen.schnittAn?.(), ifc: !!o.aktionen.ifcAn?.(), ...(o.sitzung?.tastenZustand?.() ?? {}) }),
    aktion: (id) => aktion(id),
  });

  async function befehlSenden(name, par = {}) {
    try {
      const r = await o.befehl(name, par);
      letztesGreifen = { befehl: name, parameter: par, angenommen: !!r.angenommen, meldung: r.fehler ?? r.meldung ?? '' };
      return r;
    } catch (e) {
      letztesGreifen = { befehl: name, parameter: par, angenommen: false, meldung: String(e?.message ?? e) };
      return { angenommen: false, fehler: String(e?.message ?? e) };
    }
  }
  async function aktion(id) {
    if (id === 'raum') { messen.anSetzen(false); messen.lotSetzen(false); }   // XR-H: im Sitzungsraum gibt es kein Messen am Modell
    if (o.sitzung?.aktion(id)) { schreiben(true); return; }                   // XR-H: Wechsel, Etagen, Zurücksetzen (xr-sitzung.js)
    switch (id) {
      case 'rueckgaengig': case 'wiederholen': {
        const r = await befehlSenden(id);
        hinweis(r.angenommen ? (id === 'rueckgaengig' ? 'Rückgängig gemacht' : 'Wiederholt') : (r.fehler || r.meldung || 'Nicht möglich'), r.angenommen ? 'info' : 'warnung');
        break;
      }
      case 'messen': messen.anSetzen(!messen.an); if (!messen.an) messen.lotSetzen(false); break;
      case 'lot': if (!messen.lot) { messen.anSetzen(true); messen.lotSetzen(true); } else messen.lotSetzen(false); break;
      case 'messungenLoeschen': messen.loeschen(); break;
      case 'hoeher': o.aktionen.hoehe?.(0.1); break;
      case 'tiefer': o.aktionen.hoehe?.(-0.1); break;
      case 'schnitt': if (o.aktionen.schnittVorhanden?.() === false) hinweis('Diese Ansicht hat keinen Schnittbereich', 'warnung'); else o.aktionen.schnitt?.(); break;
      case 'ifc': o.aktionen.ifc?.(); break;
      case 'ende': o.aktionen.ende?.(); break;
      default: break;
    }
    schreiben(true);
  }

  // ---------------------------------------------------------------------------------------------- Rohwerte der Hände und Controller
  const tmp = new THREE.Vector3(), tmpR = new THREE.Vector3();
  function seitenIndex(seite) { return o.quellen().findIndex((q) => q?.handedness === seite); }
  function strahlVon(c) {
    c.updateMatrixWorld(true);
    const u = new THREE.Vector3().setFromMatrixPosition(c.matrixWorld);
    const r = new THREE.Vector3(0, 0, -1).transformDirection(c.matrixWorld);
    return { u: { x: u.x, y: u.y, z: u.z }, r: { x: r.x, y: r.y, z: r.z } };
  }
  function rohStandard() {
    const roh = { left: null, right: null };
    const quellen = o.quellen(), cs = o.controller();
    quellen.forEach((q, i) => {
      if (!q || (q.handedness !== 'left' && q.handedness !== 'right')) return;
      const c = cs[i];
      if (q.hand) {
        const h = o.renderer.xr.getHand(i);
        const g = {};
        for (const n of GELENKE) { const j = h.joints?.[n]; if (j) { h.updateMatrixWorld?.(true); tmp.setFromMatrixPosition(j.matrixWorld); g[n] = { x: tmp.x, y: tmp.y, z: tmp.z }; } }
        roh[q.handedness] = { art: 'hand', gelenke: g, strahl: c ? strahlVon(c) : null };
      } else {
        const b = q.gamepad?.buttons ?? [];
        c?.updateMatrixWorld(true);
        const p = c ? tmpR.setFromMatrixPosition(c.matrixWorld) : null;
        roh[q.handedness] = { art: 'controller', ort: p ? { x: p.x, y: p.y, z: p.z } : null, griffTaste: !!b[1]?.pressed, abzug: !!b[0]?.pressed, menue: q.handedness === 'left' && !!b[5]?.pressed, strahl: c ? strahlVon(c) : null };
      }
    });
    return roh;
  }
  function auswerten(seite, r) {
    const m = mem[seite];
    if (!r) { m.griff = false; m.pinch = false; return null; }
    if (r.art === 'hand' && r.gelenke) {
      const griff = istFaust(r.gelenke, m.griff), pinch = istPinch(r.gelenke, m.pinch);
      m.griff = griff; m.pinch = pinch;
      return { art: 'hand', griff, pinch, ort: handflaechenMitte(r.gelenke), spitze: r.gelenke['index-finger-tip'] ?? null, gelenke: r.gelenke, strahl: r.strahl ?? null, menue: false };
    }
    const griff = !!r.griffTaste, pinch = !!r.abzug;
    m.griff = griff; m.pinch = pinch;
    return { art: 'controller', griff, pinch, ort: r.ort ?? null, spitze: null, gelenke: null, strahl: r.strahl ?? null, menue: !!r.menue };
  }

  // ---------------------------------------------------------------------------------------------- Tablet: Geste und Berührung
  function tabletPflegen(dt) {
    const kopf = o.kopf();
    const L = seiten.left;
    let lage = null;
    if (L && kopf) {
      if (L.art === 'hand' && L.gelenke) {
        const soll = tabletGeste(L.gelenke, 'left', kopf, tablet.offen && tabletUrsache === 'geste');
        const n = handflaechenNormale(L.gelenke, 'left');
        lage = n && L.ort ? tabletLage(L.ort, n, kopf) : null;
        if (soll && lage) { if (!tablet.offen) { tabletUrsache = 'geste'; tabletGlatt = lage; tablet.oeffnen(L.ort); } }
        else if (tablet.offen && tabletUrsache === 'geste') tablet.schliessen();
      } else if (L.art === 'controller') {
        lage = L.ort ? tabletLage(L.ort, { x: 0, y: 1, z: 0 }, kopf) : null;
        if (L.menue && !mem.left.menue) {   // Taste Y links: ein Druck öffnet, der nächste schliesst
          if (tablet.offen) { tablet.schliessen(); tabletUrsache = null; }
          else if (lage) { tabletUrsache = 'taste'; tabletGlatt = lage; tablet.oeffnen(L.ort); }
        }
        mem.left.menue = L.menue;
      }
    } else if (tablet.offen) tablet.schliessen();
    if (lage) {
      tabletGlatt = tabletGlatt ? glaetten(tabletGlatt, lage, Math.min(1, dt * 14)) : lage;
      tabletLageJetzt = tabletGlatt;
    }
    tablet.bild(dt, tabletLageJetzt);
    // Tippen mit dem rechten Zeigefinger
    const R = seiten.right;
    const m = mem.right;
    if (R?.art === 'hand' && R.spitze && tablet.offen && tabletLageJetzt) {
      const b = tabletBeruehrung(tabletLageJetzt, R.spitze);
      if (b && !m.beruehrung) tablet.tippen(b.u, b.v);
      m.beruehrung = !!b;
    } else m.beruehrung = false;
  }
  function glaetten(a, b, k) {
    const mix = (p, q) => ({ x: p.x + (q.x - p.x) * k, y: p.y + (q.y - p.y) * k, z: p.z + (q.z - p.z) * k });
    return { ...b, mitte: mix(a.mitte, b.mitte), normale: einheit(mix(a.normale, b.normale)), rechts: einheit(mix(a.rechts, b.rechts)), oben: einheit(mix(a.oben, b.oben)) };
  }

  // ---------------------------------------------------------------------------------------------- Messen: Sonde und Setzen (rechte Seite)
  function sondePflegen() {
    if (!messen.an) return;
    const R = seiten.right;
    if (!R) { messen.sonde(null); return; }
    fangSync();
    if (R.art === 'hand' && R.spitze) {
      messen.sonde({ art: 'punkt', p: R.spitze });
      if (R.pinch && !mem.right.pinchAlt && !(tablet.offen && tabletLageJetzt && tabletBeruehrung(tabletLageJetzt, R.spitze))) { messen.setzen(); schreiben(true); }
    } else if (R.strahl) messen.sonde({ art: 'strahl', u: R.strahl.u, r: R.strahl.r });
    else messen.sonde(null);
    mem.right.pinchAlt = R.art === 'hand' ? R.pinch : false;
  }

  // ---------------------------------------------------------------------------------------------- Greifen
  const ebeneVon = (id) => daten.get(id)?.ebene ?? o.koerperDaten().get(id)?.ebene ?? 'sonst';
  function kandidaten() {
    const k = [];
    for (const z of daten.alle()) {
      if (!GREIFBAR.has(z.ebene)) continue;
      k.push({ id: z.id, box: z.box, vorrang: VORRANG[z.ebene] ?? 1, strecken: z.ebene === 'leitung' ? z.strecken.filter((s) => s.art === 'achse').map((s) => ({ a: s.a, b: s.b, radius: s.radius })) : null });
    }
    return k;
  }
  function elementVon(id) { return o.element(id); }
  function hatNachbarn(kz) {
    const k = String(kz).toLowerCase();
    return (o.verbindungen() ?? []).some((v) => String(v.von).toLowerCase() === k || String(v.nach).toLowerCase() === k);
  }
  /** Reserviert ein anderer das Element? → Text der Meldung oder null. */
  async function fremdReserviert(el) {
    try {
      const st = await o.status();
      const r = (st?.reservierungen ?? []).find((x) => String(x.element).toLowerCase() === String(el.kennzeichen).toLowerCase() && !x.eigene);
      return r ? `${el.kennzeichen} ist von ${r.akteur}@${r.geraet} bis ${r.bis} reserviert – nicht greifbar` : null;
    } catch { return null; }
  }

  async function greifenStart(seite, ort, fern = null) {
    if (grab || startet || !ort) return;
    fangSync(true);
    let ziel = null;
    if (fern) ziel = { id: fern.id, abstand: fern.abstand };
    else ziel = elementInReichweite(ort, kandidaten(), GREIF_REICHWEITE);
    if (!ziel) return;
    const el = elementVon(ziel.id);
    if (!el) return;
    const ebene = ebeneVon(ziel.id);
    if (ebene === 'leitung' && !fern && o.gewaehlt() !== ziel.id) { o.auswaehlen(ziel.id); hinweis('Leitung gewählt – zum Verschieben nochmals greifen', 'info'); schreiben(true); return; }   // erst antippen, dann greifen
    startet = true;
    const grund = await fremdReserviert(el);
    startet = false;
    if (grund) { hinweis(grund, 'warnung'); schreiben(true); return; }
    // das Greifen ist inzwischen vielleicht beendet (Hand wieder offen)
    if (!fern && !mem[seite].griff) return;
    if (fern && !fern.haelt()) return;
    o.auswaehlen(ziel.id);
    const z = daten.get(ziel.id);
    const grp = o.objekte().get(ziel.id);
    let achse = null, anker = z.box ? mitteVon3(z.box.min, z.box.max) : ort, radius = 0;
    if (ebene === 'leitung') {
      const strecken = z.strecken.filter((s) => s.art === 'achse');
      let beste = strecken[0], bd = Infinity;
      for (const s of strecken) { const d = abstandZuElement(ort, { strecken: [{ a: s.a, b: s.b, radius: 0 }] }); if (d < bd) { bd = d; beste = s; } }
      if (beste) { achse = minus(beste.b, beste.a); anker = mitteVon3(beste.a, beste.b); radius = beste.radius ?? 0; }
    }
    grab = {
      seite, id: ziel.id, kz: el.kennzeichen, ebene, art: fern ? 'fern' : 'nah', start: { ...ort }, anker, achse, radius, grp, offset: v3(), oben: 0, seitlich: 0,
      eigenePunkte: z.punkte.slice(0, 40).map((p) => p.p), t: 0, fernZiel: null, fern,
    };
    if (fern) {
      const richtung = einheit(minus(fern.punkt, ort));
      // Elemente werden zur Hand geholt; eine Leitung bleibt dabei an ihrem Ort (sie bewegt sich nur quer, mit der Hand) – sonst würde sie quer durch den Raum wandern
      grab.fernZiel = ebene === 'leitung' ? fern.punkt : plus(ort, mal(richtung, HERANHOLEN_ABSTAND));
      grab.fernPunkt = fern.punkt;
    }
    schreiben(true);
  }

  function griffAktualisieren(dt) {
    if (!grab) return;
    const S = seiten[grab.seite];
    const ort = S?.ort ?? (S?.art === 'controller' ? S.ort : null);
    if (!ort) return;
    grab.t += dt;
    let delta = minus(ort, grab.start);
    if (grab.art === 'fern') delta = plus(delta, mal(minus(grab.fernZiel, grab.fernPunkt), heranholAnteil(grab.t)));
    let offset;
    if (grab.ebene === 'leitung' && grab.achse) {
      const q = querVerschiebung(grab.achse, delta, SCHRITT_M);
      offset = q.offset; grab.oben = q.oben; grab.seitlich = q.seitlich;
    } else {
      offset = v3(aufSchritt(delta.x), aufSchritt(delta.y), aufSchritt(delta.z));
      // Fang: eigene Eckpunkte an Ecken und Anschlüsse anderer Elemente (5 cm)
      let best = null;
      for (const p of grab.eigenePunkte) {
        const f = fangenPunkt(daten, plus(p, offset), { radius: 0.05, ausser: new Set([grab.id]) });
        if (f.art === 'anschluss' || f.art === 'ecke') {
          const adj = minus(f.p, plus(p, offset)), l = laenge(adj);
          if (!best || l < best.l) best = { adj, l };
        }
      }
      if (best) offset = plus(offset, best.adj);
      grab.fang = best ? { l: rund(best.l, 4) } : null;
    }
    grab.offset = offset;
    grab.grp?.position.set(offset.x, offset.y, offset.z);
    grab.grp?.updateMatrixWorld(true);
    massePflegen();
    o.anfordern?.();
  }

  function massePflegen() {
    if (!grab || grab.ebene !== 'leitung' || !grab.achse) { massAnzeige?.verstecken(); return; }
    if (!massAnzeige) massAnzeige = massAnzeigeBauen();
    const p = plus(grab.anker, grab.offset);
    const { s } = querAchsen(grab.achse);
    const erg = deckeUndWaende(daten, p, [s, mal(s, -1)], { ausser: new Set([grab.id]), radius: grab.radius });
    grab.masse = { decke: erg.decke && { rand: Math.round(erg.decke.abstandRandMm), achse: Math.round(erg.decke.abstandMm), id: erg.decke.id }, wandA: erg.wandA && { rand: Math.round(erg.wandA.abstandRandMm), achse: Math.round(erg.wandA.abstandMm), id: erg.wandA.id }, wandB: erg.wandB && { rand: Math.round(erg.wandB.abstandRandMm), achse: Math.round(erg.wandB.abstandMm), id: erg.wandB.id } };
    massAnzeige.setzen(p, { decke: erg.decke, wandA: erg.wandA, wandB: erg.wandB }, grab);
  }
  function massAnzeigeBauen() {
    const teile = ['decke', 'wandA', 'wandB'].map((n) => ({ n, linie: holoLinie(), e: etikett('', { breite: 0.2 }) }));
    const versatz = etikett('', { breite: 0.2 });
    for (const t of teile) { wurzelGruppe.add(t.linie.linie, t.e.sprite); }
    wurzelGruppe.add(versatz.sprite);
    return {
      teile, versatz,
      setzen(p, erg, g) {
        for (const t of teile) {
          const r = erg[t.n];
          t.linie.linie.visible = t.e.sprite.visible = !!r;
          if (!r) continue;
          t.linie.setzen(p, r.punkt);
          t.e.setzen(`${t.n === 'decke' ? 'Decke' : 'Wand'} ${massKurz(r.abstandRandMm)}\nAchse ${massKurz(r.abstandMm)}`);
          const m = mitteVon3(p, r.punkt); t.e.sprite.position.set(m.x, m.y, m.z);
        }
        versatz.setzen(`Versatz hoch ${(g.oben * 100).toFixed(0)} cm\nquer ${(g.seitlich * 100).toFixed(0)} cm`);
        versatz.sprite.position.set(p.x, p.y + 0.12, p.z); versatz.sprite.visible = true;
      },
      verstecken() { for (const t of teile) t.linie.linie.visible = t.e.sprite.visible = false; versatz.sprite.visible = false; },
      freigeben() { for (const t of teile) { t.linie.linie.removeFromParent(); t.linie.freigeben(); t.e.sprite.removeFromParent(); t.e.freigeben(); } versatz.sprite.removeFromParent(); versatz.freigeben(); },
    };
  }

  /** Loslassen: aus dem Versatz wird EIN Befehl des Kerns (Paket, Rückgängig); scheitert er oder ist das Element reserviert, bleibt es an Ort und es gibt einen Hinweis. */
  async function loslassen() {
    const g = grab;
    if (!g) return;
    grab = null;
    massAnzeige?.verstecken();
    const zuruecksetzen = () => { g.grp?.position.set(0, 0, 0); g.grp?.updateMatrixWorld(true); o.anfordern?.(); };
    const m = befehlsMasse(g.offset);
    if (m.dx === 0 && m.dy === 0 && m.dz === 0) { zuruecksetzen(); schreiben(true); return; }
    let r;
    if (g.ebene === 'leitung') {
      if (faehigkeiten === null) { try { faehigkeiten = JSON.stringify(await o.abfrage('faehigkeiten', {})); } catch { faehigkeiten = ''; } }
      if (faehigkeiten.includes('"leitung_versetzen"')) r = await befehlSenden('leitung_versetzen', { leitung: g.kz, ...m });
      else if (!hatNachbarn(g.kz)) r = await befehlSenden('elemente_verschieben', { elemente: [g.kz], ...m });
      else {
        zuruecksetzen();
        hinweis(`${g.kz} hängt an Formteilen oder Nachbarleitungen. Der Kern kann verbundene Leitungen noch nicht quer versetzen (Befehl leitung_versetzen fehlt) – nichts verschoben`, 'warnung');
        letztesGreifen = { befehl: 'leitung_versetzen', parameter: { leitung: g.kz, ...m }, angenommen: false, meldung: 'fehlt im Kern' };
        schreiben(true);
        return;
      }
    } else r = await befehlSenden('elemente_verschieben', { elemente: [g.kz], ...m });
    if (!r.angenommen) {
      zuruecksetzen();
      hinweis(`${g.kz}: ${r.gesperrt?.length ? 'von jemand anderem reserviert – ' : ''}${r.fehler || r.meldung || 'abgelehnt'} – nichts verschoben`, 'warnung');
    } else hinweis(`${g.kz} verschoben (${massKurz(Math.hypot(m.dx, m.dy, m.dz))})`, 'info');
    schreiben(true);
  }

  // ---------------------------------------------------------------------------------------------- Zeigestrahl (Abzug bzw. Pinch mit Strahl)
  /** Rückgabe true = der Druck gehört dem Tablet, dem Messwerkzeug oder dem Antippen/Heranholen eines Elements (kein Teleport). */
  function selectStart(c) {
    const i = o.controller().indexOf(c);
    const q = o.quellen()[i];
    const seite = q?.handedness ?? 'right';
    const ray = strahlVon(c);
    if (tablet.offen && tabletLageJetzt) {
      const h = tabletStrahl(tabletLageJetzt, ray.u, ray.r);
      if (h) { tablet.tippen(h.u, h.v); schreiben(true); return true; }
    }
    if (o.sitzung?.aktiv) return true;   // XR-H: im Sitzungsraum gibt es weder Teleport noch Antippen von Elementen
    if (messen.an) {
      if (!q?.hand && seite === 'right') { fangSync(); messen.sonde({ art: 'strahl', u: ray.u, r: ray.r }); messen.setzen(); schreiben(true); }
      return true;
    }
    fangSync();
    const t = fangenStrahl(daten, ray.u, ray.r, {});
    if (!t.id || !FERN.has(ebeneVon(t.id))) return false;
    const el = elementVon(t.id);
    if (!el) return false;
    const ort = q?.hand ? (seiten[seite]?.ort ?? ray.u) : ray.u;   // Controller: der Strahlursprung ist die Hand (genauer als der Wert des letzten Bildes)
    const z = daten.get(t.id);
    const nahe = abstandZuElement(ort, { id: t.id, box: z.box, strecken: ebeneVon(t.id) === 'leitung' ? z.strecken.filter((s) => s.art === 'achse').map((s) => ({ a: s.a, b: s.b, radius: s.radius })) : null });
    if (nahe <= GREIF_REICHWEITE) { o.auswaehlen(t.id); schreiben(true); return true; }   // antippen
    // weit entfernt: antippen und heranholen
    o.auswaehlen(t.id);
    if (ebeneVon(t.id) === 'leitung') {
      // die Leitung wird gewählt; zum Heranholen zählt die Querbewegung (Greifen mit gewählter Leitung)
    }
    fernHalt[seite] = true;
    greifenStart(seite, ort, { id: t.id, abstand: nahe, punkt: t.p, haelt: () => !!fernHalt[seite] });
    return true;
  }
  const fernHalt = { left: false, right: false };
  function selectEnd(c) {
    const i = o.controller().indexOf(c);
    const seite = o.quellen()[i]?.handedness ?? 'right';
    if (fernHalt[seite]) {
      fernHalt[seite] = false;
      if (grab?.art === 'fern' && grab.seite === seite) { loslassen(); return true; }
    }
    return false;
  }

  // ---------------------------------------------------------------------------------------------- Hände zeigen
  function haendeZeigen() {
    for (const seite of ['left', 'right']) {
      const S = seiten[seite];
      let m = handMesh[seite];
      if (!S?.gelenke) { if (m) m.visible = false; continue; }
      if (!m) {
        m = new THREE.InstancedMesh(new THREE.SphereGeometry(0.007, 8, 6), new THREE.MeshBasicMaterial({ color: 0x38e8ff, transparent: true, opacity: 0.75, depthTest: false }), GELENKE.length);
        m.frustumCulled = false; m.renderOrder = 997; wurzelGruppe.add(m); handMesh[seite] = m;
      }
      const mat = new THREE.Matrix4();
      GELENKE.forEach((n, i) => { const p = S.gelenke[n]; mat.makeTranslation(p ? p.x : 0, p ? p.y : -100, p ? p.z : 0); m.setMatrixAt(i, mat); });
      m.instanceMatrix.needsUpdate = true; m.visible = true;
    }
  }

  // ---------------------------------------------------------------------------------------------- Zustand für Prüfung
  function schreiben(erzwingen = false) {
    if (!erzwingen && uhr - letztesSchreiben < SCHREIB_MS / 1000) return;
    letztesSchreiben = uhr;
    o.wurzel.dataset.xr = JSON.stringify(zustand());
  }
  function zustand() {
    const s = (S) => (S ? { art: S.art, griff: S.griff, pinch: S.pinch, ort: pt(S.ort) } : null);
    return {
      links: s(seiten.left), rechts: s(seiten.right),
      tablet: { ...tablet.zustand(), lage: tabletLageJetzt ? { mitte: pt(tabletLageJetzt.mitte), normale: pt(tabletLageJetzt.normale), rechts: pt(tabletLageJetzt.rechts), oben: pt(tabletLageJetzt.oben) } : null, ursache: tabletUrsache },
      messen: messen.zustand(), messungen: messen.messungen().map((m) => ({ ...m, a: pt(m.a), b: pt(m.b), fuss: pt(m.fuss), abstandMm: rund(m.abstandMm, 1), dx: rund(m.dx, 1), dy: rund(m.dy, 1), dz: rund(m.dz, 1) })),
      greifen: grab ? { id: grab.id, kz: grab.kz, art: grab.art, offset: pt(grab.offset), oben: rund(grab.oben), seitlich: rund(grab.seitlich), masse: grab.masse ?? null, fang: grab.fang ?? null } : null,
      befehl: letztesGreifen, hinweis: hinweisZeit.text, fangdaten: daten.anzahl,
    };
  }

  // ---------------------------------------------------------------------------------------------- Bild
  return {
    api, daten, messen, tablet,
    anbinden() {
      const rig = o.rig();
      for (let i = 0; i < 2; i++) { try { const h = o.renderer.xr.getHand(i); if (h && !h.parent) rig.add(h); } catch { /* egal */ } }
    },
    /** Je Bild nach der Eingabe des Rigs. */
    bild(dt) {
      uhr += dt;
      const roh = (api.haende ?? rohStandard)() ?? {};
      const vor = { left: seiten.left?.griff, right: seiten.right?.griff };
      seiten = { left: auswerten('left', roh.left), right: auswerten('right', roh.right) };
      tabletPflegen(dt);
      sondePflegen();
      // Greifen: steigende Flanke der Faust bzw. der Grifftaste (Tablet-Hand ist offen); sinkende Flanke lässt los
      for (const seite of ['left', 'right']) {
        const S = seiten[seite];
        if (S?.griff && !vor[seite] && !grab && !messen.an && !o.sitzung?.aktiv) greifenStart(seite, S.ort);
        if (grab && grab.seite === seite && grab.art === 'nah' && S && !S.griff) loslassen();
        if (grab && grab.seite === seite && grab.art === 'nah' && !S) loslassen();
      }
      griffAktualisieren(dt);
      if (o.sitzung?.aktiv) o.sitzung.bild(dt, seiten);   // XR-H: Modell auf dem Tisch greifen, drehen, zoomen
      haendeZeigen();
      schreiben();
    },
    selectStart, selectEnd, zustand, schreiben,
    get aktiv() { return !!grab; },
    abbauen() {
      grab?.grp?.position.set(0, 0, 0);
      grab = null;
      massAnzeige?.freigeben(); massAnzeige = null;
      tablet.freigeben(); messen.freigeben();
      for (const m of Object.values(handMesh)) { m.geometry.dispose(); m.material.dispose(); }
      wurzelGruppe.removeFromParent();
      delete o.wurzel.dataset.xr;
    },
  };
}
