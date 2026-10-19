// Werkzeuge «Rohr» (PI) und «Kanal» (DT), Modul 3 Teil C1: Punkte in Fliessrichtung anklicken (Polylinie), Gummiband mit Länge, Zifferneingabe
// der Länge, Winkelfang nach den erlaubten Winkeln der Serie, Höhenwechsel (Steigleitung) per Eingabe «Höhe» in der Optionsleiste oder
// mit der Taste H, Anschlussfang in 3D-Lage (Name, Medium, Grösse, frei/belegt), Abzweig durch Klick auf eine bestehende Gerade.
// Enter oder Doppelklick schliesst ab: zuerst der Trockenlauf des Kerns (Vorschau-Zeile mit Längen und Formteilen oder die Ablehnung
// mit Vorschlag), dann «Ausführen». Das Werkzeug rechnet nichts Fachliches: Geraden, Bögen, Reduktionen, Einbaulängen und
// Verbindungen setzt der Kern (leitung_zeichnen, leitung_abzweigen); Parameter und Texte stehen in leitung-logik.js (in Node getestet).
// Tasten ausser den Ziffern (Basis: basis.js) verarbeitet ein eigener Hörer am Fenster: Esc verwirft zuerst die laufende Leitung,
// Rückschritt nimmt den letzten Punkt zurück, H springt in das Feld «Höhe».

import { element, alleElemente } from '../zustand.js';
import { sende } from '../oberflaeche/befehle.js';
import { meldung } from '../oberflaeche/dialoge.js';
import { hinweisSetzen } from '../oberflaeche/register.js';
import { abstand, punktNachLaenge } from './fangen.js';
import { setze, rasterWert } from './optionen.js';
import { familienLaden } from '../familien/daten.js';
import { esc } from './schicht.js';
import * as S from './schicht.js';
import * as L from './leitung-logik.js';
import * as D from './leitung-daten.js';

let laufend = null;      // { z, ctx, id, kategorie, leiste } des laufenden Werkzeugs

const kote = (ctx) => element(ctx.geschoss)?.werte?.kote ?? 0;
const hoeheZ = (ctx) => L.hoeheAbsolut(kote(ctx), ctx.opt.hoehe);
const plan = (p) => [p[0], p[1]];

// ------------------------------------------------------------------------------------------------ Vorschau-Zeile

function leisteErstellen(ctx) {
  const wurzel = ctx.ansicht.werkzeugSchicht?.ownerSVGElement?.parentElement;
  if (!wurzel) return null;
  const div = document.createElement('div');
  div.className = 'lt-leiste';
  div.hidden = true;
  div.addEventListener('mousedown', (e) => e.preventDefault());   // der Fokus bleibt, wo er ist (Tastatur gehört der Ansicht)
  div.addEventListener('click', (e) => {
    const b = e.target.closest('[data-lt]');
    if (!b || !laufend) return;
    const { z, ctx: c } = laufend;
    if (b.dataset.lt === 'liste') { div.querySelector('.lt-liste')?.toggleAttribute('hidden'); return; }
    if (b.dataset.lt === 'ausfuehren') c.lauf.run(() => ausfuehren(z, c));
    else if (b.dataset.lt === 'neu') c.lauf.run(() => vorschauHolen(z, c));
    else if (b.dataset.lt === 'zurueck') { z.vorschau = null; leisteAus(); c.lauf.hinweisAktualisieren(); c.neuZeichnen(); }
  });
  wurzel.appendChild(div);
  return div;
}

function leisteAus() { if (laufend?.leiste) { laufend.leiste.hidden = true; laufend.leiste.innerHTML = ''; } }

