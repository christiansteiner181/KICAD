// Werkzeug «Familie platzieren» (Modul 2, C1): wie «Komponente platzieren» in Revit. Typauswahl «Familie: Typ», Drehung und Exemplarparameter
// stehen in der Optionsleiste (Felder aus typauswahl.js über optionen.js). Klick im Grundriss setzt den Einfügepunkt (mit Fangen, Raster),
// die Leertaste dreht um 90°, die Drehung geht auch als Zahl in der Optionsleiste. Danach der Befehl familie_platzieren; es bleibt aktiv,
// bis Esc oder Rechtsklick beendet. Die Vorschau am Mauszeiger zeigt den Umriss von oben (Abfrage familie_ansicht oben, Detailgrad mittel),
// gedreht und verschoben (familien-ansicht.js). Das Werkzeug rechnet nichts: Lage, Anschlüsse und Kennwerte ergibt der Kern.

import { abfrage } from '../api.js';
import { setze, wert } from '../werkzeuge/optionen.js';
import { modusSetzen, modus } from '../oberflaeche/register.js';
import { familienLaden, familienListe } from './daten.js';
import { typTeile, typWert, definitionLaden, definitionAus, platzierParameter, exemplarWerte, haken } from './typauswahl.js';
import { umrissPrimitive, vorschauSvg, drehungWeiter, drehungNormieren } from './familien-ansicht.js';

/** Nach einer Wahl in der Optionsleiste: Definition holen (Felder der Exemplarparameter) und die Leiste neu zeichnen. */
export async function typGewaehlt() {
  await definitionLaden(typTeile(wert('familie', 'typ'))?.familie ?? '');
  if (modus.id === 'werkzeug' && modus.daten.werkzeug === 'familie') modusSetzen('werkzeug', { ...modus.daten });
}

haken.typGewaehlt = () => typGewaehlt();

/** Setzt die Typauswahl (Familie und Typ) für den nächsten Start. */
export function typSetzen(kz, typ) { setze('familie', 'typ', typWert(kz, typ)); }

/** Erster platzierbarer Typ (Vorgabe, wenn nichts gewählt ist oder die Wahl nicht mehr stimmt). */
export function ersterTyp(liste = familienListe()) {
  const f = liste.find((x) => x.gueltig !== false && (x.typen ?? []).length > 0);
  return f ? typWert(f.kennzeichen, f.typen[0]) : '';
}

export function platzierWerkzeug() {
  return {
    id: 'familie',
    titel: 'Familie platzieren',
    planarten: ['grundriss'],
    neu(ctx) {
      const z = { punkt: null, vorschau: null, vorschauSchluessel: '', letzterTyp: '' };
      (async () => {
        await familienLaden();
        const liste = familienListe();
        const t = typTeile(ctx.opt.typ);
        if (!(t && liste.some((f) => f.kennzeichen === t.familie && (f.typen ?? []).includes(t.typ)))) {
          const e = typTeile(ersterTyp(liste));
          if (e) typSetzen(e.familie, e.typ);
        }
        await typGewaehlt();
        z.letzterTyp = ctx.opt.typ;
        vorschauLaden(z, ctx);
      })();
      return z;
    },
    hinweis(z, ctx) {
      const t = typTeile(ctx.opt.typ);
      return `${t ? `${t.familie} «${t.typ}»` : 'Typ in der Optionsleiste wählen'} · Klick setzt den Einfügepunkt · Leertaste dreht um 90° (jetzt ${drehungNormieren(ctx.opt.drehung ?? 0)}°)`;
    },
    bewegung(z, fang, ctx) {
      z.punkt = fang?.punkt ?? z.punkt;
      if (ctx.opt.typ !== z.letzterTyp) { z.letzterTyp = ctx.opt.typ; vorschauLaden(z, ctx); typGewaehlt(); }
    },
    leertaste(z, shift, ctx) {
      setze('familie', 'drehung', drehungWeiter(ctx.opt.drehung ?? 0, shift));
      ctx.optionenNeu();
    },
    async klick(z, fang, ctx) {
      const t = typTeile(ctx.opt.typ);
      if (!t) { ctx.pflicht('typ', 'Zuerst einen Typ wählen (Optionsleiste «Typ»).'); return; }
      const fam = definitionAus(t.familie);
      const p = platzierParameter(ctx.opt, fang.punkt, fam, ctx.geschoss);
      const r = await ctx.befehl('familie_platzieren', p, `${fam?.name ?? t.familie} «${t.typ}» platziert.`);
      if (r?.angenommen) z.punkt = fang.punkt;
    },
    zeichne(z, fang, ctx, s) {
      const punkt = fang?.punkt ?? z.punkt;
      if (!punkt) return '';
      return vorschauSvg(z.vorschau, drehungNormieren(ctx.opt.drehung ?? 0), punkt, s);
    },
    ende() { /* nichts aufzuräumen */ },
  };
}

async function vorschauLaden(z, ctx) {
  const t = typTeile(ctx.opt.typ);
  if (!t) { z.vorschau = null; return; }
  const fam = definitionAus(t.familie);
  const ex = fam ? exemplarWerte(fam, ctx.opt) : {};
  const schluessel = JSON.stringify([t, ex]);
  if (schluessel === z.vorschauSchluessel) return;
  z.vorschauSchluessel = schluessel;
  try {
    const a = await abfrage('familie_ansicht', { familie: t.familie, typ: t.typ, ansicht: 'oben', detailgrad: 'mittel', ...(Object.keys(ex).length ? { parameter: ex } : {}) });
    if (z.vorschauSchluessel !== schluessel) return;
    z.vorschau = umrissPrimitive(a);
  } catch (e) {
    z.vorschau = null;
    console.warn('Vorschau der Familie', e?.message ?? e);
  }
  ctx.neuZeichnen();
}
