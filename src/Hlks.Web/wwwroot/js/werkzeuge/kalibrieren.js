// Werkzeug «Kalibrieren» (Register Einfügen, Gruppe Unterlagen): eine DWG/DXF-, PDF- oder Bild-Unterlage im Grundriss auf bekannte
// Punkte ausrichten. Ablauf: Unterlage wählen (Optionsleiste, vorbelegt mit der gewählten bzw. der einzigen; oder anklicken) →
// zwei Punkte auf der Unterlage anklicken (Fangen) → zwei Zielpunkte anklicken (Fangen) oder mit Enter Koordinaten eintippen.
// «Nur verschieben» braucht nur ein Punktpaar. Danach ein Befehl unterlage_kalibrieren; Faktor, Drehung und Verschiebung rechnet
// der Kern. Die Oberfläche rechnet nur die Klickpunkte aus Modell-mm in Unterlagen-Koordinaten zurück (kalibrierung.js).

import { element, derKlasse } from '../zustand.js';
import { zeigen } from '../oberflaeche/dialoge.js';
import { knoten } from '../oberflaeche/werkzeug.js';
import { setze } from './optionen.js';
import { zahlAusText } from './fangen.js';
import { WerkzeugFehler } from './parameter.js';
import { transformationVon, unterlageNachModell, modellNachUnterlage, kalibrierParameter, KALIBRIERBAR } from './kalibrierung.js';
import * as S from './schicht.js';

/** Unterlagen, die sich kalibrieren lassen (DWG, DXF, PDF, Bild). */
export const kalibrierbareUnterlagen = () => derKlasse('Unterlage').filter((u) => KALIBRIERBAR.has(u.werte?.art));

const MIT_PUNKTEN = ['endpunkt', 'schnittpunkt', 'mitte', 'lot', 'naechster'];   // auf der Unterlage kein Raster: genau treffen

/** Dialog «Zielpunkt eingeben» (Modell-mm). Gibt [x, y] zurück oder null. */
export async function koordinatenDialog(nummer, vorgabe = null) {
  const w = knoten(`<div>
    <label class="fz"><span>Ost x [mm]</span><input type="text" class="k-x" inputmode="decimal" value="${vorgabe ? vorgabe[0] : ''}"></label>
    <label class="fz"><span>Nord y [mm]</span><input type="text" class="k-y" inputmode="decimal" value="${vorgabe ? vorgabe[1] : ''}"></label>
    <p class="leise">Koordinaten des Zielpunkts ${nummer} im Projekt (Millimeter, x Ost, y Nord).</p></div>`);
  const x = w.querySelector('.k-x'), y = w.querySelector('.k-y');
  const lesen = () => [zahlAusText(x.value), zahlAusText(y.value)];
  const ok = await zeigen({
    titel: `Zielpunkt ${nummer} eingeben`, inhalt: w, breite: 380,
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'OK', wert: true, primaer: true, pruefen: () => (lesen().every((v) => v !== null) ? true : 'Bitte x und y als Zahlen eingeben.') }],
  });
  return ok === true ? lesen() : null;
}

/**
 * Das Werkzeug. param.unterlage = Kennzeichen der Unterlage (z. B. aus dem Kontextmenü des Projektbrowsers).
 * Phasen ergeben sich aus den gesammelten Punkten: von1, von2, nach1, nach2 (mit «nur verschieben» je nur 1).
 */