function leisteZeigen(v) {
  const div = laufend?.leiste;
  if (!div) return;
  if (v.ok) {
    const info = L.vorschauAus(v.r);
    const zeilen = L.vorschauListe(info);
    div.innerHTML = `<div class="lt-kopf"><span class="lt-titel">Vorschau</span><span class="lt-text">${esc(L.vorschauZeile(info))}</span>`
      + `<button type="button" class="lt-knopf" data-lt="liste" title="Einzelheiten ein-/ausblenden">▾</button>`
      + `<button type="button" class="lt-knopf lt-primaer" data-lt="ausfuehren">Ausführen (Enter)</button>`
      + `<button type="button" class="lt-knopf" data-lt="zurueck">Weiter zeichnen (Esc)</button></div>`
      + `<ul class="lt-liste" hidden>${zeilen.map((t) => `<li>${esc(t)}</li>`).join('')}</ul>`;
  } else {
    const a = L.ablehnung(v.r);
    div.innerHTML = `<div class="lt-kopf lt-abgelehnt"><span class="lt-titel lt-fehler">Abgelehnt</span><span class="lt-text">${esc(a.text)}</span>`
      + `<button type="button" class="lt-knopf" data-lt="neu" title="Noch einmal prüfen (Enter)">Neu prüfen</button>`
      + `<button type="button" class="lt-knopf" data-lt="zurueck">Weiter zeichnen (Esc)</button></div>`
      + (a.vorschlag ? `<div class="lt-vorschlag">Vorschlag: ${esc(a.vorschlag)}</div>` : '');
  }
  div.hidden = false;
}

// ------------------------------------------------------------------------------------------------ Zielpunkt und Winkelfang

/** Richtungen, auf die der Winkelfang einrastet (Grad), oder null, wenn nichts zu fangen ist. */
function kandidaten(z, ctx) {
  if (z.abzweig && z.punkte.length === 1) return L.abzweigRichtungen(z.abzweig.winkel);
  if (!ctx.opt.winkelfang) return null;
  const winkel = L.erlaubteWinkel(D.serieDef(ctx.opt.serie));
  const erster = z.von && z.punkte.length === 1 ? L.anschlussRichtungPlan(z.von) : null;
  if (z.von && z.punkte.length === 1 && erster === null) return null;      // senkrechter Anschluss: Höhenwechsel nötig
  return L.richtungsKandidaten({ vorher: L.letzteRichtung(z.punkte), winkel, erster });
}

/** Zielpunkt des Mauszeigers: Anschluss, sonst Winkelfang (wenn kein anderer Fang greift), sonst der Fang der Basis. */
function ziel(z, fang, ctx) {
  if (!fang) return null;
  if (fang.art === 'anschluss') return { punkt: fang.punkt, art: 'anschluss', anschluss: fang.anschluss };
  const letzter = z.punkte.at(-1);
  const stark = ['endpunkt', 'schnittpunkt', 'mitte', 'lot'].includes(fang.art);
  if (!letzter || fang.ortho || stark || !ctx.maus) return { punkt: fang.punkt, art: fang.art };
  const k = kandidaten(z, ctx);
  if (!k) return { punkt: fang.punkt, art: fang.art };
  const w = L.winkelFang({ letzter: plan(letzter), maus: ctx.maus, kandidaten: k, raster: rasterWert() });
  return w ? { punkt: w.punkt, art: 'winkel', winkel: w.winkel } : { punkt: fang.punkt, art: fang.art };
}

// ------------------------------------------------------------------------------------------------ Anschlüsse in der Nähe

function anschluesseNahe(ctx, pixel) {
  if (!ctx.maus) return [];
  return L.anschluesseNahe(D.alleAnschluesse(), ctx.maus, pixel * ctx.s());
}

function nachladen(ctx) {
  if (!ctx.maus) return;
  // Sind neue Anschlüsse da, wird der Fang unter dem Mauszeiger neu bestimmt (sonst erst bei der nächsten Mausbewegung)
  D.anschluesseLaden(D.elementeNahe(ctx.maus, 3000, ctx.geschoss), () => { if (ctx.lauf.fertig) return; ctx.lauf.berechneFang(); ctx.neuZeichnen(); });
}

// ------------------------------------------------------------------------------------------------ Befehle

