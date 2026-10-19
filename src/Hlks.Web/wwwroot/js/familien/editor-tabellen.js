// Familieneditor (Modul 2, C1): die Seiten Parameter, Typen, Anschlüsse und Prüfen. Jede Seite liefert
//   zeichne(ctx) → HTML · aenderung(el, ctx) → { ops } | { fehler } | { eigen: async () => … } | null (Änderung eines Feldes)
//   klick(el, ctx) → { ops } | { eigen } | null (Knopf mit data-aktion)
// Alle Änderungen gehen als ops an familie_aendern (der Editor sendet; hier steht nur die Umrechnung Tabelle ↔ ops). Der Kern prüft jede
// Änderung im Trockenlauf – Fehler erscheinen rot mit seiner Meldung.

import { knoten } from '../oberflaeche/werkzeug.js';
import { zeigen, bestaetigen, eingabe } from '../oberflaeche/dialoge.js';
import {
  esc, ARTEN, DATENTYPEN, GRUPPEN, FLUESSE, ANSCHLUSSARTEN, FORMEN, NAME_MUSTER, klon, wertAnzeige, parameterMitFeld, parameterNeu, parameterPruefen,
  verwendungenVon, umbenennenOps, typSpalten, typZelleText, typMitWert, typKopie, typUmbenennenOps, freierName, anschlussNeu, anschlussMitFeld,
  anschlussGroesse, mediumAuswahl, pruefZeilen, feldText, kernFehler,
} from './familien-text.js';
import { eingabeFeld, auswahlFeld, knopf, tabelle, leerHinweis } from './editor-bausteine.js';

const einheitVon = (p) => (p.datentyp === 'zahl' ? p.einheit ?? '' : p.datentyp === 'laenge' ? 'mm' : p.datentyp === 'winkel' ? '°' : '');

// ================================================================================================ Parameter

function parameterZeilen(ctx) {
  const def = ctx.def;
  const erster = ctx.fam?.typen?.[0]?.werte ?? {};
  return (def.parameter ?? []).map((p) => {
    const n = p.name;
    const fk = (f) => `p:${n}:${f}`;
    const formel = p.formel !== undefined && p.formel !== null;
    const aufgeloest = formel && erster[n] !== undefined && erster[n] !== null ? `<span class="fe-aufgeloest" title="Wert für den ersten Typ">→ ${esc(feldText(Math.round(erster[n] * 1e6) / 1e6))}</span>` : '';
    return `<tr data-zeile="${esc(n)}">
      <td>${eingabeFeld(n, { fk: fk('name'), feld: 'name', titel: 'Name (Umbenennen führt Formeln, Körper und Typwerte nach)' })}</td>
      <td>${auswahlFeld(ARTEN, p.art, { fk: fk('art'), feld: 'art', titel: 'typ: je Typ fest · exemplar: je platziertes Element einstellbar' })}</td>
      <td>${auswahlFeld(DATENTYPEN, p.datentyp, { fk: fk('datentyp'), feld: 'datentyp' })}</td>
      <td>${eingabeFeld(p.datentyp === 'zahl' ? p.einheit ?? '' : '', { fk: fk('einheit'), feld: 'einheit', deaktiviert: p.datentyp !== 'zahl', platzhalter: einheitVon(p), klasse: 'schmal', titel: 'Einheit gilt nur für Zahl (Länge immer mm, Winkel immer Grad)' })}</td>
      <td>${auswahlFeld(GRUPPEN, p.gruppe ?? '', { fk: fk('gruppe'), feld: 'gruppe', leer: '–' })}</td>
      <td class="fe-wertzelle">${eingabeFeld(wertAnzeige(p), { fk: fk('eingabe'), feld: 'eingabe', live: true, klasse: formel ? 'formel' : '', titel: 'Vorgabewert, oder Formel mit «=» (bei Zahlen genügt es, wenn der Text keine Zahl ist). Funktionen: if, and, or, not, min, max, abs, sqrt, round, roundup, rounddown, sin, cos, tan, asin, acos, atan, pi()' })}${aufgeloest}</td>
      <td>${eingabeFeld(p.ziel ?? '', { fk: fk('ziel'), feld: 'ziel', titel: 'Eigenschaft der KICAD-Klasse, die der Wert am platzierten Element setzt (Herkunft familie), z. B. leistung' })}</td>
      <td>${eingabeFeld(p.beschreibung ?? '', { fk: fk('beschreibung'), feld: 'beschreibung', klasse: 'breit' })}</td>
      <td class="fe-k">${knopf('✕', 'parameter-loeschen', { titel: 'Parameter löschen', klasse: 'klein' })}</td></tr>`;
  });
}

