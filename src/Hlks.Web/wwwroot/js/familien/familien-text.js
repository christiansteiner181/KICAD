// Familieneditor (Modul 2, C1): reine Logik der Tabellen und Felder – ohne DOM, in Node getestet (familien.test.mjs).
// Der Editor rechnet nichts Fachliches: er macht aus Eingaben Zahl, Formel-Text oder Wert und aus Tabellenzeilen die ops für den
// Befehl familie_aendern; ob eine Formel gilt, sagt der Kern (Trockenlauf). Die Formelprüfung hier beschränkt sich auf das Erkennen
// von Namen (für «wird verwendet» und Umbenennen) – ausgewertet wird nie.
//
// Annahmen (Prüfliste):
//  - Eine Eingabe, die keine Zahl ist, gilt bei Zahlfeldern als Formel; bei Text- und Medium-Parametern ist ein führendes «=» der Formel-Hinweis.
//  - Neue Körper und Konturen bekommen Platzhaltermasse (Würfel 100 mm usw.), damit der Entwurf gültig bleibt; sie sind keine Herstellerwerte.
//  - Umbenennen eines Parameters ersetzt den Namen in allen Formeln, Körpern, Anschlüssen, Sinnbild und Typwerten (ganze Namen, nicht in Texten).

export const esc = (t) => String(t ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
export const klon = (x) => (x === undefined ? undefined : JSON.parse(JSON.stringify(x)));

export const ARTEN = [['typ', 'Typ'], ['exemplar', 'Exemplar']];
export const DATENTYPEN = [['laenge', 'Länge'], ['winkel', 'Winkel'], ['zahl', 'Zahl'], ['text', 'Text'], ['ja_nein', 'Ja/Nein'], ['medium', 'Medium'], ['dn', 'DN']];
export const GRUPPEN = [['abmessung', 'Abmessung'], ['anschluss', 'Anschluss'], ['kennwert', 'Kennwert'], ['sonstiges', 'Sonstiges']];
export const DETAILGRADE = ['grob', 'mittel', 'fein'];
export const MATERIALIEN = [['geraet', 'Gerät'], ['apparat', 'Apparat'], ['armatur', 'Armatur'], ['rohr', 'Rohr'], ['sonst', 'Sonstiges']];
export const FLUESSE = [['rein', 'rein'], ['raus', 'raus'], ['beide', 'beide']];
export const ANSCHLUSSARTEN = [['rohr', 'Rohr'], ['kanal', 'Kanal'], ['elektro', 'Elektro']];
export const FORMEN = [['rund', 'rund'], ['rechteck', 'rechteckig']];
export const NAME_MUSTER = /^[A-Za-zÄÖÜäöü_][A-Za-zÄÖÜäöü0-9_]*$/;
const TEXT_TYPEN = new Set(['text', 'medium']);
const WAHR = new Set(['ja', 'wahr', 'true', '1']);
const FALSCH = new Set(['nein', 'falsch', 'false', '0']);

// ------------------------------------------------------------------------------------------------ Zahlen und Formeln

/** «1,5» / «1.5» / « 12 » → Zahl; sonst null. */
export function zahlLesen(text) {
  const t = String(text ?? '').trim().replace(',', '.');
  if (t === '' || !/^[-+]?(\d+(\.\d*)?|\.\d+)(e[-+]?\d+)?$/i.test(t)) return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

/** Eingabe eines Zahlfeldes: leer → undefined, Zahl → number, sonst Formel-Text (ohne führendes «=»). */
export function zahlOderFormel(text) {
  const t = String(text ?? '').trim();
  if (t === '') return undefined;
  const z = zahlLesen(t);
  if (z !== null && !t.startsWith('=')) return z;
  return t.startsWith('=') ? t.slice(1).trim() : t;
}

/** Anzeige eines Zahlfeldes: Zahl (höchstens 6 Nachkommastellen) oder Formel-Text. */
export function feldText(v) {
  if (v === undefined || v === null) return '';
  if (typeof v === 'number') return String(Math.round(v * 1e6) / 1e6);
  if (typeof v === 'boolean') return v ? 'ja' : 'nein';
  return String(v);
}

/** Eingabe für den Wert/Formel-Spalte eines Parameters → { leer } | { wert } | { formel }. */
export function wertEingabe(datentyp, text) {
  const t = String(text ?? '').trim();
  if (t === '') return { leer: true };
  if (t.startsWith('=')) return { formel: t.slice(1).trim() };
  if (TEXT_TYPEN.has(datentyp)) return { wert: t };
  if (datentyp === 'ja_nein') {
    const k = t.toLowerCase();
    if (WAHR.has(k)) return { wert: true };
    if (FALSCH.has(k)) return { wert: false };
    return { formel: t };
  }
  const z = zahlLesen(t);
  return z !== null ? { wert: z } : { formel: t };
}

/** Anzeige der Wert/Formel-Spalte: «= bodenabstand + hoehe» oder der Vorgabewert. */
export function wertAnzeige(p) {
  if (p?.formel !== undefined && p.formel !== null) return `= ${p.formel}`;
  if (p?.wert === undefined || p.wert === null) return '';
  return feldText(p.wert);
}

/** Namen (Bezeichner) in einer Formel, ohne Texte in Anführungszeichen und ohne Funktionsnamen (gefolgt von «(»). */
export function bezeichner(formel) {
  const aus = [];
  const re = /"[^"]*"|([A-Za-zÄÖÜäöü_][A-Za-zÄÖÜäöü0-9_]*)(\s*\()?/g;
  let m;
  while ((m = re.exec(String(formel ?? ''))) !== null) if (m[1] && !m[2]) aus.push(m[1]);
  return aus;
}

/** Ersetzt den ganzen Namen alt durch neu in einer Formel (nicht in Texten, nicht in Funktionsnamen). */
export function nameErsetzen(formel, alt, neu) {
  return String(formel).replace(/"[^"]*"|([A-Za-zÄÖÜäöü_][A-Za-zÄÖÜäöü0-9_]*)(\s*\()?/g, (all, name, klammer) => (name === alt && !klammer ? neu : all));
}

// ------------------------------------------------------------------------------------------------ Stellen mit Formeln

const KOERPER_OHNE = new Set(['id', 'name', 'art', 'detailgrad', 'material', 'quellen', 'ebene']);
const SINNBILD_FELDER = ['punkte', 'radius', 'von', 'bis', 'sichtbar'];
const ANSCHLUSS_FELDER = ['dn', 'd', 'b', 'h', 'lage', 'richtung', 'sichtbar'];

/** Läuft durch einen Wert und ruft fn(text, setzen) für jede Zeichenkette (Formel) auf; setzen(neu) ersetzt sie im Baum. */
function textstellen(wert, fn) {
  if (Array.isArray(wert)) {
    wert.forEach((x, i) => { if (typeof x === 'string') fn(x, (n) => { wert[i] = n; }); else textstellen(x, fn); });
  } else if (wert && typeof wert === 'object') {
    for (const k of Object.keys(wert)) {
      if (typeof wert[k] === 'string') fn(wert[k], (n) => { wert[k] = n; }); else textstellen(wert[k], fn);
    }
  }
}

/** Alle Stellen der Definition, die eine Formel tragen können: { ort, tab, name, feld, text, setzen(neu) } (setzen wirkt auf eine Kopie). */
export function formelStellen(def) {
  const aus = [];
  const nimm = (tab, name, feld, ort) => (text, setzen) => aus.push({ ort: `${ort}.${feld}`, tab, name, feld, text, setzen });
  for (const p of def?.parameter ?? []) if (typeof p.formel === 'string') aus.push({ ort: `Parameter «${p.name}»`, tab: 'parameter', name: p.name, feld: 'formel', text: p.formel, setzen: (n) => { p.formel = n; } });
  for (const k of def?.koerper ?? []) {
    for (const feld of Object.keys(k)) {
      if (KOERPER_OHNE.has(feld)) continue;
      const f = nimm('koerper', k.id, feld, `Körper «${k.id}»`);
      if (feld === 'achse') { if (k.achse?.punkt) textstellen(k.achse.punkt, nimm('koerper', k.id, 'achse.punkt', `Körper «${k.id}»`)); continue; }
      if (typeof k[feld] === 'string') f(k[feld], (n) => { k[feld] = n; }); else textstellen(k[feld], f);
    }
    if (typeof k.sichtbar === 'string') { /* durch obige Schleife erfasst */ }
  }
  for (const a of def?.anschluesse ?? []) {
    for (const feld of ANSCHLUSS_FELDER) {
      if (a[feld] === undefined) continue;
      const f = nimm('anschluesse', a.name, feld, `Anschluss «${a.name}»`);
      if (typeof a[feld] === 'string') f(a[feld], (n) => { a[feld] = n; }); else textstellen(a[feld], f);
    }
  }
  (def?.sinnbild ?? []).forEach((s, i) => {
    for (const feld of SINNBILD_FELDER) {
      if (s[feld] === undefined) continue;
      const f = nimm('sinnbild', String(i + 1), feld, `Sinnbild ${i + 1}`);
      if (typeof s[feld] === 'string') f(s[feld], (n) => { s[feld] = n; }); else textstellen(s[feld], f);
    }
  });
  (def?.masse ?? []).forEach((m) => {
    for (const feld of ['von', 'bis']) if (m[feld] !== undefined) textstellen(m[feld], nimm('masse', m.name, feld, `Mass «${m.name}»`));
  });
  return aus;
}

/** Wo wird der Parameter name verwendet? [{ ort, tab, name, feld }] – Formeln, Körper, Anschlüsse, Sinnbild, Medium-Anschlüsse, Typwerte. */
export function verwendungenVon(def, name) {
  const aus = [];
  const kopie = klon(def) ?? {};
  for (const s of formelStellen(kopie)) {
    if (s.tab === 'parameter' && s.name === name) continue;   // die eigene Formel zählt nicht mit (sonst wäre er zirkulär)
    if (bezeichner(s.text).includes(name)) aus.push({ ort: s.ort, tab: s.tab, name: s.name, feld: s.feld });
  }
  for (const a of def?.anschluesse ?? []) if (a.medium === name) aus.push({ ort: `Anschluss «${a.name}».medium`, tab: 'anschluesse', name: a.name, feld: 'medium' });
  for (const t of def?.typen ?? []) if (t.werte && Object.hasOwn(t.werte, name)) aus.push({ ort: `Typ «${t.name}»: Wert`, tab: 'typen', name: t.name, feld: name });
  return aus;
}

/** Nur die Verwendungen, die das Löschen verhindern würden (Formeln, Körper, Anschlüsse, Sinnbild – nicht die Typwerte). */
export const blockierendeVerwendungen = (v) => v.filter((x) => x.tab !== 'typen');

/** ops zum Umbenennen eines Parameters inklusive aller Verwendungen. Ergebnis { ops, verwendungen } oder { fehler }. */
export function umbenennenOps(def, alt, neu) {
  neu = String(neu ?? '').trim();
  if (!NAME_MUSTER.test(neu)) return { fehler: `«${neu}» ist kein gültiger Parametername (Buchstaben, Ziffern, _; nicht mit einer Ziffer beginnen).` };
  if ((def.parameter ?? []).some((p) => p.name === neu)) return { fehler: `Einen Parameter «${neu}» gibt es schon.` };
  const p = (def.parameter ?? []).find((x) => x.name === alt);
  if (!p) return { fehler: `Parameter «${alt}» gibt es nicht.` };
  const verw = verwendungenVon(def, alt);
  const kopie = klon(def);
  for (const s of formelStellen(kopie)) if (bezeichner(s.text).includes(alt)) s.setzen(nameErsetzen(s.text, alt, neu));
  for (const a of kopie.anschluesse ?? []) if (a.medium === alt) a.medium = neu;
  const ops = [{ op: 'parameter_setzen', parameter: { ...klon(kopie.parameter.find((x) => x.name === alt)), name: neu } }];
  for (const q of kopie.parameter) if (q.name !== alt && JSON.stringify(q) !== JSON.stringify(def.parameter.find((x) => x.name === q.name))) ops.push({ op: 'parameter_setzen', parameter: q });
  for (const t of kopie.typen ?? []) {
    const orig = def.typen.find((x) => x.name === t.name);
    const hat = t.werte && Object.hasOwn(t.werte, alt), hatQ = t.quellen && Object.hasOwn(t.quellen, alt);
    if (hat || hatQ) {
      if (hat) { t.werte[neu] = t.werte[alt]; delete t.werte[alt]; }
      if (hatQ) { t.quellen[neu] = t.quellen[alt]; delete t.quellen[alt]; }
      ops.push({ op: 'typ_setzen', typ: t });
    } else if (JSON.stringify(t) !== JSON.stringify(orig)) ops.push({ op: 'typ_setzen', typ: t });
  }
  for (const k of kopie.koerper ?? []) { const o = def.koerper.find((x) => x.id === k.id); if (JSON.stringify(k) !== JSON.stringify(o)) ops.push({ op: 'koerper_setzen', koerper: k }); }
  for (const a of kopie.anschluesse ?? []) { const o = def.anschluesse.find((x) => x.name === a.name); if (JSON.stringify(a) !== JSON.stringify(o)) ops.push({ op: 'anschluss_setzen', anschluss: a }); }
  if (JSON.stringify(kopie.sinnbild ?? null) !== JSON.stringify(def.sinnbild ?? null)) ops.push({ op: 'sinnbild_setzen', sinnbild: kopie.sinnbild ?? null });
  // Neuer Name steht schon da (parameter_setzen mit neuem Namen hängt an); der alte wird zuletzt gelöscht (löscht auch seine Typwerte)
  ops.push({ op: 'parameter_loeschen', name: alt });
  return { ops, verwendungen: verw };
}

// ------------------------------------------------------------------------------------------------ Parameter

/** Parameterobjekt ohne leere Felder, in der Reihenfolge des Schemas. */
export function parameterSauber(p) {
  const o = {};
  for (const k of ['name', 'art', 'datentyp', 'einheit', 'gruppe', 'wert', 'formel', 'ziel', 'beschreibung', 'quellen']) {
    let v = p[k];
    if (typeof v === 'string') v = v.trim();
    if (v === undefined || v === null || v === '') continue;
    if (k === 'wert' && p.formel !== undefined && p.formel !== null && p.formel !== '') continue;   // Formel geht vor
    if (k === 'einheit' && p.datentyp !== 'zahl') continue;   // Einheit gilt nur für Zahl (laenge immer mm, winkel immer Grad)
    o[k] = v;
  }
  return o;
}

/** Neuer Parameter aus den Feldern des Dialogs. */
export function parameterNeu({ name, art = 'typ', datentyp = 'laenge', einheit = '', gruppe = 'sonstiges', eingabe = '', ziel = '', beschreibung = '' }) {
  const p = { name: String(name ?? '').trim(), art, datentyp, einheit, gruppe, ziel, beschreibung };
  const e = wertEingabe(datentyp, eingabe);
  if (e.wert !== undefined) p.wert = e.wert;
  if (e.formel !== undefined) p.formel = e.formel;
  return parameterSauber(p);
}

/** Prüfung für den Dialog «Neuer Parameter»; true oder Fehlertext. */
export function parameterPruefen(def, felder) {
  const name = String(felder.name ?? '').trim();
  if (name === '') return 'Name fehlt.';
  if (!NAME_MUSTER.test(name)) return `«${name}» ist kein gültiger Parametername (Buchstaben, Ziffern, _; nicht mit einer Ziffer beginnen).`;
  if ((def?.parameter ?? []).some((p) => p.name === name)) return `Einen Parameter «${name}» gibt es schon.`;
  return true;
}

/** Ändert ein Feld einer Parameterzeile (Eingabetext) → neues Parameterobjekt. feld: art, datentyp, einheit, gruppe, eingabe (Wert/Formel), ziel, beschreibung. */
export function parameterMitFeld(p, feld, text) {
  const n = klon(p);
  if (feld === 'eingabe') {
    const e = wertEingabe(n.datentyp, text);
    delete n.wert; delete n.formel;
    if (e.wert !== undefined) n.wert = e.wert;
    if (e.formel !== undefined) n.formel = e.formel;
  } else if (feld === 'datentyp') {
    n.datentyp = text;
    // Der Vorgabewert passt nicht mehr zum Datentyp: neu einlesen (Formeln bleiben)
    if (n.wert !== undefined) {
      const e = wertEingabe(text, feldText(n.wert));
      delete n.wert;
      if (e.wert !== undefined) n.wert = e.wert; else if (e.formel !== undefined) n.formel = e.formel;
    }
  } else n[feld] = text;
  return parameterSauber(n);
}

// ------------------------------------------------------------------------------------------------ Typen

/** Typparameter ohne Formel = Spalten des Typkatalogs. */
export const typSpalten = (def) => (def?.parameter ?? []).filter((p) => p.art === 'typ' && (p.formel === undefined || p.formel === null));

/** Anzeige eines Typwerts in der Zelle. */
export function typZelleText(p, wert) {
  if (wert === undefined || wert === null) return '';
  if (p.datentyp === 'ja_nein') return wert === true || wert === 1 ? 'ja' : 'nein';
  return feldText(wert);
}

/** Eingabe einer Typzelle → { leer } | { wert } | { fehler }. */
export function typZelleLesen(p, text) {
  const t = String(text ?? '').trim();
  if (t === '') return { leer: true };
  if (TEXT_TYPEN.has(p.datentyp)) return { wert: t };
  if (p.datentyp === 'ja_nein') {
    const k = t.toLowerCase();
    if (WAHR.has(k)) return { wert: true };
    if (FALSCH.has(k)) return { wert: false };
    return { fehler: 'ja oder nein erwartet' };
  }
  const z = zahlLesen(t);
  return z === null ? { fehler: 'Zahl erwartet (Formeln gibt es nur in der Parametertabelle)' } : { wert: z };
}

/** Typ mit geänderter Zelle → { typ } oder { fehler }. */
export function typMitWert(typ, p, text) {
  const e = typZelleLesen(p, text);
  if (e.fehler) return { fehler: e.fehler };
  const n = klon(typ);
  n.werte ??= {};
  if (e.leer) delete n.werte[p.name]; else n.werte[p.name] = e.wert;
  return { typ: n };
}

/** Freier Name nach dem Muster «Basis», «Basis 2», «Basis 3» … */
export function freierName(basis, vorhandene) {
  const belegt = new Set(vorhandene.map((x) => String(x).toLowerCase()));
  if (!belegt.has(String(basis).toLowerCase())) return basis;
  for (let i = 2; i < 10000; i++) if (!belegt.has(`${basis} ${i}`.toLowerCase())) return `${basis} ${i}`;
  return `${basis} ${Date.now()}`;
}

/** Kopie eines Typs unter neuem Namen (Werte kopiert, Quellen nicht – sie gehören zum Original). */
export function typKopie(typ, neuName) {
  return { name: neuName, werte: klon(typ?.werte ?? {}) };
}

/** Umbenennen eines Typs: ops (neuer Eintrag, alter gelöscht). */
export function typUmbenennenOps(typ, neuName) {
  return [{ op: 'typ_setzen', typ: { ...klon(typ), name: neuName } }, { op: 'typ_loeschen', name: typ.name }];
}

// ------------------------------------------------------------------------------------------------ Pfade in Objekten

/** Wert unter einem Pfad wie «achse.punkt». */
export function pfadLesen(obj, pfad) {
  return String(pfad).split('.').reduce((o, k) => (o === undefined || o === null ? undefined : o[k]), obj);
}

/** Kopie des Objekts mit gesetztem Wert unter dem Pfad; undefined entfernt den Eintrag (leere Zwischenobjekte entfallen). */
export function pfadSetzen(obj, pfad, wert) {
  const n = klon(obj);
  const teile = String(pfad).split('.');
  const kette = [n];
  let o = n;
  for (const k of teile.slice(0, -1)) {
    if (o[k] === undefined || o[k] === null || typeof o[k] !== 'object') o[k] = {};
    o = o[k]; kette.push(o);
  }
  const letzter = teile.at(-1);
  if (wert === undefined) delete o[letzter]; else o[letzter] = wert;
  for (let i = kette.length - 1; i > 0; i--) if (Object.keys(kette[i]).length === 0) delete kette[i - 1][teile[i - 1]];
  return n;
}

// ------------------------------------------------------------------------------------------------ Körper

export const KOERPER_ARTEN = [['quader', 'Quader'], ['zylinder', 'Zylinder'], ['kegel', 'Kegel'], ['kugel', 'Kugel'], ['extrusion', 'Extrusion'], ['rotation', 'Rotation'], ['sweep', 'Sweep'], ['uebergang', 'Übergang'], ['netz', 'Netz (Herstellergeometrie)']];
const EBENEN = [['xy', 'xy (Grundriss)'], ['xz', 'xz (Vorderansicht)'], ['yz', 'yz (Seitenansicht)']];

/** Felder je Körperart: { pfad, label, typ: zahl|punkt3|punkt2|punkte2|punkte3|loecher|auswahl|profil, optionen?, optional? }. */
export function koerperFelder(art) {
  const P3 = (pfad, label) => ({ pfad, label, typ: 'punkt3' });
  const Z = (pfad, label, optional = false) => ({ pfad, label, typ: 'zahl', optional });
  switch (art) {
    case 'quader': return [P3('von', 'von (x, y, z)'), P3('bis', 'bis (x, y, z)')];
    case 'zylinder': return [P3('von', 'von (x, y, z)'), P3('nach', 'nach (x, y, z)'), Z('r', 'Radius r')];
    case 'kegel': return [P3('von', 'von (x, y, z)'), P3('nach', 'nach (x, y, z)'), Z('r1', 'Radius r1 (bei von)'), Z('r2', 'Radius r2 (bei nach)')];
    case 'kugel': return [P3('mitte', 'Mitte (x, y, z)'), Z('r', 'Radius r')];
    case 'extrusion': return [{ pfad: 'ebene', label: 'Ebene der Kontur', typ: 'auswahl', optionen: EBENEN }, Z('von', 'von (Lage auf der dritten Achse)'), Z('bis', 'bis (Lage auf der dritten Achse)'),
      { pfad: 'kontur', label: 'Kontur (u, v, Wölbung)', typ: 'punkte2', min: 2 }, { pfad: 'loecher', label: 'Löcher', typ: 'loecher' }];
    case 'rotation': return [{ pfad: 'ebene', label: 'Ebene der Kontur', typ: 'auswahl', optionen: EBENEN },
      { pfad: 'kontur', label: 'Kontur (u, v, Wölbung)', typ: 'punkte2', min: 2 },
      { pfad: 'achse.punkt', label: 'Achse: Punkt (u, v)', typ: 'punkt2' },
      { pfad: 'achse.richtung', label: 'Achse: Richtung', typ: 'auswahl', optionen: [['u', 'u'], ['v', 'v']] },
      Z('winkel', 'Winkel (leer = 360)', true)];
    case 'sweep': return [{ pfad: 'profil', label: 'Profil', typ: 'profil' }, { pfad: 'pfad', label: 'Pfad (x, y, z)', typ: 'punkte3', min: 2 }, Z('biegeradius', 'Biegeradius', true)];
    // Übergang zwischen zwei Profilen (rund–eckig, exzentrisch) und Herstellergeometrie (30.10.2026: «es müssen alle geometrien abgebildet werden können»)
    case 'uebergang': return [P3('von', 'von (x, y, z)'), { pfad: 'profil', label: 'Profil bei von', typ: 'profil' }, P3('nach', 'nach (x, y, z)'),
      { pfad: 'profil_nach', label: 'Profil bei nach', typ: 'profil' }, { pfad: 'oben', label: 'oben (Richtung der Höhe h, leer = z)', typ: 'punkt3', optional: true }];
    case 'netz': return [{ pfad: 'dreiecke', label: 'Netz', typ: 'netzinfo' }, { pfad: 'verschiebung', label: 'Verschiebung (x, y, z)', typ: 'punkt3', optional: true }, Z('drehung', 'Drehung um z (Grad)', true)];
    default: return [];
  }
}

/** Platzhaltermasse eines neuen Körpers (kein Herstellerwert). */
export function koerperNeu(art, id, name) {
  const k = { id, name: name ?? id, art, material: art === 'zylinder' || art === 'sweep' ? 'rohr' : 'geraet' };
  switch (art) {
    case 'quader': k.von = [0, 0, 0]; k.bis = [100, 100, 100]; break;
    case 'zylinder': k.von = [0, 0, 0]; k.nach = [0, 0, 100]; k.r = 50; break;
    case 'kegel': k.von = [0, 0, 0]; k.nach = [0, 0, 100]; k.r1 = 50; k.r2 = 25; break;
    case 'kugel': k.mitte = [0, 0, 50]; k.r = 50; break;
    case 'extrusion': k.ebene = 'xy'; k.kontur = [[0, 0], [100, 0], [100, 100], [0, 100]]; k.von = 0; k.bis = 100; break;
    case 'rotation': k.ebene = 'xz'; k.kontur = [[10, 0], [50, 0], [50, 100], [10, 100]]; k.achse = { punkt: [0, 0], richtung: 'v' }; break;
    case 'sweep': k.profil = { kreis: 10 }; k.pfad = [[0, 0, 0], [0, 0, 100], [100, 0, 100]]; break;
    case 'uebergang': k.von = [0, 0, 0]; k.nach = [0, 0, 100]; k.profil = { kreis: 50 }; k.profil_nach = { rechteck: [100, 60] }; break;
    case 'netz': k.ecken = [[0, 0, 0], [100, 0, 0], [0, 100, 0], [0, 0, 100]]; k.dreiecke = [[0, 2, 1], [0, 1, 3], [1, 2, 3], [0, 3, 2]]; break;
    default: break;
  }
  return k;
}

/** Freie Körper-Id: «quader1», «quader2» … */
export function freieKoerperId(def, art) {
  const belegt = new Set((def?.koerper ?? []).map((k) => k.id));
  for (let i = 1; i < 10000; i++) if (!belegt.has(`${art}${i}`)) return `${art}${i}`;
  return `${art}${Date.now()}`;
}

/** Detailgrade eines Körpers (Vorgabe alle). */
export const koerperDetailgrade = (k) => (Array.isArray(k?.detailgrad) && k.detailgrad.length ? k.detailgrad : DETAILGRADE);

/** Körper mit gesetzten Detailgraden; alle drei = Vorgabe (Feld entfällt). Mindestens einer bleibt. */
export function detailgradSetzen(k, grad, an) {
  const jetzt = new Set(koerperDetailgrade(k));
  if (an) jetzt.add(grad); else jetzt.delete(grad);
  if (jetzt.size === 0) return { fehler: 'Mindestens ein Detailgrad muss bleiben.' };
  const n = klon(k);
  const liste = DETAILGRADE.filter((g) => jetzt.has(g));
  if (liste.length === DETAILGRADE.length) delete n.detailgrad; else n.detailgrad = liste;
  return { koerper: n };
}

/** Zeile der Körperliste: Art, Name, Detailgrad als Kurztext («g m f» mit gesperrten Graden als «–»). */
export function detailgradKurz(k) {
  const d = new Set(koerperDetailgrade(k));
  return DETAILGRADE.map((g) => (d.has(g) ? g[0] : '–')).join(' ');
}

/** Wert eines Feldes aus Eingabetexten: Punkt aus Zellen (x, y, z) → Array aus Zahl/Formel; leere Zelle → Fehler. */
export function punktAusTexten(texte, anzahl) {
  const p = [];
  for (let i = 0; i < anzahl; i++) {
    const z = zahlOderFormel(texte[i]);
    if (z === undefined) return { fehler: 'Wert fehlt' };
    p.push(z);
  }
  return { punkt: p };
}

/** Konturzeile aus Texten: [u, v] oder [u, v, wölbung] (Wölbung leer = weglassen). */
export function konturZeile(texte) {
  const u = zahlOderFormel(texte[0]), v = zahlOderFormel(texte[1]), w = zahlOderFormel(texte[2]);
  if (u === undefined || v === undefined) return { fehler: 'u und v fehlen' };
  return { punkt: w === undefined ? [u, v] : [u, v, w] };
}

/** Profil eines Sweeps: Art wechseln. */
export function profilWechseln(art) {
  return art === 'rechteck' ? { rechteck: [20, 20] } : { kreis: 10 };
}
export const profilArt = (profil) => (profil && profil.rechteck !== undefined ? 'rechteck' : 'kreis');

// ------------------------------------------------------------------------------------------------ Sinnbild

export const SINNBILD_ARTEN = [['linie', 'Linie'], ['polylinie', 'Polylinie'], ['polygon', 'Polygon'], ['kreis', 'Kreis'], ['bogen', 'Bogen'], ['text', 'Text']];
export const STIFTE = [['', 'Vorgabe'], ['duenn', 'dünn'], ['mittel', 'mittel'], ['dick', 'dick']];
export const LINIENARTEN = [['', 'durchgezogen'], ['voll', 'voll'], ['strich', 'Strich'], ['strichpunkt', 'Strichpunkt'], ['punkt', 'Punkt']];

/** Felder eines Sinnbildteils je Art (Punkte in Familienkoordinaten von oben, x nach rechts, y nach hinten). */
export function sinnbildFelder(art) {
  const Z = (pfad, label, optional = false) => ({ pfad, label, typ: 'zahl', optional });
  switch (art) {
    case 'linie': return [{ pfad: 'punkte', label: 'Punkte (x, y)', typ: 'punkte2', min: 2, max: 2, ohneWoelbung: true }];
    case 'polylinie': return [{ pfad: 'punkte', label: 'Punkte (x, y)', typ: 'punkte2', min: 2, ohneWoelbung: true }];
    case 'polygon': return [{ pfad: 'punkte', label: 'Punkte (x, y)', typ: 'punkte2', min: 3, ohneWoelbung: true }];
    case 'kreis': return [{ pfad: 'punkte', label: 'Mitte (x, y)', typ: 'punkte2', min: 1, max: 1, ohneWoelbung: true }, Z('radius', 'Radius')];
    case 'bogen': return [{ pfad: 'punkte', label: 'Mitte (x, y)', typ: 'punkte2', min: 1, max: 1, ohneWoelbung: true }, Z('radius', 'Radius'), Z('von', 'Anfangswinkel (Grad)'), Z('bis', 'Endwinkel (Grad)')];
    case 'text': return [{ pfad: 'punkte', label: 'Lage (x, y)', typ: 'punkte2', min: 1, max: 1, ohneWoelbung: true }, { pfad: 'text', label: 'Text', typ: 'text' }, { pfad: 'groesse', label: 'Schrifthöhe (Papier-mm)', typ: 'zahlnur', optional: true }];
    default: return [];
  }
}

/** Platzhalter eines neuen Sinnbildteils (Familienkoordinaten, kein Herstellermass). */
export function sinnbildNeu(art) {
  switch (art) {
    case 'linie': return { art, punkte: [[0, 0], [100, 0]] };
    case 'polylinie': return { art, punkte: [[0, 0], [100, 0], [100, 100]] };
    case 'polygon': return { art, punkte: [[0, 0], [100, 0], [100, 100], [0, 100]] };
    case 'kreis': return { art, punkte: [[50, 50]], radius: 25 };
    case 'bogen': return { art, punkte: [[50, 50]], radius: 25, von: 0, bis: 180 };
    case 'text': return { art, punkte: [[0, 0]], text: 'Text', groesse: 2.5 };
    default: return { art };
  }
}

// ------------------------------------------------------------------------------------------------ Anschlüsse

/** Welche Grössenfelder gehören zum Anschluss? rohr → dn · kanal rund → d · kanal rechteckig → b, h · elektro → keine (Entscheid Kern 18.10.2026). */
export function anschlussGroesse(a) {
  if (a?.art === 'kanal') return a.form === 'rechteck' ? ['b', 'h'] : ['d'];
  if (a?.art === 'elektro') return [];
  return ['dn'];
}

/** Neuer Anschluss; die Grösse (dn, d, b/h) bleibt leer – sie kommt nur aus einer Unterlage. */
export function anschlussNeu({ name, medium = '*', fluss = 'beide', art = 'rohr', form = 'rund' }) {
  const a = { name: String(name).trim(), medium, fluss, art, form, lage: [0, 0, 0], richtung: [0, 0, -1] };
  if (art === 'elektro') delete a.form;
  return a;
}

/** Anschluss mit geändertem Feld (Eingabetext) → { anschluss } oder { fehler }. feld: name, medium, fluss, art, form, dn, d, b, h, gewinde, beschreibung, lage.0 … richtung.2 */
export function anschlussMitFeld(a, feld, text) {
  const n = klon(a);
  const t = String(text ?? '').trim();
  const m = /^(lage|richtung)\.([012])$/.exec(feld);
  if (m) {
    const z = zahlOderFormel(t);
    if (z === undefined) return { fehler: 'Wert fehlt' };
    n[m[1]] = [...(n[m[1]] ?? [0, 0, 0])];
    n[m[1]][Number(m[2])] = z;
    return { anschluss: n };
  }
  if (['dn', 'd', 'b', 'h'].includes(feld)) {
    const z = zahlOderFormel(t);
    if (z === undefined) delete n[feld]; else n[feld] = z;
    return { anschluss: n };
  }
  if (t === '' && !['name', 'medium', 'fluss'].includes(feld)) delete n[feld]; else n[feld] = t;
  if (feld === 'art') {
    // Grössenfelder, die zur neuen Art nicht passen, entfallen (der Kern meldet sie sonst als Hinweis)
    const erlaubt = new Set(anschlussGroesse(n));
    for (const g of ['dn', 'd', 'b', 'h']) if (!erlaubt.has(g)) delete n[g];
    if (t === 'elektro') delete n.form; else n.form ??= 'rund';
  }
  if (feld === 'form') {
    const erlaubt = new Set(anschlussGroesse(n));
    for (const g of ['d', 'b', 'h']) if (!erlaubt.has(g)) delete n[g];
  }
  return { anschluss: n };
}

/** Auswahl für das Medium: «*», Kennungen aus dem Katalog, Parameter mit Datentyp medium. [[wert, text]] */
export function mediumAuswahl(medien, def) {
  const l = [['*', '* (Medium des Systems)']];
  for (const m of medien ?? []) l.push([m.kennung, `${m.kennung} – ${m.name}`]);
  for (const p of def?.parameter ?? []) if (p.datentyp === 'medium') l.push([p.name, `Parameter ${p.name}`]);
  return l;
}

// ------------------------------------------------------------------------------------------------ Fehler des Kerns

/** Zerlegt die Ablehnung des Kerns in einzelne Fehler: [{ stelle, meldung, vorschlag }]; text = alles ohne Vorspann. */
export function kernFehler(r) {
  const roh = String(r?.fehler ?? r?.meldung ?? '');
  const i = roh.search(/\d+ Fehler – /);
  const nachVorspann = i >= 0 ? roh.slice(roh.indexOf(' – ', i) + 3) : roh.replace(/^Abgelehnt:\s*/, '');
  const stuecke = nachVorspann.split(/(?:^|\s)\((\d+)\)\s/).slice(1);
  const eintraege = [];
  for (let k = 0; k + 1 < stuecke.length; k += 2) eintraege.push({ nr: Number(stuecke[k]), text: stuecke[k + 1].trim() });
  const vorschlaege = new Map();
  const v = String(r?.vorschlag ?? '');
  const vs = v.split(/(?:^|\s)\((\d+)\)\s/).slice(1);
  for (let k = 0; k + 1 < vs.length; k += 2) vorschlaege.set(Number(vs[k]), vs[k + 1].trim());
  const liste = eintraege.map((e) => {
    const j = e.text.indexOf(': ');
    return { stelle: j > 0 ? e.text.slice(0, j) : '', meldung: j > 0 ? e.text.slice(j + 2) : e.text, vorschlag: vorschlaege.get(e.nr) ?? '' };
  });
  if (liste.length === 0) liste.push({ stelle: '', meldung: nachVorspann || 'Abgelehnt.', vorschlag: v });
  return { eintraege: liste, text: liste.map((e) => e.meldung).join(' · '), vorschlag: liste[0].vorschlag };
}

/** Wohin springt ein Fehler? stelle → { tab, name, feld } oder null. */
export function stelleZiel(stelle) {
  const s = String(stelle ?? '');
  let m;
  if ((m = /^parameter «([^»]+)»(?:\.(\w+))?/.exec(s))) return { tab: 'parameter', name: m[1], feld: m[2] ? (m[2] === 'formel' || m[2] === 'wert' ? 'eingabe' : m[2]) : '' };
  if ((m = /^typ «([^»]+)»(?:\.(?:werte|quellen)\.([^.\s]+))?/.exec(s))) return { tab: 'typen', name: m[1], feld: m[2] ?? '' };
  if ((m = /^koerper «([^»]+)»(?:\.(\w+(?:\.\w+)?))?/.exec(s))) return { tab: 'koerper', name: m[1], feld: m[2] ?? '' };
  if ((m = /^anschluss «([^»]+)»(?:\.(\w+))?/.exec(s))) return { tab: 'anschluesse', name: m[1], feld: m[2] ?? '' };
  if ((m = /^sinnbild\[(\d+)\](?:\.(\w+))?/.exec(s))) return { tab: 'sinnbild', name: m[1], feld: m[2] ?? '' };
  if ((m = /^leitung(?:\.([\w.]+))?$/.exec(s))) return { tab: 'leitung', name: '', feld: m[1] ?? '' };   // Modul 3 (C3): Seite «Leitungsserie»
  return null;
}

/** Zeilen der Prüfliste aus der Antwort der Abfrage familie: Definitionsfehler und Fehler je Typ. */
export function pruefZeilen(f) {
  const aus = [];
  for (const e of f?.definitionsfehler ?? []) aus.push({ art: 'Definition', typ: '', stelle: e.stelle, meldung: e.meldung, vorschlag: e.vorschlag ?? '' });
  for (const t of f?.typen ?? []) for (const e of t.auswertungsfehler ?? []) aus.push({ art: `Typ «${t.name}»`, typ: t.name, stelle: e.stelle, meldung: e.meldung, vorschlag: e.vorschlag ?? '' });
  // Fehlt einem Typ ein Wert («Parameter «x» hat keinen Wert»), ist die Zelle im Typkatalog das Ziel – die Stelle des Kerns nennt nur die Verwendung (z. B. die Formel)
  const ziel = (z) => {
    const fehlt = z.typ ? /Parameter «([^»]+)» hat keinen Wert/.exec(z.meldung ?? '') : null;
    if (fehlt) return { tab: 'typen', name: z.typ, feld: fehlt[1] };
    return stelleZiel(z.stelle) ?? (z.typ ? { tab: 'typen', name: z.typ, feld: '' } : null);
  };
  return aus.map((z) => ({ ...z, ziel: ziel(z) }));
}

// ------------------------------------------------------------------------------------------------ Neue Familie

/** Familienfähige Klassen aus dem Katalog (Kurzform): `familie: true` (seit Modul 2, C2); ältere Antworten ohne das Feld: die Klasse hat die Eigenschaft «familientyp». */
export function familienKategorien(klassen) {
  const hat = (k) => k.familie === true || (k.familie === undefined && (k.eigenschaften ?? []).some((e) => String(e).split(' ')[0] === 'familientyp'));
  return (klassen ?? []).filter(hat).map((k) => k.name).sort((a, b) => a.localeCompare(b, 'de'));
}

/**
 * Minimaler Entwurf einer neuen Familie mit einem Typ «Standard» (ohne Körper, Anschlüsse; ein Parameter wäre erfunden).
 * Modul 3 (C3): Eine Leitungsserie (Kategorie Rohr bzw. Kanal, auch abgeleitete Klassen) braucht den Block «leitung» – dann entstehen
 * zusätzlich die Typparameter der Vorlage (leitungVorlage) ohne Werte; die Werte trägt der Mensch bzw. die KI aus Unterlagen ein.
 * opt: { klassen (Kurzform des Katalogs, für abgeleitete Klassen), form (siehe LEITUNG_FORMEN; Vorgabe die erste der Kategorie) }.
 */
export function familieEntwurfMinimal(kategorie, name, opt = {}) {
  const d = { format: 1, name: String(name).trim(), kategorie, parameter: [], typen: [{ name: 'Standard', werte: {} }], koerper: [], anschluesse: [] };
  const formen = leitungFormen(leitungsArt(kategorie, opt.klassen));
  if (formen.length) {
    const v = leitungVorlage(formen.some(([f]) => f === opt.form) ? opt.form : formen[0][0]);
    d.parameter = v.parameter;
    d.leitung = v.leitung;
  }
  return d;
}

// ------------------------------------------------------------------------------------------------ Leitungsserie (Modul 3, C3)
//
// Eine Leitungsserie ist eine Familie der Kategorie Rohr bzw. Kanal mit dem Block «leitung» (schema/hlks-familie.schema.json). Der Editor macht aus
// den Feldern des Formulars den ganzen Block und sendet ihn mit der op `leitung_setzen` über familie_aendern; ob er gilt (Parameter, Formeln,
// Typregeln, Winkel), sagt der Kern. Hier steht nur das Umrechnen Formular ↔ Block, keine Fachregel.

export const QUERSCHNITTE = [['rund', 'rund'], ['rechteck', 'rechteckig']];
/** Formteilarten des Blocks (Schlüssel in leitung.formteile) mit Anzeigename und Kategorie der Formteil-Familie. */
export const FORMTEIL_ARTEN = [
  { art: 'bogen', label: 'Bogen', kategorie: 'Bogen' },
  { art: 'tstueck', label: 'T-Stück', kategorie: 'TStueck' },
  { art: 'reduktion', label: 'Reduktion', kategorie: 'Reduktion' },
  { art: 'reinigungsoeffnung', label: 'Reinigungsöffnung', kategorie: 'Reinigungsoeffnung' },
];
const GROESSE_LABEL = { dn: 'Grösse DN', d: 'Grösse Ø d', b: 'Grösse Breite b', h: 'Grösse Höhe h' };
/** Einsatz einer Rohrserie (leitung.einsatz; weitere Werte später) und Grössenart eines Rohrs (Abwasser: Aussendurchmesser oder Nennweite). */
export const EINSAETZE = [['abwasser', 'Abwasser (Schmutz-, Regenwasser)']];
export const GROESSENARTEN = [['d', 'Aussendurchmesser d (Abwasser)'], ['dn', 'Nennweite DN']];
/** Datentypen, die als Grössenparameter in Frage kommen (der Kern verlangt eine Zahl). */
const GROESSE_DATENTYPEN = new Set(['dn', 'laenge', 'zahl']);

/** Art der Leitungsserie zu einer Kategorie: 'rohr' | 'kanal' | 'leitung' (abstrakte Basisklasse) | null (keine Leitungsserie). Läuft über `basis` der Kurzform. */
export function leitungsArt(kategorie, klassen) {
  const nach = new Map((klassen ?? []).map((k) => [k.name, k]));
  let n = kategorie;
  for (let i = 0; n && i < 12; i++) {
    if (n === 'Rohr') return 'rohr';
    if (n === 'Kanal') return 'kanal';
    if (n === 'Leitung') return 'leitung';
    n = nach.get(n)?.basis ?? null;
  }
  return null;
}

/** Querschnitt und Grössenparameter der Vorlagen (Auswahl im Dialog «Neue Familie»). */
export const LEITUNG_FORMEN = [['rohr', 'rund · Nennweite DN (Rohr)'], ['kanal-rund', 'rund · Durchmesser d (Kanal)'], ['kanal-rechteck', 'rechteckig · Breite b × Höhe h (Kanal)']];

/** Welche Vorlagen gehören zur Art? Rohr ist immer rund (Kern); Kanal rund oder rechteckig; die abstrakte Basisklasse Leitung lässt alle zu. */
export function leitungFormen(art) {
  if (art === 'rohr') return LEITUNG_FORMEN.filter(([f]) => f === 'rohr');
  if (art === 'kanal') return LEITUNG_FORMEN.filter(([f]) => f !== 'rohr');
  if (art === 'leitung') return [...LEITUNG_FORMEN];
  return [];
}

/** Typparameter für eine Grösse (ohne Werte; die Werte kommen aus der Unterlage). Rohr dn: ziel «dn» wie in den Testserien. */
export function groessenParameterNeu(schluessel) {
  const p = { name: schluessel, art: 'typ', datentyp: schluessel === 'dn' ? 'dn' : 'laenge', gruppe: 'abmessung' };
  if (schluessel === 'dn') p.ziel = 'dn';
  p.beschreibung = { dn: 'Nennweite (Grösse) – Werte aus der Herstellerunterlage', d: 'Durchmesser in mm – Werte aus der Herstellerunterlage', b: 'Breite in mm – Werte aus der Herstellerunterlage', h: 'Höhe in mm – Werte aus der Herstellerunterlage' }[schluessel];
  return p;
}

/** Parameter und Block «leitung» einer Vorlage. Der Aussendurchmesser ist ein Typparameter «da» ohne Werte (rechteckig: nicht genutzt, der Block verlangt ihn trotzdem – wie die Testserie «b»). */
export function leitungVorlage(form) {
  const aussen = { name: 'da', art: 'typ', datentyp: 'laenge', gruppe: 'abmessung', beschreibung: 'Aussendurchmesser in mm – Werte aus der Herstellerunterlage' };
  if (form === 'kanal-rechteck') {
    return { parameter: [groessenParameterNeu('b'), groessenParameterNeu('h')], leitung: { querschnitt: 'rechteck', groesse: { b: 'b', h: 'h' }, aussen: 'b' } };
  }
  const k = form === 'kanal-rund' ? 'd' : 'dn';
  return { parameter: [groessenParameterNeu(k), aussen], leitung: { querschnitt: 'rund', groesse: { [k]: k }, aussen: 'da' } };
}

/** Typparameter mit Zahl: die Namen, die als Grösse gewählt werden können. */
export const groessenParameter = (def) => (def?.parameter ?? []).filter((p) => p.art === 'typ' && GROESSE_DATENTYPEN.has(p.datentyp)).map((p) => p.name);

/** Grössenschlüssel des Blocks: rechteckig b, h · rund bei Kanal d, bei Rohr dn; bei der Basisklasse Leitung nach dem, was der Block hat (Kern: «d», sonst «dn»). */
export function groesseSchluessel(block, art) {
  if (block?.querschnitt === 'rechteck') return ['b', 'h'];
  if (art === 'kanal') return ['d'];
  // Rohr: DN, bei einer Abwasser-Serie (einsatz abwasser) auch der Aussendurchmesser d (Nachtrag, entwurf/abwasser-serie.md); bleibt, was der Block hat
  if (art === 'rohr') return block?.groesse && Object.hasOwn(block.groesse, 'd') ? ['d'] : ['dn'];
  return block?.groesse && Object.hasOwn(block.groesse, 'd') ? ['d'] : ['dn'];
}

/** Grössenart eines Rohrs wechseln (nur Abwasser-Serien): 'dn' Nennweite oder 'd' Aussendurchmesser. Der Typparameter mit dem Schlüsselnamen wird genommen oder (ohne Werte)
 *  angelegt; den Rest (ob einsatz abwasser gesetzt ist, ob die Werte stimmen) prüft der Kern. → { block, ops } | { fehler }. */
export function groessenartWechseln(block, def, neu) {
  if (!['dn', 'd'].includes(neu)) return { fehler: `Grössenart «${neu}» gibt es nicht.` };
  const b = klon(block ?? {});
  const gueltig = new Set(groessenParameter(def));
  const ops = [];
  const vorhanden = (def?.parameter ?? []).find((p) => p.name === neu);
  if (vorhanden && !gueltig.has(neu)) return { fehler: `Der Parameter «${neu}» gibt es schon, er ist aber kein Typparameter mit Zahl – als Grösse braucht der Block einen Typparameter (Datentyp DN, Länge oder Zahl).` };
  if (!vorhanden) ops.push({ op: 'parameter_setzen', parameter: groessenParameterNeu(neu) });
  b.groesse = { [neu]: neu };
  ops.push({ op: 'leitung_setzen', leitung: b });
  return { block: b, ops };
}

/** Querschnitt wechseln: neuer Block mit den Grössenschlüsseln der neuen Form. Gibt es den Typparameter mit dem Schlüsselnamen schon, wird er genommen, sonst kommt ein neuer
 *  (ohne Werte) hinzu. → { block, ops: [parameter_setzen …, leitung_setzen] } oder { fehler }. */
export function querschnittWechseln(block, def, art, neu) {
  if (!QUERSCHNITTE.some(([w]) => w === neu)) return { fehler: `Querschnitt «${neu}» gibt es nicht.` };
  const b = klon(block ?? {});
  b.querschnitt = neu;
  const keys = groesseSchluessel(b, art);
  const gueltig = new Set(groessenParameter(def));
  const ops = [];
  b.groesse = {};
  for (const k of keys) {
    const vorhanden = (def?.parameter ?? []).find((p) => p.name === k);
    if (vorhanden && !gueltig.has(k)) return { fehler: `Der Parameter «${k}» gibt es schon, er ist aber kein Typparameter mit Zahl – als Grösse braucht der Block einen Typparameter (Datentyp DN, Länge oder Zahl).` };
    if (!vorhanden) ops.push({ op: 'parameter_setzen', parameter: groessenParameterNeu(k) });
    b.groesse[k] = k;
  }
  ops.push({ op: 'leitung_setzen', leitung: b });
  return { block: b, ops };
}

/** Liste von Zahlen aus «15, 30; 45 90°»: Komma, Semikolon und Leerzeichen trennen, das Dezimalzeichen ist der Punkt («88.5»). → { liste } | { fehler }. */
export function zahlenListeLesen(text) {
  const teile = String(text ?? '').replace(/°/g, ' ').split(/[;,\s]+/).map((s) => s.trim()).filter(Boolean);
  const liste = [];
  for (const t of teile) {
    const z = zahlLesen(t);
    if (z === null) return { fehler: `«${t}» ist keine Zahl` };
    liste.push(z);
  }
  return { liste };
}

/** Anzeige einer Zahlenliste: «15, 30, 45, 90». */
export const zahlenListeText = (l) => (Array.isArray(l) ? l.map((x) => feldText(x)).join(', ') : '');

/** Eingabe eines Feldes des Blocks → { wert } (undefined = Feld entfernen) oder { fehler }. typ: zahl (Pflicht) | zahlopt | winkel («frei» oder Liste) | liste (Zahlenliste) | typregel (Formel-Text) | auswahl | mischbar (Serie → Liste) | text (freier Text, leer = entfernen). */
export function leitungFeldLesen(typ, text) {
  const t = String(text ?? '').trim();
  switch (typ) {
    case 'zahl': { const z = zahlOderFormel(t); return z === undefined ? { fehler: 'Wert fehlt – eine Zahl oder eine Formel aus den Parametern' } : { wert: z }; }
    case 'zahlopt': return { wert: zahlOderFormel(t) };
    case 'winkel': {
      if (t === '') return { wert: undefined };
      if (t.toLowerCase() === 'frei') return { wert: 'frei' };
      const r = zahlenListeLesen(t);
      if (r.fehler) return { fehler: `${r.fehler} – Winkel als Liste in Grad (15, 30, 45, 90) oder «frei»` };
      return r.liste.length ? { wert: r.liste } : { wert: undefined };
    }
    case 'liste': {
      if (t === '') return { wert: undefined };
      const r = zahlenListeLesen(t);
      if (r.fehler) return { fehler: `${r.fehler} – Liste in Grad, z. B. 45, 88.5` };
      return r.liste.length ? { wert: r.liste } : { wert: undefined };
    }
    case 'masse': {   // freie Grösse: Grenze in mm (Zahl > 0), leer = keine Grenze
      if (t === '') return { wert: undefined };
      const z = zahlLesen(t.replace(/\s*mm$/i, ''));
      if (z === null || !(z > 0)) return { fehler: `«${t}» ist keine Grenze – eine Zahl über 0 in mm (leer = keine Grenze)` };
      return { wert: z };
    }
    case 'ganzzahl': {   // freie Grösse: Schritt in ganzen mm, leer = Vorgabe 1
      if (t === '') return { wert: undefined };
      const z = zahlLesen(t.replace(/\s*mm$/i, ''));
      if (z === null || !Number.isInteger(z) || z < 1) return { fehler: `«${t}» ist kein Schritt – eine ganze Zahl ab 1 in mm (leer = 1 mm)` };
      return { wert: z };
    }
    case 'typregel': { const f = t.startsWith('=') ? t.slice(1).trim() : t; return { wert: f === '' ? undefined : f }; }
    case 'auswahl': return { wert: t === '' ? undefined : t };
    case 'mischbar': return { wert: t === '' ? undefined : [t] };   // Liste mit einer Serie (Kern: regeln.mischbar)
    case 'text': return { wert: t === '' ? undefined : t };   // freier Text (Kürzel); Länge und Zeichen prüft der Kern
    default: return { fehler: 'unbekanntes Feld' };
  }
}

/** Block mit gesetztem Feld (Pfad wie «formteile.bogen.familie», «regeln.winkel»); undefined entfernt das Feld. Wird die Familie einer Formteilart entfernt, entfällt die Art samt Typregel. */
export function leitungMitFeld(block, pfad, wert) {
  let n = pfadSetzen(block ?? {}, pfad, wert);
  const m = /^formteile\.([a-z]+)\.familie$/.exec(pfad);
  if (m && wert === undefined) n = pfadSetzen(n, `formteile.${m[1]}`, undefined);
  // Freie Grösse: ohne Vorlage entfällt der ganze Block frei (Grenzen und Schritt gehören dazu); leere Grenzen eines Schlüssels entfallen mit ihm
  if (pfad === 'frei.vorlage' && wert === undefined) n = pfadSetzen(n, 'frei', undefined);
  return n;
}

/** Die op zum Speichern des Blocks. */
export const leitungOps = (block) => [{ op: 'leitung_setzen', leitung: klon(block) }];

/** Auswahl der Formteil-Familien einer Art aus der Liste der Familien (Abfrage familien): [[definitions-id, «FAM-2 · Name»]]. Eine Familie, die der Block nennt, aber im Projekt fehlt, bleibt sichtbar. */
export function formteilOptionen(liste, kategorie, aktuell) {
  const o = (liste ?? []).filter((f) => f.kategorie === kategorie && f.id).sort((a, b) => String(a.name).localeCompare(String(b.name), 'de'))
    .map((f) => [f.id, `${f.kennzeichen} · ${f.name}${f.gueltig === false ? ' (ungültig)' : ''}`]);
  if (aktuell && !o.some(([w]) => w === aktuell)) o.push([aktuell, `${aktuell} (nicht im Projekt)`]);
  return o;
}

/** Typen der Formteil-Familie mit dieser Definitions-id (Antwort familien: typen = Namen) als Auswahl; ein Typ, den der Block nennt und die Familie nicht hat, bleibt sichtbar. */
export function formteilTypOptionen(liste, familieId, aktuell) {
  const f = (liste ?? []).find((x) => x.id === familieId);
  const o = (f?.typen ?? []).map((t) => [t, t]);
  if (aktuell && !o.some(([w]) => w === aktuell)) o.push([aktuell, f ? `${aktuell} (nicht in der Familie)` : `${aktuell} (Familie nicht im Projekt)`]);
  return o;
}

/** Querschnitt-Auswahl: ein Rohr ist immer rund (der Kern lehnt rechteckig ab). */
export const querschnittOptionen = (art) => (art === 'rohr' ? QUERSCHNITTE.filter(([w]) => w === 'rund') : QUERSCHNITTE);

/**
 * Definition → Formular: die Felder der Seite «Leitungsserie» in Reihenfolge, je { gruppe, pfad, label, typ, text|wert, optionen?, leer?, titel?, platzhalter?, deaktiviert?, hinweis? }.
 * o: { art ('rohr'|'kanal'|'leitung'|null), familien (Antwort familien) }.
 */
export function leitungFelder(def, o = {}) {
  const b = def?.leitung ?? {};
  const params = groessenParameter(def);
  const f = [];
  f.push({ gruppe: 'Querschnitt und Grösse', pfad: 'querschnitt', label: 'Querschnitt', typ: 'auswahl', optionen: querschnittOptionen(o.art), wert: b.querschnitt ?? '',
    titel: 'Rohre sind immer rund; Kanäle rund (Ø d) oder rechteckig (b × h). Ein Wechsel legt fehlende Grössenparameter ohne Werte an.' });
  // Einsatz (Nachtrag, entwurf/abwasser-serie.md; nur Rohre): «Abwasser» = Grösse und Beschriftung nach Aussendurchmesser, keine DN nötig, Prüfbefund P30 in einem anderen Medium
  if (o.art !== 'kanal') {
    f.push({ gruppe: 'Querschnitt und Grösse', pfad: 'einsatz', label: 'Einsatz', typ: 'auswahl', optionen: EINSAETZE, wert: b.einsatz ?? '', leer: '(nicht angegeben)',
      titel: 'Abwasser: Abwasser-Serie (z. B. Geberit PE, Silent-db20, PP-Hausabfluss). Grösse und Beschriftung immer nach Aussendurchmesser (z. B. «PE 110»), unabhängig vom Medium; keine DN nötig. Verwendet jemand die Serie für Heizung, Kälte oder Trinkwasser, meldet der Kern den Prüfbefund P30.' });
    if (b.einsatz === 'abwasser')
      f.push({ gruppe: 'Querschnitt und Grösse', pfad: 'groessenart', label: 'Grösse geführt nach', typ: 'auswahl', optionen: GROESSENARTEN, wert: Object.hasOwn(b.groesse ?? {}, 'd') ? 'd' : 'dn',
        titel: 'Aussendurchmesser d (Abwasser-Serie nach Aussendurchmesser, «d 110», keine DN nötig) oder Nennweite DN. Eine DN der Herstellerunterlage steht höchstens als eigener Typparameter mit Ziel dn – der Kern leitet sie nie aus d ab.' });
  }
  for (const k of groesseSchluessel(b, o.art)) {
    f.push({ gruppe: 'Querschnitt und Grösse', pfad: `groesse.${k}`, label: k === 'd' && o.art === 'rohr' ? 'Grösse Aussendurchmesser d' : GROESSE_LABEL[k], typ: 'auswahl', optionen: params.map((n) => [n, n]), wert: b.groesse?.[k] ?? '', leer: '(wählen)',
      titel: `Typparameter mit der Grösse «${k}»: sein Wert je Typ ist die Grösse der Leitung` });
  }
  f.push({ gruppe: 'Masse (Formeln aus den Parametern)', pfad: 'aussen', label: 'Aussendurchmesser', typ: 'zahl', text: feldText(b.aussen), platzhalter: 'z. B. da', titel: 'Pflicht. Formel aus den Parametern, z. B. «da» (bei rechteckigen Kanälen nicht genutzt)' });
  f.push({ gruppe: 'Masse (Formeln aus den Parametern)', pfad: 'innen', label: 'Innendurchmesser', typ: 'zahlopt', text: feldText(b.innen), platzhalter: 'z. B. da - 2 * s', titel: 'Optional. Formel, z. B. «da - 2 * s»' });
  f.push({ gruppe: 'Masse (Formeln aus den Parametern)', pfad: 'wand', label: 'Wandstärke', typ: 'zahlopt', text: feldText(b.wand), platzhalter: 'z. B. s', titel: 'Optional. Formel aus den Parametern' });
  // Freie Grösse (Nachtrag, entwurf/freie-groesse-kanal.md): nur Kanalserien, nie Rohre. Die Typen bleiben das Vorschlagsraster; jede andere Grösse innerhalb von Grenzen
  // und Schritt rechnet der Kern aus dem Typ «Vorlage» (die Grössenparameter ersetzt er). Grenzen nur aus der Unterlage (frei.quellen).
  if (o.art !== 'rohr') {
    const fr = b.frei ?? null;
    const typen = (def?.typen ?? []).map((t) => [t.name, t.name]);
    f.push({ gruppe: 'Freie Grösse (nach Mass gefertigt)', pfad: 'frei.vorlage', label: 'Vorlage (Typ)', typ: 'auswahl', optionen: typen, wert: fr?.vorlage ?? '', leer: '(keine – nur die Typen)',
      hinweis: fr && fr.grenzen && Object.keys(fr.grenzen).length ? (Array.isArray(fr.quellen) && fr.quellen.length ? `Quellen der Grenzen: ${fr.quellen.length}` : 'Grenzen ohne Quelle (frei.quellen) – der Kern meldet das als Hinweis') : undefined,
      titel:'Leer = nur die Grössen der Typen. Mit Vorlage kann jede Grösse b × h bzw. Ø d innerhalb der Grenzen gewählt werden; ihre Werte und Formeln (aussen, innen, wand …) gelten für jede freie Grösse, die Grössenparameter werden ersetzt (sie müssen Typparameter ohne Formel sein). Die Typen bleiben das Vorschlagsraster.' });
    for (const k of groesseSchluessel(b, o.art)) {
      for (const [seite, label] of [['min', 'kleinste'], ['max', 'grösste']]) {
        f.push({ gruppe: 'Freie Grösse (nach Mass gefertigt)', pfad: `frei.grenzen.${k}.${seite}`, label: `${label} ${k === 'b' ? 'Breite b' : k === 'h' ? 'Höhe h' : 'Ø d'} [mm]`, typ: 'masse', text: feldText(fr?.grenzen?.[k]?.[seite]),
          deaktiviert: !fr, platzhalter: 'leer = keine Grenze',
          titel: `Grenze ${seite === 'min' ? 'nach unten' : 'nach oben'} für ${k} in mm – nur eintragen, was die Unterlage nennt (Quelle in frei.quellen). Leer = jede Grösse über 0.` });
      }
    }
    f.push({ gruppe: 'Freie Grösse (nach Mass gefertigt)', pfad: 'frei.schritt', label: 'Schritt [mm]', typ: 'ganzzahl', text: feldText(fr?.schritt), deaktiviert: !fr, platzhalter: 'leer = 1 mm',
      titel: 'Raster der Eingabe in ganzen mm (Vorgabe 1). Eine Grösse ausserhalb des Rasters lehnt der Kern mit einem gerundeten Vorschlag ab, nie still.' });
  }
  // Beschriftung (B9/B9b, Entscheid Christian 19.10.2026): Werkstoff-Kürzel der Serie; die Beschriftung («CNS NW 20», bei Abwasser «PE-S 110») setzt der Kern zusammen (Rohrgroessen.Von)
  if (o.art !== 'kanal') {
    f.push({ gruppe: 'Beschriftung', pfad: 'kuerzel', label: 'Kürzel (Werkstoff)', typ: 'text', text: typeof b.kuerzel === 'string' ? b.kuerzel : '', platzhalter: 'z. B. CNS, PE-S, PEX',
      hinweis: 'für die Beschriftung, z. B. CNS, PE-S, PEX',
      titel: 'Optional. Werkstoff-Kürzel der Rohre dieser Serie. Die Beschriftung lautet «<kuerzel> NW|DN <Nennweite>» (z. B. «CNS NW 20», NW oder DN nach Projekteinstellung), bei Abwasser «<kuerzel> <Aussendurchmesser>» (z. B. «PE-S 110»); ohne kuerzel nur die Grösse. Nur eintragen, was die Unterlage nennt. Höchstens 12 Zeichen, eine Zeile.' });
  }
  for (const a of FORMTEIL_ARTEN) {
    const cur = b.formteile?.[a.art]?.familie ?? '';
    const opt = formteilOptionen(o.familien, a.kategorie, cur);
    f.push({ gruppe: 'Formteile (Familien des Projekts)', pfad: `formteile.${a.art}.familie`, label: a.label, typ: 'auswahl', optionen: opt, wert: cur, leer: '(keine)',
      hinweis: opt.length === 0 ? `Im Projekt gibt es keine Familie der Kategorie ${a.kategorie} – «Familie laden» oder neu anlegen.` : undefined,
      titel: `Formteil-Familie für ${a.label} (Familien der Kategorie ${a.kategorie})` });
    f.push({ gruppe: 'Formteile (Familien des Projekts)', pfad: `formteile.${a.art}.typ`, label: `${a.label}: Typregel`, typ: 'typregel', text: b.formteile?.[a.art]?.typ ?? '', deaktiviert: !cur,
      platzhalter: 'leer = Typ nach den Anschlussgrössen', titel: 'Optional: Formel, die den Typnamen ergibt, z. B. if(dn = 15, "DN 15", "DN 20"). Namen: die Grössenparameter der Serie, bei T-Stück und Reduktion mit Endung _2 für Abzweig bzw. kleine Grösse.' });
    // Vorlage für freie Grössen (Nachtrag): nur bei einer Serie mit freier Grösse; der Typ der Formteil-Familie, der mit Exemplarwerten gerechnet wird, wenn kein Typ passt
    const vor = b.formteile?.[a.art]?.vorlage ?? '';
    if (b.frei || vor)
      f.push({ gruppe: 'Formteile (Familien des Projekts)', pfad: `formteile.${a.art}.vorlage`, label: `${a.label}: Vorlage (freie Grösse)`, typ: 'auswahl', optionen: formteilTypOptionen(o.familien, cur, vor), wert: vor, leer: '(erster Typ der Familie)',
        deaktiviert: !cur,
        titel: 'Nur bei freier Grösse: Typ der Formteil-Familie, der für eine freie Grösse mit Exemplarwerten gerechnet wird, wenn kein Typ passt (leer = der erste Typ). Die Anschlussgrössen (b, h bzw. d) der Formteil-Familie müssen dafür Exemplarparameter ohne Formel sein; die Einbaulängen rechnen die Formeln der Familie.' });
  }
  f.push({ gruppe: 'Regeln', pfad: 'regeln.winkel', label: 'Erlaubte Bogenwinkel', typ: 'winkel', text: b.regeln?.winkel === 'frei' ? 'frei' : zahlenListeText(b.regeln?.winkel), platzhalter: '15, 30, 45, 90  oder  frei',
    titel: 'Liste in Grad oder «frei»; leer = nicht angegeben (der Kern lässt dann jeden Winkel zu)' });
  f.push({ gruppe: 'Regeln', pfad: 'regeln.biegeradius', label: 'Biegeradius', typ: 'zahlopt', text: feldText(b.regeln?.biegeradius), platzhalter: 'optional, mm oder Formel', titel: 'Optional: Biegeradius in mm (Zahl oder Formel)' });
  f.push({ gruppe: 'Regeln', pfad: 'regeln.abzweig', label: 'Abzweig', typ: 'auswahl', optionen: [['tstueck', 'T-Stück']], wert: b.regeln?.abzweig ?? '', leer: '(nicht angegeben)' });
  f.push({ gruppe: 'Regeln', pfad: 'regeln.abzweigwinkel', label: 'Abzweigwinkel', typ: 'liste', text: zahlenListeText(b.regeln?.abzweigwinkel), platzhalter: 'leer = nur 90°', titel: 'Erlaubte Abzweigwinkel der T-Stücke in Grad (über 0 bis 90), z. B. 45, 88.5; leer = nur rechtwinklig' });
  // regeln.mischbar (31.10.2026, Christian: «geberit silent und pe können gemischt werden»): fehlt ein Formteil, nimmt der Kern das der anderen Serie
  const kat = o.art === 'kanal' ? 'Kanal' : 'Rohr';
  const mischOpt = (o.familien ?? []).filter((x) => x.kategorie === kat && x.id && x.id !== def?.id).sort((x, y) => String(x.name).localeCompare(String(y.name), 'de'))
    .map((x) => [x.id, `${x.kennzeichen} · ${x.name}`]);
  const misch = Array.isArray(b.regeln?.mischbar) ? b.regeln.mischbar[0] ?? '' : '';
  if (misch && !mischOpt.some(([id]) => id === misch)) mischOpt.push([misch, `${misch} (nicht im Projekt)`]);
  f.push({ gruppe: 'Regeln', pfad: 'regeln.mischbar', label: 'Mischbar mit', typ: 'mischbar', optionen: mischOpt, wert: misch, leer: '(keine)',
    titel: 'Andere Leitungsserie, deren Formteile der Kern nimmt, wenn die eigene Formteil-Familie keinen passenden Typ hat (z. B. Geberit PE in Silent-db20)' });
  return f;
}

/** Änderung eines Feldes (Formular → Definition): liest den Wert aus dem Eingabetext und liefert die ops für familie_aendern. → { ops } | { fehler }. */
export function leitungAenderung(def, art, pfad, typ, text) {
  if (pfad === 'querschnitt') {
    const r = querschnittWechseln(def?.leitung, def, art, text);
    return r.fehler ? { fehler: r.fehler } : { ops: r.ops };
  }
  if (pfad === 'groessenart') {
    const r = groessenartWechseln(def?.leitung, def, text);
    return r.fehler ? { fehler: r.fehler } : { ops: r.ops };
  }
  const r = leitungFeldLesen(typ, text);
  if (r.fehler) return { fehler: r.fehler };
  return { ops: leitungOps(leitungMitFeld(def?.leitung, pfad, r.wert)) };
}

// ------------------------------------------------------------------------------------------------ Kontrolltabelle (Abfrage leitungsserie)

/** Text der Grösse einer Zeile: «DN 20», «Ø 125», «400 × 200». */
export function groesseText(g, serie = null) {
  if (g?.dn !== undefined && g.dn !== null) return `DN ${feldText(g.dn)}`;
  if (g?.d !== undefined && g.d !== null) return serie?.nachAussendurchmesser ? `d ${feldText(g.d)}` : `Ø ${feldText(g.d)}`;   // Abwasser-Serie nach Aussendurchmesser: «d 110»
  if (g?.b !== undefined && g?.h !== undefined) return `${feldText(g.b)} × ${feldText(g.h)}`;
  return '–';
}

/** Zellen der Formteile einer Grösse: { bogen, tstueck, reinigungsoeffnung, reduktion } je { zustand: 'ja' | 'nein' | 'teils' | 'keine', text, titel }. */
export function serieZellen(g) {
  const ft = g?.formteile ?? {};
  // freie Grösse: das Formteil wird aus seiner Vorlage mit Exemplarwerten gerechnet (parameter) – im Tooltip
  const exemplar = (w) => (w?.frei && w.parameter && typeof w.parameter === 'object' ? 'Exemplarwerte: ' + Object.entries(w.parameter).map(([k, v]) => `${k} = ${feldText(v)}`).join(', ') : '');
  const einfach = (w) => (w?.verfuegbar
    ? { zustand: 'ja', text: 'ja', titel: [w.familie, w.typ, w.hinweis, exemplar(w)].filter(Boolean).join(' · ') }
    : { zustand: 'nein', text: 'nein', titel: [w?.grund, w?.vorschlag].filter(Boolean).join(' – ') });
  const red = ft.reduktion ?? [];
  const ok = red.filter((r) => r.verfuegbar).length;
  const reduktion = red.length === 0 ? { zustand: 'keine', text: '–', titel: 'Kleinste Grösse: keine Reduktion nötig' }
    : ok === red.length ? { zustand: 'ja', text: 'ja', titel: red.map((r) => `auf ${r.auf}: ${r.typ}${exemplar(r) ? ` (${exemplar(r)})` : ''}`).join(' · ') }
      : { zustand: ok === 0 ? 'nein' : 'teils', text: ok === 0 ? 'nein' : `${ok} von ${red.length}`, titel: red.filter((r) => !r.verfuegbar).map((r) => `auf ${r.auf}: ${r.grund}`).join(' – ') };
  return { bogen: einfach(ft.bogen), tstueck: einfach(ft.tstueck), reduktion, reinigungsoeffnung: einfach(ft.reinigungsoeffnung) };
}

/** Gründe, warum Formteile nicht verfügbar sind, je Art (und Reduktion je Zielgrösse, T-Stück je Abzweigwinkel) zusammengefasst:
 *  [{ art, label, groessen: [Typ …], alle, grund, vorschlag, mehrere }] – der Grund nennt oft die Grössen («a = DN 32, b = DN 15»); gibt es mehrere verschiedene Texte, steht der erste
 *  (mehrere: true, «z. B.»), alle stehen im Tooltip der Zelle. Reihenfolge: Bogen, T-Stück, Reduktion, Reinigungsöffnung. */
export function serieGruende(serie) {
  const gueltige = (serie?.groessen ?? []).filter((g) => g.gueltig !== false);
  const gruppen = new Map();
  const nimm = (art, label, typ, w, zusatz = '') => {
    if (!w || w.verfuegbar) return;
    const k = `${art}|${zusatz}`;
    if (!gruppen.has(k)) gruppen.set(k, { art, label: label + zusatz, groessen: [], grund: w.grund, vorschlag: w.vorschlag ?? '', mehrere: false, zusatz });
    const x = gruppen.get(k);
    if (x.grund !== w.grund) x.mehrere = true;
    x.groessen.push(typ);
  };
  for (const g of gueltige) {
    nimm('bogen', 'Bogen', g.typ, g.formteile?.bogen);
    nimm('tstueck', 'T-Stück', g.typ, g.formteile?.tstueck);
    for (const w of g.formteile?.tstueckJeWinkel ?? []) nimm('tstueck', 'T-Stück', g.typ, w, ` (Abzweig ${feldText(w.winkel)}°)`);
    for (const r of g.formteile?.reduktion ?? []) nimm('reduktion', 'Reduktion', g.typ, r, ` auf ${r.auf}`);
    nimm('reinigungsoeffnung', 'Reinigungsöffnung', g.typ, g.formteile?.reinigungsoeffnung);
  }
  const reihenfolge = FORMTEIL_ARTEN.map((a) => a.art);
  return [...gruppen.values()].sort((x, y) => reihenfolge.indexOf(x.art) - reihenfolge.indexOf(y.art)).map(({ zusatz, ...x }) => ({ ...x, alle: x.groessen.length === gueltige.length }));
}


export function neueFamiliePruefen(felder, vorhandene) {
  if (!felder.kategorie) return 'Kategorie wählen.';
  const name = String(felder.name ?? '').trim();
  if (!name) return 'Name fehlt.';
  if ((vorhandene ?? []).some((n) => String(n).trim().toLowerCase() === name.toLowerCase())) return `Eine Familie «${name}» gibt es schon.`;
  return true;
}

/** Dateiname/Pfad für familie_laden: leer → Fehler. */
export function ladenPruefen(datei) {
  return String(datei ?? '').trim() ? true : 'Datei oder Pfad angeben.';
}

/** Dateinamen aus dem Vorschlag des Kerns («Bibliothek kataloge/familien: a.familie.json, b.familie.json»). Seit Modul 2, C3 nicht mehr benutzt: «Familie laden» liest die Liste aus familien_bibliothek (bibliothek-dialoge.js). */
export function bibliotheksDateien(vorschlag) {
  const m = /Bibliothek[^:]*:\s*(.+)$/.exec(String(vorschlag ?? ''));
  return m ? m[1].split(',').map((s) => s.trim()).filter((s) => /\.familie\.json$/i.test(s)) : [];
}