export function kalibrierenWerkzeug(param = {}) {
  const anzahl = (ctx) => (ctx.opt.nurVerschieben ? 1 : 2);
  const gewaehlt = (ctx) => { const u = element(ctx.opt.unterlage); return u?.klasse === 'Unterlage' ? u : null; };
  const phase = (z, ctx) => {
    const u = gewaehlt(ctx);
    if (!u) return 'wahl';
    if (z.fuer !== u.kennzeichen) { z.von = []; z.nach = []; z.fuer = u.kennzeichen; }   // andere Unterlage gewählt: von vorn
    const n = anzahl(ctx);
    if (z.von.length < n) return `von${z.von.length + 1}`;
    return `nach${z.nach.length + 1}`;
  };
  const zurueck = (z) => { z.von = []; z.nach = []; };
  let aktuell = null;   // Zustand des laufenden Werkzeugs (für fangArten und letzterPunkt, die ihn nicht übergeben bekommen)

  /** Unterlage unter dem Mauszeiger (Plan-Teil mit Id einer Unterlage) oder null. */
  const unterlageAmZeiger = (ctx, fang) => {
    const direkt = element(fang?.id);
    if (direkt?.klasse === 'Unterlage') return direkt;
    try {
      const b = ctx.maus && ctx.ansicht.zuBildschirm?.(ctx.maus);
      if (b && typeof document !== 'undefined') {
        for (const el of document.elementsFromPoint(b.x, b.y)) {
          const e = element(el.closest?.('[data-id]')?.getAttribute('data-id'));
          if (e?.klasse === 'Unterlage') return e;
        }
      }
    } catch { /* ohne Treffer weiter */ }
    return null;
  };

  const waehlen = (ctx, u) => { setze('kalibrieren', 'unterlage', u.kennzeichen); ctx.optionenNeu(); };

  const absenden = async (z, ctx) => {
    const u = gewaehlt(ctx);
    const n = anzahl(ctx);
    const p = kalibrierParameter({ unterlage: u.kennzeichen, von: z.von.slice(0, n), nach: z.nach.slice(0, n), nurVerschieben: !!ctx.opt.nurVerschieben });
    const r = await ctx.befehl('unterlage_kalibrieren', p);
    zurueck(z);
    if (r.angenommen) ctx.beenden();
  };

  const zielNehmen = async (z, ctx, punkt) => {
    z.nach.push([punkt[0], punkt[1]]);
    if (z.nach.length >= anzahl(ctx)) await absenden(z, ctx);
  };

  return {
    id: 'kalibrieren', titel: 'Kalibrieren', planarten: ['grundriss'],
    neu(ctx) {
      const z = { von: [], nach: [], fuer: null, ctx };
      aktuell = z;
      const kandidaten = kalibrierbareUnterlagen();
      const vorgabe = element(param.unterlage) ?? (element(ctx.opt.unterlage)?.klasse === 'Unterlage' ? element(ctx.opt.unterlage) : null);
      const imGeschoss = kandidaten.filter((u) => !u.geschoss || u.geschoss === ctx.geschoss);
      const wahl = (vorgabe && KALIBRIERBAR.has(vorgabe.werte?.art) ? vorgabe : null) ?? (imGeschoss.length === 1 ? imGeschoss[0] : kandidaten.length === 1 ? kandidaten[0] : null);
      setze('kalibrieren', 'unterlage', wahl ? wahl.kennzeichen : '');
      setze('kalibrieren', 'nurVerschieben', false);   // jedes Mal zwei Punktpaare, bis der Benutzer «Nur verschieben» wählt
      return z;
    },
    // Fangen: auf der Unterlage ohne Raster (genau auf Ecken und Kanten), bei den Zielpunkten mit Raster
    get fangArten() { return aktuell && phase(aktuell, aktuell.ctx).startsWith('nach') ? [...MIT_PUNKTEN, 'raster'] : MIT_PUNKTEN; },
    hinweis(z, ctx) {
      switch (phase(z, ctx)) {
        case 'wahl': return kalibrierbareUnterlagen().length === 0 ? 'Keine Unterlage zum Kalibrieren (DWG, DXF, PDF oder Bild einfügen).' : 'Unterlage in der Optionsleiste wählen oder anklicken.';
        case 'von1': return ctx.opt.nurVerschieben ? 'Punkt auf der Unterlage anklicken, der verschoben werden soll.' : 'Ersten Punkt auf der Unterlage anklicken.';
        case 'von2': return 'Zweiten Punkt auf der Unterlage anklicken (möglichst weit vom ersten).';
        case 'nach1': return `Zielpunkt für Punkt 1 anklicken oder Enter für Koordinaten.`;
        default: return 'Zielpunkt für Punkt 2 anklicken oder Enter für Koordinaten.';
      }
    },
    letzterPunkt(z) {
      const ph = phase(z, z.ctx);
      if (ph === 'von2' && z.von.length) { const u = gewaehlt(z.ctx), t = transformationVon(u); return t ? unterlageNachModell(t, u.werte.art, z.von[0]) : null; }
      if (ph === 'nach2') return z.nach[0] ?? null;
      return null;
    },
    eingabeMoeglich: () => false,
    async klick(z, fang, ctx) {
      const u = gewaehlt(ctx);
      if (!u) {
        const treffer = unterlageAmZeiger(ctx, fang);
        if (treffer && KALIBRIERBAR.has(treffer.werte?.art)) waehlen(ctx, treffer);
        else throw new WerkzeugFehler('Keine Unterlage gewählt.', 'In der Optionsleiste eine Unterlage wählen oder eine anklicken.');
        return;
      }
      const ph = phase(z, ctx);
      if (ph.startsWith('von')) {
        const t = transformationVon(u);
        if (!t) throw new WerkzeugFehler(`Die Unterlage ${u.kennzeichen} hat noch keine Lage.`, 'Die Unterlage zuerst über «Einfügen» anlegen; sie bekommt dabei ihre erste Lage.');
        const uv = modellNachUnterlage(t, u.werte.art, fang.punkt);
        if (z.von.length && Math.hypot(uv[0] - z.von[0][0], uv[1] - z.von[0][1]) < 1e-9) return;
        z.von.push(uv);
        return;
      }
      if (z.nach.length && Math.hypot(fang.punkt[0] - z.nach[0][0], fang.punkt[1] - z.nach[0][1]) < 0.5) return;
      await zielNehmen(z, ctx, fang.punkt);
    },
    async enter(z, ctx) {
      const u = gewaehlt(ctx);
      if (!u) return;
      if (phase(z, ctx).startsWith('von')) return;
      const nummer = z.nach.length + 1;
      const p = await koordinatenDialog(nummer);
      if (p) await zielNehmen(z, ctx, p);
    },
    zeichne(z, fang, ctx, s) {
      const u = gewaehlt(ctx);
      const t = transformationVon(u);
      if (!u || !t) return '';
      const art = u.werte.art;
      const k = 7 * s;
      let h = '';
      const quelle = z.von.map((uv) => unterlageNachModell(t, art, uv));
      quelle.forEach((q, i) => {
        h += S.linie([q[0] - k, q[1]], [q[0] + k, q[1]], 'wz-linie') + S.linie([q[0], q[1] - k], [q[0], q[1] + k], 'wz-linie');
        h += S.text(q, `${i + 1}`, s, { versatzPx: [9, 9], klasse: 'wz-text wz-masstext' });
      });
      z.nach.forEach((q, i) => {
        h += S.kreis(q, k, 'wz-linie') + S.text(q, `${i + 1}'`, s, { versatzPx: [9, 9], klasse: 'wz-text wz-masstext' });
        if (quelle[i]) h += S.linie(quelle[i], q, 'wz-gummi');
      });
      if (fang) {
        const ph = phase(z, ctx);
        if (ph === 'von2' && quelle[0]) h += S.linie(quelle[0], fang.punkt, 'wz-gummi');
        if (ph.startsWith('nach')) {
          const i = z.nach.length;
          if (quelle[i]) h += S.linie(quelle[i], fang.punkt, 'wz-gummi') + S.tempMass(quelle[i], fang.punkt, s, { mitWinkel: false });
        }
      }
      return h;
    },
  };
}