export const parameterTab = {
  id: 'parameter',
  titel: 'Parameter',
  zeichne(ctx) {
    const n = (ctx.def.parameter ?? []).length;
    return `<div class="fe-werkzeuge">${knopf('＋ Parameter', 'parameter-neu', { titel: 'Parameter hinzufügen' })}<span class="leise">${n} Parameter · Formeln wie Revit: + − * / ^ , Vergleiche, if(b, a, c) …</span></div>`
      + (n === 0 ? leerHinweis('Noch kein Parameter. Mit «＋ Parameter» beginnen.')
        : tabelle(['Name', 'Art', 'Datentyp', 'Einheit', 'Gruppe', 'Wert oder Formel', 'Ziel', 'Beschreibung', ''], parameterZeilen(ctx)));
  },
  aenderung(el, ctx) {
    const zeile = el.closest('[data-zeile]');
    const p = (ctx.def.parameter ?? []).find((x) => x.name === zeile?.dataset.zeile);
    if (!p) return null;
    const feld = el.dataset.feld;
    if (feld === 'name') {
      const neu = el.value.trim();
      if (neu === p.name) return null;
      return { eigen: () => parameterUmbenennen(ctx, p.name, neu) };
    }
    return { ops: [{ op: 'parameter_setzen', parameter: parameterMitFeld(p, feld, el.value) }] };
  },
  klick(el, ctx) {
    if (el.dataset.aktion === 'parameter-neu') return { eigen: () => parameterDialog(ctx) };
    if (el.dataset.aktion === 'parameter-loeschen') {
      const name = el.closest('[data-zeile]')?.dataset.zeile;
      return { eigen: () => parameterLoeschen(ctx, name) };
    }
    return null;
  },
};

const verwendungsListe = (v) => `<ul class="fe-verw">${v.slice(0, 8).map((x) => `<li>${esc(x.ort)}</li>`).join('')}${v.length > 8 ? `<li>… und ${v.length - 8} weitere</li>` : ''}</ul>`;

/** Parameter löschen: wird er verwendet, fragt der Editor zuerst (mit dem Urteil des Kerns aus dem Trockenlauf). */
export async function parameterLoeschen(ctx, name) {
  if (!name) return;
  const op = { op: 'parameter_loeschen', name };
  const v = verwendungenVon(ctx.def, name);
  if (v.length === 0) { await ctx.senden([op]); return; }
  const r = await ctx.pruefen([op]);
  const k = r.angenommen ? null : kernFehler(r);
  const ok = await zeigen({
    titel: `Parameter «${name}» löschen`,
    breite: 520,
    inhalt: `<p class="dlg-text">«${esc(name)}» wird verwendet (${v.length}×):</p>${verwendungsListe(v)}${k
      ? `<p class="fehlertext">Der Kern lehnt das Löschen ab, solange der Parameter gebraucht wird: ${esc(k.text)}</p><p class="leise">Zuerst die Verwendungen ändern oder entfernen.</p>`
      : '<p class="dlg-text">Die Werte in den Typen gehen verloren.</p>'}`,
    knoepfe: k ? [{ text: 'OK', wert: false, primaer: true }] : [{ text: 'Abbrechen', wert: false }, { text: 'Löschen', wert: true, primaer: true, gefahr: true }],
  });
  if (ok === true) await ctx.senden([op]);
}