function befehlBauen(z, ctx) {
  if (z.abzweig) {
    const pts = z.punkte.slice(1);
    const richtung = pts.length ? null : L.abzweigSeite(z.abzweig.winkel ?? 0, plan(z.abzweig.punkt), ctx.maus ?? plan(z.abzweig.punkt));
    return { name: 'leitung_abzweigen', parameter: L.abzweigParameter({ opt: ctx.opt, leitung: z.abzweig.leitung, punkt: z.abzweig.punkt, punkte: pts, richtung, nach: z.nach, geschoss: ctx.geschoss }) };
  }
  return { name: 'leitung_zeichnen', parameter: L.leitungParameter({ opt: ctx.opt, punkte: z.punkte, von: z.von, nach: z.nach, geschoss: ctx.geschoss }).parameter };
}

async function vorschauHolen(z, ctx) {
  let bau;
  try { bau = befehlBauen(z, ctx); } catch (e) { ctx.fehler(e); return; }
  const r = await sende(bau.name, bau.parameter, { trockenlauf: true });
  z.vorschau = { ok: !!r.angenommen, r, name: bau.name, parameter: bau.parameter };
  leisteZeigen(z.vorschau);
  hinweisSetzen(r.angenommen ? 'Vorschau des Kerns – Enter führt aus, Esc zeichnet weiter.' : 'Der Kern hat abgelehnt – Vorschlag lesen, Esc zeichnet weiter.');
  ctx.neuZeichnen();
}

function zuruecksetzen(z) {
  z.punkte = []; z.von = null; z.nach = null; z.abzweig = null; z.vorschau = null;
  leisteAus();
}

async function ausfuehren(z, ctx) {
  const v = z.vorschau;
  if (!v?.ok) { await vorschauHolen(z, ctx); return; }
  const info = L.vorschauAus(v.r);
  const r = await ctx.befehl(v.name, v.parameter, L.vorschauZeile(info));
  if (r.angenommen) {
    zuruecksetzen(z);
    const b = L.neueBefundeTexte(r);
    if (b.length) meldung(b.join(' · '), 'warnung');
  } else {
    z.vorschau = { ok: false, r };
    leisteZeigen(z.vorschau);
  }
}

// ------------------------------------------------------------------------------------------------ Start und Klicks

function pflichtPruefen(ctx, kategorie) {
  const pf = L.pflichtFehlt(ctx.opt, { kategorie, serien: D.serienListe(kategorie) });
  if (!pf) return true;
  ctx.pflicht(pf.feld, `${pf.text} ${pf.vorschlag}`);
  return false;
}

function anschlussBelegt(a) {
  meldung(`${a.element}.${a.name} ist schon verbunden (mit ${a.verbunden}).`, 'warnung', { vorschlag: 'Einen freien Anschluss wählen oder die Verbindung zuerst trennen.' });
}

function abzweigStarten(z, hit, maus, ctx, id) {
  const el = hit.element, pr = hit.projektion;
  const zH = hoeheZ(ctx);
  let q = pr.punkt;
  if (pr.senkrecht) {
    const z0 = el.geometrie.punkte[0][2] ?? 0, z1 = el.geometrie.punkte[1][2] ?? 0;
    if (!(zH > Math.min(z0, z1) && zH < Math.max(z0, z1))) {
      meldung(`${el.kennzeichen} steht senkrecht von ${Math.round(Math.min(z0, z1))} bis ${Math.round(Math.max(z0, z1))} mm.`, 'warnung', { vorschlag: 'In der Optionsleiste die Höhe des Abzweigs zwischen diese Werte legen (über OK Geschoss) und noch einmal klicken.' });
      return false;
    }
    q = [pr.punkt[0], pr.punkt[1], zH];
  }
  z.abzweig = { leitung: el.kennzeichen, winkel: L.leitungRichtungPlan(el), punkt: q };
  z.punkte = [q];
  // Der Abzweig übernimmt Serie, Grösse und Höhe der Leitung (änderbar in der Optionsleiste)
  if (el.bezuege?.familie) setze(id, 'serie', el.bezuege.familie);
  if (el.werte?.familientyp) setze(id, 'groesse', el.werte.familientyp);
  setze(id, 'hoehe', L.hoeheRelativ(kote(ctx), q[2]));
  ctx.optionenNeu();
  return true;
}