/** Parameter umbenennen: führt alle Verwendungen nach; wird er verwendet, fragt der Editor vorher. */
export async function parameterUmbenennen(ctx, alt, neu) {
  const u = umbenennenOps(ctx.def, alt, neu);
  if (u.fehler) { ctx.fehler(u.fehler); ctx.neuZeichnen(); return; }
  if (u.verwendungen.length > 0) {
    const r = await ctx.pruefen(u.ops);
    if (!r.angenommen) { ctx.fehlerKern(r); return; }
    const ok = await zeigen({
      titel: `Parameter «${alt}» umbenennen`, breite: 520,
      inhalt: `<p class="dlg-text">«${esc(alt)}» wird verwendet (${u.verwendungen.length}×). Beim Umbenennen in «${esc(neu)}» werden diese Stellen nachgeführt:</p>${verwendungsListe(u.verwendungen)}`,
      knoepfe: [{ text: 'Abbrechen', wert: false }, { text: 'Umbenennen', wert: true, primaer: true }],
    });
    if (ok !== true) { ctx.neuZeichnen(); return; }
  }
  await ctx.senden(u.ops);
}

/** Dialog «Neuer Parameter». */
export async function parameterDialog(ctx) {
  const w = knoten(`<div class="fe-dlg">
    <label class="fz"><span>Name *</span><input type="text" class="pn-name" data-kein-enter="1" placeholder="z. B. hoehe"></label>
    <label class="fz"><span>Art</span><select class="pn-art">${ARTEN.map(([v, t]) => `<option value="${v}">${t}</option>`).join('')}</select></label>
    <label class="fz"><span>Datentyp</span><select class="pn-datentyp">${DATENTYPEN.map(([v, t]) => `<option value="${v}">${t}</option>`).join('')}</select></label>
    <label class="fz"><span>Einheit (nur Zahl)</span><input type="text" class="pn-einheit" data-kein-enter="1" disabled placeholder="z. B. W"></label>
    <label class="fz"><span>Gruppe</span><select class="pn-gruppe">${GRUPPEN.map(([v, t]) => `<option value="${v}">${t}</option>`).join('')}</select></label>
    <label class="fz"><span>Wert oder Formel</span><input type="text" class="pn-eingabe" data-kein-enter="1" placeholder="Zahl, Text oder = Formel"></label>
    <label class="fz"><span>Ziel (Eigenschaft)</span><input type="text" class="pn-ziel" data-kein-enter="1" placeholder="z. B. leistung"></label>
    <label class="fz"><span>Beschreibung</span><input type="text" class="pn-beschreibung" data-kein-enter="1"></label>
    <div class="fe-dlg-pruefung leise"></div></div>`);
  const q = (s) => w.querySelector(s);
  const felder = () => ({ name: q('.pn-name').value, art: q('.pn-art').value, datentyp: q('.pn-datentyp').value, einheit: q('.pn-einheit').value, gruppe: q('.pn-gruppe').value, eingabe: q('.pn-eingabe').value, ziel: q('.pn-ziel').value, beschreibung: q('.pn-beschreibung').value });
  let nr = 0;
  const pruefung = q('.fe-dlg-pruefung');
  let timer = null;
  async function live() {
    const mein = ++nr;
    q('.pn-einheit').disabled = q('.pn-datentyp').value !== 'zahl';
    const f = felder();
    const lokal = parameterPruefen(ctx.def, f);
    if (lokal !== true) { pruefung.className = 'fe-dlg-pruefung leise'; pruefung.textContent = f.name.trim() ? lokal : ''; return; }
    const r = await ctx.pruefen([{ op: 'parameter_setzen', parameter: parameterNeu(f) }]);
    if (mein !== nr) return;
    if (r.angenommen) { pruefung.className = 'fe-dlg-pruefung ok'; pruefung.textContent = 'Der Kern nimmt den Parameter an.'; }
    else { const k = kernFehler(r); pruefung.className = 'fe-dlg-pruefung fehlertext'; pruefung.textContent = `${k.text}${k.vorschlag ? ' – ' + k.vorschlag : ''}`; }
  }
  w.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(live, 300); });
  w.addEventListener('change', () => { clearTimeout(timer); timer = setTimeout(live, 100); });
  await zeigen({
    titel: 'Neuer Parameter', inhalt: w, breite: 520, klasse: 'familie',
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Hinzufügen', primaer: true, schliesst: false,
        pruefen: () => parameterPruefen(ctx.def, felder()),
        async klick(c) {
          const r = await ctx.senden([{ op: 'parameter_setzen', parameter: parameterNeu(felder()) }], { still: true });
          if (!r.angenommen) { const k = kernFehler(r); c.fehler(`${k.text}${k.vorschlag ? ' – ' + k.vorschlag : ''}`); return; }
          c.schliessen(true);
        } }],
  });
  clearTimeout(timer); nr++;
}

// ================================================================================================ Typen

export const typenTab = {
  id: 'typen',
  titel: 'Typen',
  zeichne(ctx) {
    const def = ctx.def;
    const sp = typSpalten(def);
    const typen = def.typen ?? [];
    if (!ctx.auswahl.typ || !typen.some((t) => t.name === ctx.auswahl.typ)) ctx.auswahl.typ = typen[0]?.name ?? null;
    const kopf = ['Typ', ...sp.map((p) => ({ html: `${esc(p.name)}${einheitVon(p) ? ` <span class="leise">[${esc(einheitVon(p))}]</span>` : ''}`, attr: { title: p.beschreibung ?? p.name } })), ''];
    const zeilen = typen.map((t) => `<tr data-zeile="${esc(t.name)}" class="${t.name === ctx.auswahl.typ ? 'gewaehlt' : ''}">
      <td>${eingabeFeld(t.name, { fk: `t:${t.name}:name`, feld: 'typname', klasse: 'typname' })}</td>
      ${sp.map((p) => {
        const wert = t.werte?.[p.name];
        const fehlt = (wert === undefined || wert === null) && (p.wert === undefined || p.wert === null);
        const extra = { 'data-p': p.name };
        const fk = `t:${t.name}:${p.name}`;
        return `<td class="${fehlt ? 'fehlt' : ''}">${p.datentyp === 'ja_nein'
          ? auswahlFeld([['ja', 'ja'], ['nein', 'nein']], typZelleText(p, wert ?? p.wert), { fk, feld: 'wert', leer: '', extra, klasse: 'schmal' })
          : eingabeFeld(typZelleText(p, wert), { fk, feld: 'wert', extra, platzhalter: p.wert !== undefined && p.wert !== null ? typZelleText(p, p.wert) : '', klasse: 'zelle', titel: fehlt ? 'Wert fehlt – ohne Wert lässt sich der Typ nicht platzieren' : p.beschreibung })}</td>`;
      }).join('')}
      <td class="fe-k">${knopf('⧉', 'typ-kopieren', { titel: 'Typ kopieren', klasse: 'klein' })}${knopf('✕', 'typ-loeschen', { titel: 'Typ löschen', klasse: 'klein' })}</td></tr>`);
    return `<div class="fe-werkzeuge">${knopf('＋ Typ', 'typ-neu', { titel: 'Typ hinzufügen (leer, Vorgaben der Parameter)' })}${knopf('Typ kopieren', 'typ-kopieren-gewaehlt', { titel: 'Kopie des gewählten Typs' })}${knopf('Typ löschen', 'typ-loeschen-gewaehlt', { titel: 'Gewählten Typ löschen' })}`
      + `<span class="leise">${typen.length} Typ(en) · Spalten: Typparameter ohne Formel${sp.length === 0 ? ' – zuerst Typparameter anlegen' : ''}</span></div>`
      + tabelle(kopf, zeilen, { klasse: 'typkatalog' });
  },
  aenderung(el, ctx) {
    const zeile = el.closest('[data-zeile]');
    const typ = (ctx.def.typen ?? []).find((t) => t.name === zeile?.dataset.zeile);
    if (!typ) return null;
    ctx.auswahl.typ = typ.name;
    if (el.dataset.feld === 'typname') {
      const neu = el.value.trim();
      if (neu === typ.name) return null;
      if (!neu) return { fehler: 'Der Typname darf nicht leer sein.' };
      if ((ctx.def.typen ?? []).some((t) => t.name.toLowerCase() === neu.toLowerCase())) return { fehler: `Einen Typ «${neu}» gibt es schon.` };
      return { eigen: () => typUmbenennen(ctx, typ, neu) };
    }
    const p = typSpalten(ctx.def).find((x) => x.name === el.dataset.p);
    if (!p) return null;
    const r = typMitWert(typ, p, el.value);
    if (r.fehler) return { fehler: `${p.name}: ${r.fehler}` };
    return { ops: [{ op: 'typ_setzen', typ: r.typ }] };
  },
  klick(el, ctx) {
    const a = el.dataset.aktion;
    const zeile = el.closest('[data-zeile]')?.dataset.zeile ?? ctx.auswahl.typ;
    if (zeile) ctx.auswahl.typ = zeile;
    if (a === 'typ-neu') return { eigen: () => typNeu(ctx, false) };
    if (a === 'typ-kopieren' || a === 'typ-kopieren-gewaehlt') return { eigen: () => typNeu(ctx, true, zeile) };
    if (a === 'typ-loeschen' || a === 'typ-loeschen-gewaehlt') return { eigen: () => typLoeschen(ctx, zeile) };
    return null;
  },
};