function leitungenImGeschoss(kategorie, ctx) {
  const g = element(ctx.geschoss);
  return alleElemente().filter((e) => e.klasse === kategorie && (!g || !e.geschoss || element(e.geschoss)?.id === g.id));
}

async function klick(z, fang, ctx, id, kategorie) {
  z.vorschau = null; leisteAus();
  if (!pflichtPruefen(ctx, kategorie)) return;
  const zl = fang.art === 'eingabe' ? { punkt: fang.punkt, art: 'eingabe' } : ziel(z, fang, ctx);
  if (!zl) return;
  const zH = hoeheZ(ctx);

  if (z.punkte.length === 0) {
    if (zl.anschluss) {
      const a = zl.anschluss;
      if (!L.anschlussFrei(a)) { anschlussBelegt(a); return; }
      z.von = a;
      z.punkte.push([a.lage[0], a.lage[1], a.lage[2]]);
      setze(id, 'hoehe', L.hoeheRelativ(kote(ctx), a.lage[2]));        // die Höhe des Anschlusses gilt
      ctx.optionenNeu();
      return;
    }
    if (ctx.maus) {
      const hit = L.leitungUnterMaus(ctx.maus, leitungenImGeschoss(kategorie, ctx), 8 * ctx.s(), kategorie);
      if (hit && abzweigStarten(z, hit, ctx.maus, ctx, id)) return;
      if (hit) return;
    }
    z.punkte.push([zl.punkt[0], zl.punkt[1], zH]);
    return;
  }

  const letzter = z.punkte.at(-1);
  // Wurde die Höhe geändert, ohne dass die Optionsleiste es schon gemeldet hat (Eingabe noch offen): zuerst das senkrechte Stück
  if (zH !== null && Math.abs(letzter[2] - zH) > 0.5 && !zl.anschluss) {
    z.punkte = L.hoehenWechsel(z.punkte, zH).punkte;
  }
  const l = z.punkte.at(-1);

  if (zl.anschluss) {
    const a = zl.anschluss;
    if (!L.anschlussFrei(a)) { anschlussBelegt(a); return; }
    if (z.von && z.von.element === a.element && z.von.name === a.name) return;
    if (abstand(plan(l), a.p) > 1) z.punkte.push([a.p[0], a.p[1], l[2]]);   // zuerst auf der eigenen Höhe heran, dann senkrecht an den Anschluss
    z.punkte.push([a.lage[0], a.lage[1], a.lage[2]]);
    z.nach = a;
    await vorschauHolen(z, ctx);                                            // am Anschluss endet die Leitung
    return;
  }
  const p = [zl.punkt[0], zl.punkt[1], zH ?? l[2]];
  if (abstand(plan(l), plan(p)) < 1 && Math.abs(l[2] - p[2]) < 0.5) return;
  z.punkte.push(p);
}

// ------------------------------------------------------------------------------------------------ Höhenwechsel und Tasten

function hoeheGeaendert() {
  const t = laufend;
  if (!t || t.ctx.lauf.fertig) return;
  const { z, ctx } = t;
  const zNeu = hoeheZ(ctx);
  if (zNeu === null || !z.punkte.length) return;
  const r = L.hoehenWechsel(z.punkte, zNeu);
  if (!r.angehaengt) return;
  z.punkte = r.punkte; z.vorschau = null; leisteAus();
  hinweisSetzen(`Steigleitung: senkrecht auf ${Math.round(ctx.opt.hoehe)} mm über OK Geschoss – nächsten Punkt anklicken.`, 4000);
  ctx.neuZeichnen();
}

function punktZurueck(t) {
  const { z, ctx } = t;
  const p = z.punkte.pop();
  if (z.nach && p && abstand(plan(p), z.nach.p) <= 1 && Math.abs(p[2] - z.nach.lage[2]) < 0.5) z.nach = null;
  if (z.punkte.length === 0) zuruecksetzen(z);
  z.vorschau = null; leisteAus();
  ctx.lauf.hinweisAktualisieren(); ctx.neuZeichnen();
}

const dialogOffen = () => (document.getElementById('dialogschicht')?.childElementCount ?? 0) > 0;
const inEingabe = (ziel) => ziel instanceof HTMLElement && (ziel.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(ziel.tagName));