/** Neuer Typ: leer (Vorgaben der Parameter) oder Kopie des Typs von. */
export async function typNeu(ctx, kopie, von) {
  const typen = ctx.def.typen ?? [];
  const quelle = kopie ? typen.find((t) => t.name === (von ?? ctx.auswahl.typ)) ?? typen.at(-1) : null;
  const vorschlag = freierName(quelle ? `${quelle.name} Kopie` : 'Typ', typen.map((t) => t.name));
  const name = await eingabe(kopie ? 'Typ kopieren' : 'Neuer Typ', 'Name des Typs', vorschlag);
  if (name === null) return;
  if (typen.some((t) => t.name.toLowerCase() === name.toLowerCase())) { ctx.fehler(`Einen Typ «${name}» gibt es schon.`); return; }
  const typ = quelle ? typKopie(quelle, name) : { name, werte: {} };
  ctx.auswahl.typ = name;
  await ctx.senden([{ op: 'typ_setzen', typ }]);
}

async function typLoeschen(ctx, name) {
  if (!name) return;
  const n = (ctx.fam?.verwendungen ?? []).length;
  if (n > 0) {
    const ok = await bestaetigen(`Der Typ «${name}» wird gelöscht. Die Familie ist an ${n} Element(en) platziert; platzierte Elemente dieses Typs verlieren ihren Typ.`, { ja: 'Löschen', gefahr: true, titel: 'Typ löschen' });
    if (!ok) return;
  }
  await ctx.senden([{ op: 'typ_loeschen', name }]);
}

async function typUmbenennen(ctx, typ, neu) {
  const n = (ctx.fam?.verwendungen ?? []).length;
  if (n > 0) {
    const ok = await bestaetigen(`Der Typ «${typ.name}» wird in «${neu}» umbenannt. Die Familie ist an ${n} Element(en) platziert; deren Typ muss danach neu gewählt werden.`, { ja: 'Umbenennen', titel: 'Typ umbenennen' });
    if (!ok) { ctx.neuZeichnen(); return; }
  }
  ctx.auswahl.typ = neu;
  await ctx.senden(typUmbenennenOps(typ, neu));
}

// ================================================================================================ Anschlüsse

function groessenZellen(a, fk) {
  const felder = anschlussGroesse(a);
  if (felder.length === 0) return '<span class="leise">–</span>';
  return felder.map((g) => `<label class="fe-mini">${g}${eingabeFeld(feldText(a[g]), { fk: fk(g), feld: g, live: true, klasse: 'schmal', platzhalter: g === 'dn' ? 'DN' : 'mm', titel: g === 'dn' ? 'Nennweite DN – nur aus der Unterlage' : 'Kanalmass in mm (Formel erlaubt)' })}</label>`).join('');
}

export const anschlussTab = {
  id: 'anschluesse',
  titel: 'Anschlüsse',
  zeichne(ctx) {
    const def = ctx.def;
    const medien = mediumAuswahl(ctx.medien(), def);
    const zeilen = (def.anschluesse ?? []).map((a) => {
      const n = a.name, fk = (f) => `a:${n}:${f}`;
      const lage = [0, 1, 2].map((i) => eingabeFeld(feldText(a.lage?.[i]), { fk: fk(`lage.${i}`), feld: `lage.${i}`, live: true, klasse: 'xyz', titel: `Lage ${'xyz'[i]} in mm (Formel erlaubt)` })).join('');
      const richt = [0, 1, 2].map((i) => eingabeFeld(feldText(a.richtung?.[i]), { fk: fk(`richtung.${i}`), feld: `richtung.${i}`, live: true, klasse: 'xyz', titel: `Richtung ${'xyz'[i]} aus dem Gerät heraus` })).join('');
      return `<tr data-zeile="${esc(n)}">
        <td>${eingabeFeld(n, { fk: fk('name'), feld: 'name', klasse: 'schmal' })}</td>
        <td>${auswahlFeld(medien, a.medium, { fk: fk('medium'), feld: 'medium' })}</td>
        <td>${auswahlFeld(FLUESSE, a.fluss, { fk: fk('fluss'), feld: 'fluss', klasse: 'schmal' })}</td>
        <td>${auswahlFeld(ANSCHLUSSARTEN, a.art ?? '', { fk: fk('art'), feld: 'art', leer: '–', klasse: 'schmal' })}</td>
        <td>${auswahlFeld(FORMEN, a.form ?? '', { fk: fk('form'), feld: 'form', leer: '–', klasse: 'schmal' })}</td>
        <td class="fe-gr">${groessenZellen(a, fk)}</td>
        <td>${eingabeFeld(a.gewinde ?? '', { fk: fk('gewinde'), feld: 'gewinde', klasse: 'schmal', platzhalter: 'z. B. Rp 1/2' })}</td>
        <td class="fe-xyz">${lage}</td><td class="fe-xyz">${richt}</td>
        <td>${eingabeFeld(a.beschreibung ?? '', { fk: fk('beschreibung'), feld: 'beschreibung' })}</td>
        <td class="fe-k">${knopf('✕', 'anschluss-loeschen', { titel: 'Anschluss löschen', klasse: 'klein' })}</td></tr>`;
    });
    const n = zeilen.length;
    return `<div class="fe-werkzeuge">${knopf('＋ Anschluss', 'anschluss-neu', { titel: 'Anschluss hinzufügen' })}<span class="leise">${n} Anschluss/Anschlüsse · Lage in Familienkoordinaten (Ursprung = Einfügepunkt), Richtung aus dem Gerät heraus; Grösse nur aus der Unterlage</span></div>`
      + (n === 0 ? leerHinweis('Noch kein Anschluss.')
        : tabelle(['Name', 'Medium', 'Fluss', 'Art', 'Form', 'Grösse', 'Gewinde', 'Lage x · y · z', 'Richtung x · y · z', 'Beschreibung', ''], zeilen, { klasse: 'anschlusstabelle' }));
  },
  aenderung(el, ctx) {
    const zeile = el.closest('[data-zeile]');
    const a = (ctx.def.anschluesse ?? []).find((x) => x.name === zeile?.dataset.zeile);
    if (!a) return null;
    const feld = el.dataset.feld;
    if (feld === 'name') {
      const neu = el.value.trim();
      if (neu === a.name) return null;
      if (!neu) return { fehler: 'Der Anschluss braucht einen Namen.' };
      if ((ctx.def.anschluesse ?? []).some((x) => x !== a && x.name.toLowerCase() === neu.toLowerCase())) return { fehler: `Einen Anschluss «${neu}» gibt es schon.` };
      return { ops: [{ op: 'anschluss_setzen', anschluss: { ...klon(a), name: neu } }, { op: 'anschluss_loeschen', name: a.name }] };
    }
    const r = anschlussMitFeld(a, feld, el.value);
    if (r.fehler) return { fehler: `${a.name}.${feld}: ${r.fehler}` };
    return { ops: [{ op: 'anschluss_setzen', anschluss: r.anschluss }] };
  },
  klick(el, ctx) {
    if (el.dataset.aktion === 'anschluss-neu') return { eigen: () => anschlussDialog(ctx) };
    if (el.dataset.aktion === 'anschluss-loeschen') {
      const name = el.closest('[data-zeile]')?.dataset.zeile;
      return { ops: [{ op: 'anschluss_loeschen', name }] };
    }
    return null;
  },
};