if (typeof window !== 'undefined') {
  // Am Fenster (Aufnahmephase) – läuft vor den Hörern der Basis und der Ansicht
  window.addEventListener('keydown', (e) => {
    const t = laufend;
    if (!t || t.ctx.lauf.fertig || dialogOffen() || inEingabe(e.target) || e.ctrlKey || e.metaKey || e.altKey) return;
    const { z, ctx } = t;
    const stop = () => { e.preventDefault(); e.stopImmediatePropagation(); };
    if (e.key === 'Escape' && (z.punkte.length || z.vorschau)) {
      stop();
      if (z.vorschau && z.punkte.length) { z.vorschau = null; leisteAus(); ctx.lauf.hinweisAktualisieren(); ctx.neuZeichnen(); }
      else { zuruecksetzen(z); ctx.lauf.hinweisAktualisieren(); ctx.neuZeichnen(); }
    } else if (e.key === 'Backspace' && !ctx.lauf.tipp && z.punkte.length) {
      stop(); punktZurueck(t);
    } else if ((e.key === 'h' || e.key === 'H') && !e.shiftKey) {
      const f = document.querySelector(`#optionsleiste [data-feld="hoehe"]`);
      if (f) { stop(); f.focus(); f.select?.(); }
    }
  }, true);
}

// ------------------------------------------------------------------------------------------------ Werkzeug