/** Dialog «Neuer Anschluss»: Name, Medium, Fluss, Art, Form; Lage und Richtung stehen danach in der Tabelle (Grösse nur aus der Unterlage). */
export async function anschlussDialog(ctx) {
  const medien = mediumAuswahl(ctx.medien(), ctx.def);
  const vorschlag = freierName('anschluss', (ctx.def.anschluesse ?? []).map((a) => a.name));
  const w = knoten(`<div class="fe-dlg">
    <label class="fz"><span>Name *</span><input type="text" class="an-name" data-kein-enter="1" value="${esc(vorschlag)}"></label>
    <label class="fz"><span>Medium</span><select class="an-medium">${medien.map(([v, t]) => `<option value="${esc(v)}">${esc(t)}</option>`).join('')}</select></label>
    <label class="fz"><span>Fluss</span><select class="an-fluss">${FLUESSE.map(([v, t]) => `<option value="${v}"${v === 'beide' ? ' selected' : ''}>${t}</option>`).join('')}</select></label>
    <label class="fz"><span>Art</span><select class="an-art">${ANSCHLUSSARTEN.map(([v, t]) => `<option value="${v}">${t}</option>`).join('')}</select></label>
    <label class="fz"><span>Form</span><select class="an-form">${FORMEN.map(([v, t]) => `<option value="${v}">${t}</option>`).join('')}</select></label>
    <p class="leise">Lage, Richtung und Grösse (DN, Ø, b × h) trägst du danach in der Tabelle ein; die Grösse nur aus der Unterlage.</p></div>`);
  const q = (s) => w.querySelector(s);
  await zeigen({
    titel: 'Neuer Anschluss', inhalt: w, breite: 460, klasse: 'familie',
    knoepfe: [{ text: 'Abbrechen', wert: false },
      { text: 'Hinzufügen', primaer: true, schliesst: false,
        pruefen: () => {
          const n = q('.an-name').value.trim();
          if (!n) return 'Name fehlt.';
          if ((ctx.def.anschluesse ?? []).some((a) => a.name.toLowerCase() === n.toLowerCase())) return `Einen Anschluss «${n}» gibt es schon.`;
          return true;
        },
        async klick(c) {
          const a = anschlussNeu({ name: q('.an-name').value, medium: q('.an-medium').value, fluss: q('.an-fluss').value, art: q('.an-art').value, form: q('.an-form').value });
          const r = await ctx.senden([{ op: 'anschluss_setzen', anschluss: a }], { still: true });
          if (!r.angenommen) { const k = kernFehler(r); c.fehler(`${k.text}${k.vorschlag ? ' – ' + k.vorschlag : ''}`); return; }
          c.schliessen(true);
        } }],
  });
}

// ================================================================================================ Prüfen

export const pruefenTab = {
  id: 'pruefen',
  titel: 'Prüfen',
  zeichne(ctx) {
    const z = pruefZeilen(ctx.fam);
    const kopf = `<div class="fe-werkzeuge">${knopf('Neu prüfen', 'pruefen-neu', { titel: 'Die Familie beim Kern neu abfragen' })}<span class="${z.length ? 'fehlertext' : 'huelle-ok'}">${z.length ? `${z.length} Meldung(en) des Kerns` : 'Keine Fehler: die Familie ist gültig und alle Typen lassen sich auflösen.'}</span></div>`;
    if (z.length === 0) return kopf + `<p class="fe-leer leise">Version ${esc(ctx.fam?.version ?? '')} · ${(ctx.fam?.typen ?? []).length} Typ(en) geprüft.</p>`;
    return kopf + tabelle(['Wo', 'Stelle', 'Meldung', 'Vorschlag'], z.map((e, i) => `<tr class="fe-pruefzeile${e.ziel ? ' klickbar' : ''}" data-i="${i}" data-aktion="springen" title="${e.ziel ? 'Klick springt zum Feld' : ''}">`
      + `<td>${esc(e.art)}</td><td class="mono">${esc(e.stelle)}</td><td class="fehlertext">${esc(e.meldung)}</td><td>${esc(e.vorschlag)}</td></tr>`), { klasse: 'pruefliste' });
  },
  aenderung() { return null; },
  klick(el, ctx) {
    if (el.dataset.aktion === 'pruefen-neu') return { eigen: () => ctx.neuLaden(true) };
    if (el.dataset.aktion === 'springen') {
      const z = pruefZeilen(ctx.fam)[Number(el.dataset.i)];
      if (z?.ziel) return { eigen: () => ctx.springen(z.ziel) };
    }
    return null;
  },
};

export { NAME_MUSTER };