export function leitungWerkzeug(kategorie = 'Rohr') {
  const id = kategorie === 'Kanal' ? 'kanal' : 'rohr';
  return {
    id, titel: kategorie === 'Kanal' ? 'Kanal' : 'Rohr', planarten: ['grundriss'], mehrpunkt: true,
    eingabeText: 'Länge',

    neu(ctx) {
      const z = { punkte: [], von: null, nach: null, abzweig: null, vorschau: null };
      laufend = { z, ctx, id, kategorie, leiste: leisteErstellen(ctx) };
      D.haken.hoeheGeaendert = hoeheGeaendert;
      D.haken.optionenNeu = () => ctx.optionenNeu();
      (async () => {
        await familienLaden();
        if (ctx.lauf.fertig) return;                                          // inzwischen beendet
        const o = ctx.opt;
        const k = L.optionenZustand({ serien: D.serienListe(kategorie), serie: o.serie, serieDef: D.serieDef(o.serie), groesse: o.groesse, systemKz: o.system, strangKz: o.strang, straenge: [] });
        for (const [feld, wert] of Object.entries(k.korrektur)) if (feld !== 'strang') setze(id, feld, wert);
        if (o.system && !element(o.system)) setze(id, 'system', '');          // Wert aus einem anderen Projekt
        if (o.strang && !element(o.strang)) setze(id, 'strang', '');
        if (ctx.opt.serie) await D.serieLaden(ctx.opt.serie);
        if (ctx.lauf.fertig) return;
        const def = D.serieDef(ctx.opt.serie);
        if (def && ctx.opt.groesse && !L.groessenListe(def).some((g) => g.typ === ctx.opt.groesse)) setze(id, 'groesse', '');
        ctx.optionenNeu();
      })();
      return z;
    },

    hinweis(z, ctx) {
      if (z.vorschau) return z.vorschau.ok ? 'Vorschau des Kerns – Enter führt aus, Esc zeichnet weiter.' : 'Der Kern hat abgelehnt – Vorschlag lesen, Esc zeichnet weiter.';
      if (z.punkte.length === 0) return 'Startpunkt anklicken – auf einem Anschluss beginnt die Leitung dort, auf einer Geraden entsteht ein Abzweig.';
      if (z.von && z.punkte.length === 1 && L.anschlussRichtungPlan(z.von) === null)
        return `${z.von.element}.${z.von.name} zeigt senkrecht ${(z.von.richtung?.[2] ?? 0) < 0 ? 'nach unten' : 'nach oben'}: zuerst die Höhe ändern (H), dann weiterzeichnen.`;
      if (z.abzweig) return 'Abzweig: Punkt rechtwinklig neben der Leitung anklicken, Zahl = Länge, Enter ohne weiteren Punkt setzt nur das T-Stück.';
      return 'Nächsten Punkt anklicken, Zahl = Länge in mm, H = Höhe ändern (Steigleitung), Rückschritt = Punkt zurück, Enter oder Doppelklick beendet, Umschalt = orthogonal.';
    },

    letzterPunkt: (z) => (z.punkte.length ? plan(z.punkte.at(-1)) : null),
    eingabeMoeglich: (z) => z.punkte.length > 0,

    extraPunkte(z) {
      const c = laufend?.ctx;
      const eigene = z.punkte.map(plan);
      if (!c) return eigene;
      return [...L.anschlussFangpunkte(anschluesseNahe(c, 60)), ...eigene];
    },

    bewegung(z, fang, ctx) { nachladen(ctx); },

    async klick(z, fang, ctx) { await klick(z, fang, ctx, id, kategorie); },

    async eingabe(z, zahl, fang, ctx) {
      const letzter = z.punkte.at(-1);
      if (!letzter || !fang) return;
      const zl = ziel(z, fang, ctx);
      await klick(z, { punkt: punktNachLaenge(plan(letzter), zl.punkt, zahl), art: 'eingabe', ortho: false }, ctx, id, kategorie);
    },

    async enter(z, ctx) {
      if (z.vorschau?.ok) { await ausfuehren(z, ctx); return; }
      if (z.punkte.length === 0) return;
      if (!z.abzweig && z.punkte.length < 2) { zuruecksetzen(z); return; }
      await vorschauHolen(z, ctx);
    },

    zeichne(z, fang, ctx, s) {
      const zl = ziel(z, fang, ctx);
      const breite = L.vorschauBreite(D.serieDef(ctx.opt.serie), ctx.opt.groesse) ?? 0;
      let h = '';
      const pts = z.punkte;
      if (z.abzweig) {
        const el = element(z.abzweig.leitung);
        const g = el?.geometrie?.punkte;
        if (g?.length >= 2) h += S.linie(plan(g[0]), plan(g.at(-1)), 'wz-vorschau wz-offen');
      }
      for (let i = 0; i + 1 < pts.length; i++) {
        const a = plan(pts[i]), b = plan(pts[i + 1]);
        if (abstand(a, b) > 1) { if (breite > 0) h += S.band(a, b, breite); h += S.linie(a, b, 'wz-linie'); }
        else h += S.kreis(a, Math.max(breite / 2, 4 * s), 'wz-ring');             // senkrechtes Stück (Steigleitung) als Ring
      }
      if (pts.length) {
        const l = plan(pts.at(-1));
        if (zl && !z.vorschau) {
          const b = zl.punkt;
          if (abstand(l, b) > 0.5) {
            if (breite > 0) h += S.band(l, b, breite);
            h += S.linie(l, b, 'wz-gummi');
            h += S.tempMass(l, b, s);
          }
        }
        h += S.kreis(plan(pts[0]), 3 * s, 'wz-ring');
      }
      // Anschlüsse in der Nähe des Mauszeigers: frei (offen) oder belegt (grau), der gefangene mit Beschriftung
      const nah = anschluesseNahe(ctx, 50);
      const gefangen = zl?.anschluss ?? null;
      for (const a of nah) h += S.kreis(a.p, (gefangen && gefangen.element === a.element && gefangen.name === a.name ? 6 : 4) * s, a.verbunden ? 'wz-anschluss wz-anschluss-belegt' : 'wz-anschluss');
      if (gefangen) h += S.text(gefangen.p, L.anschlussText(gefangen), s, { versatzPx: [12, 14], klasse: 'wz-text wz-fangtext' });
      else if (zl?.art === 'winkel' && pts.length) h += S.text(zl.punkt, `${Math.round(zl.winkel * 10) / 10}°`, s, { versatzPx: [12, -16], klasse: 'wz-text wz-masstext' });
      return h;
    },

    ende() {
      laufend?.leiste?.remove();
      laufend = null;
      D.haken.hoeheGeaendert = null;
      D.haken.optionenNeu = null;
    },
  };
}
