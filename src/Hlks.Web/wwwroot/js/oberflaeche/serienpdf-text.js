// Serien-PDF und Plansätze (entwurf/serien-pdf.md): reine Hilfen für Dialog und Projektbrowser – Zeilen aus der Antwort des Kerns,
// Verschieben, Parameter für den Kern, Texte. Keine Fachlogik: Gewerk-Vorschlag, Reihenfolge nach Plannummer, Namen und Ordner kommen
// fertig aus der Abfrage plansaetze; hier wird nur dargestellt und die Eingabe des Menschen (Häkchen, Reihenfolge, Anlass, Ordner) in
// Parameter für plansatz_pdf übersetzt. Läuft ohne Browser (Node-Test: serienpdf.test.mjs).

/** Maskiert Text für HTML. */
export const esc = (t) => String(t ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

// ---------------------------------------------------------------------------------------------- Plansätze der Abfrage

/** Plansätze der Antwort von plansaetze: automatische (Reihenfolge des Kerns) und eigene getrennt. */
export function satzListe(antwort) {
  const alle = antwort?.plansaetze ?? [];
  return { automatische: alle.filter((s) => s.automatisch), eigene: alle.filter((s) => !s.automatisch) };
}

/** Zeile der Liste links: «Plansatz Sanitär», «2 von 3», angeboten (noch nicht angelegt) vermerkt. */
export function satzZeile(s) {
  return { schluessel: s.schluessel, name: s.name, dabei: s.dabei, anzahl: s.anzahl, angeboten: !s.angelegt, automatisch: !!s.automatisch,
    zahlText: s.anzahl === s.dabei ? String(s.anzahl) : `${s.dabei} von ${s.anzahl}` };
}

/** Findet den Satz zum Schlüssel; nach dem Anlegen eines angebotenen Satzes heisst er PS-n statt auto:<gewerk>: dann über Gewerk und automatisch. */
export function findeSatz(antwort, schluessel, gewerk = null) {
  const alle = antwort?.plansaetze ?? [];
  const g = typeof schluessel === 'string' && schluessel.startsWith('auto:') ? schluessel.slice(5) : gewerk;
  return alle.find((s) => s.schluessel === schluessel) ?? alle.find((s) => s.plansatz && s.plansatz === schluessel)
    ?? (g ? alle.find((s) => s.automatisch && s.gewerk === g) : null) ?? null;
}

/** Bearbeitbare Zeilen eines Satzes (Kopie der Blätter in der Reihenfolge des Kerns). */
export function zeilen(satz) {
  return (satz?.blaetter ?? []).map((b) => ({
    planblatt: b.planblatt, nummer: b.nummer, nummerKurz: b.nummerKurz, titel: b.titel, format: b.format, ausrichtung: b.ausrichtung,
    gewerk: b.gewerk, gewerkName: b.gewerkName, gewerkHerkunft: b.gewerkHerkunft, gewerkQuelle: b.gewerkQuelle,
    dabei: !!b.dabei, ansichten: b.ansichten, einzelDatei: b.einzelDatei,
  }));
}

/** Verschiebt die Zeile von nach nach (Ziehen); ausserhalb des Bereichs oder gleich: unverändert. Gibt eine neue Liste zurück. */
export function schieben(liste, von, nach) {
  if (von === nach || von < 0 || nach < 0 || von >= liste.length || nach >= liste.length) return liste;
  const neu = liste.slice();
  const [z] = neu.splice(von, 1);
  neu.splice(nach, 0, z);
  return neu;
}
export const hoch = (liste, i) => schieben(liste, i, i - 1);
export const runter = (liste, i) => schieben(liste, i, i + 1);

/** Nach dem Neuladen (z. B. Gewerk eines Blatts geändert): die noch nicht gespeicherte Reihenfolge und die Häkchen des Menschen bleiben,
 *  was weg ist, entfällt, was neu ist, kommt in der Reihenfolge des Kerns dazu (dabei, wie es der Kern meldet). */
export function zusammenfuehren(alt, neu) {
  const neuVon = new Map(neu.map((z) => [z.planblatt, z]));
  const behalten = alt.filter((z) => neuVon.has(z.planblatt)).map((z) => ({ ...neuVon.get(z.planblatt), dabei: z.dabei }));
  const bekannt = new Set(behalten.map((z) => z.planblatt));
  const dazu = neu.filter((z) => !bekannt.has(z.planblatt));
  // neue Blätter dort einfügen, wo der Kern sie hat: vor das nächste behaltene Blatt, das im Kern danach kommt
  const rang = new Map(neu.map((z, i) => [z.planblatt, i]));
  for (const z of dazu) {
    const i = behalten.findIndex((b) => rang.get(b.planblatt) > rang.get(z.planblatt));
    if (i < 0) behalten.push(z); else behalten.splice(i, 0, z);
  }
  return behalten;
}

export const gewaehlte = (liste) => liste.filter((z) => z.dabei);

/** «3 von 5 Plänen gewählt». */
export function zaehlText(liste) {
  const n = gewaehlte(liste).length;
  return `${n} von ${liste.length} ${liste.length === 1 ? 'Plan' : 'Plänen'} gewählt`;
}

/** Seitenzahl im Sammel-PDF und Zahl der Einzel-PDFs für die Zusammenfassung unten. */
export function umfangText(liste, einzeln) {
  const n = gewaehlte(liste).length;
  if (n === 0) return 'Kein Plan gewählt.';
  return einzeln ? `1 Sammel-PDF mit ${n} ${n === 1 ? 'Seite' : 'Seiten'} und ${n} Einzel-PDF${n === 1 ? '' : 's'}` : `1 Sammel-PDF mit ${n} ${n === 1 ? 'Seite' : 'Seiten'}`;
}

// ---------------------------------------------------------------------------------------------- Parameter für den Kern

/** Parameter von plansatz_pdf aus dem Dialog. Der Ordner geht nur mit, wenn ihn der Mensch von Hand gesetzt hat; sonst gilt der Vorschlag
 *  des Kerns (leer = Vorschlag). Reihenfolge und Häkchen gehen vollständig mit: der Kern speichert sie im Plansatz. */
export function exportParameter(satz, liste, form, extra = {}) {
  return {
    plansatz: satz.schluessel,
    blaetter: liste.map((z) => z.planblatt),
    abgewaehlt: liste.filter((z) => !z.dabei).map((z) => z.planblatt),
    anlass: (form.anlass ?? '').trim(),
    ordner: form.ordnerVonHand ? (form.ordner ?? '').trim() : '',
    einzeln: !!form.einzeln,
    ...extra,
  };
}

/** Prüfung vor dem Export; Text des Hindernisses oder true. */
export function exportPruefen(liste, form) {
  if (gewaehlte(liste).length === 0) return 'Mindestens ein Plan muss gewählt sein.';
  if (form.ordnerVonHand && !(form.ordner ?? '').trim()) return 'Der Ordner ist leer. «Vorschlag» setzt den vorgeschlagenen Ordner ein.';
  return true;
}

/** Zustand für einen Satz, wenn er zum ersten Mal gezeigt wird: Werte aus der gespeicherten Ausgabe, sonst der Vorschlag des Kerns. */
export function formularAus(satz) {
  const a = satz?.ausgabe ?? {};
  const vorschlag = satz?.vorschlag ?? {};
  const ordnerVonHand = typeof a.ordner === 'string' && a.ordner.length > 0;
  return { anlass: a.anlass ?? '', ordner: ordnerVonHand ? a.ordner : (vorschlag.ordner ?? ''), ordnerVonHand, einzeln: a.einzeln === true };
}

/** Parameter von plansatz_setzen für einen Handsatz mit geänderter Blattliste. */
export function blattlisteParameter(satz, planblaetter) {
  return { plansatz: satz.schluessel, blaetter: planblaetter };
}

// ---------------------------------------------------------------------------------------------- Texte

export function groesseText(bytes) {
  if (bytes === null || bytes === undefined) return '';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(bytes < 10240 ? 1 : 0).replace('.', ',')} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1).replace('.', ',')} MB`;
}

export function dauerText(ms) {
  if (ms === null || ms === undefined) return '';
  return ms < 1000 ? `${ms} ms` : `${(ms / 1000).toFixed(1).replace('.', ',')} s`;
}

/** Kurztext zur Herkunft des Gewerks: gesetzt oder Vorschlag mit Quelle. */
export function gewerkTip(z) {
  return z.gewerkHerkunft === 'gesetzt' ? `${z.gewerkName}: von Hand gesetzt` : `${z.gewerkName}: Vorschlag (${z.gewerkQuelle}) – «bestätigen» setzt es fest`;
}

/** Überschrift rechts im Dialog. */
export function satzTitel(s) {
  if (!s) return '';
  return s.automatisch ? `${s.name} · automatisch, Gewerk ${s.gewerkName}${s.angelegt ? '' : ' · noch nicht angelegt'}` : `${s.name} · von Hand zusammengestellt`;
}

/** Fehlermeldung der Abfrage mit Vorschlag in einem Satz. */
export function fehlerText(e) {
  const t = e?.message ?? String(e);
  return e?.vorschlag ? `${t} – ${e.vorschlag}` : t;
}

/** Text der Rückfrage, wenn Dateien schon im Ordner liegen (Antwort der Prüfung mit pruefen = true). */
export function ueberschreibenText(antwort) {
  const l = (antwort?.vorhanden ?? []).map((p) => String(p).split(/[\\/]/).pop());
  const zeigen = l.slice(0, 6).join('\n') + (l.length > 6 ? `\n… und ${l.length - 6} weitere` : '');
  return `${l.length === 1 ? 'Diese Datei gibt' : `Diese ${l.length} Dateien geben`} es im Ordner schon:\n\n${zeigen}\n\nÜberschreiben?`;
}

/** HTML der Dateiliste nach dem Export. */
export function dateiListeHtml(antwort) {
  const zeilen_ = (antwort?.dateien ?? []).map((d) => `<tr class="sp-datei" data-art="${esc(d.art)}"><td class="sp-d-art">${d.art === 'sammel' ? 'Sammel-PDF' : 'Einzel-PDF'}</td>`
    + `<td class="sp-d-name" title="${esc(d.datei)}">${esc(d.name)}</td><td class="sp-d-seiten">${d.seiten} ${d.seiten === 1 ? 'Seite' : 'Seiten'}</td>`
    + `<td class="sp-d-gr">${esc(groesseText(d.groesseBytes))}</td></tr>`);
  return `<table class="tab sp-dateien"><thead><tr><th>Art</th><th>Datei</th><th>Umfang</th><th>Grösse</th></tr></thead><tbody>${zeilen_.join('')}</tbody></table>`;
}

/** Zusammenfassung nach dem Export: «2 Dateien, 3 Seiten im Sammel-PDF, 215 KB, 0,8 s». */
export function ergebnisText(antwort) {
  const n = (antwort?.dateien ?? []).length;
  return `${n} ${n === 1 ? 'Datei' : 'Dateien'} geschrieben · ${antwort.seiten} ${antwort.seiten === 1 ? 'Seite' : 'Seiten'} im Sammel-PDF · ${groesseText(antwort.groesseBytes)} · ${dauerText(antwort.dauerMs)}`;
}

// ---------------------------------------------------------------------------------------------- Projektbrowser

/** Planblätter nach Gewerk gruppiert (für den Knoten «Pläne»): aus den automatischen Plansätzen der Abfrage, je Gewerk die Blätter in
 *  der Reihenfolge des Kerns. Jedes Blatt steht in genau einer Gruppe. */
export function gewerkGruppen(antwort) {
  return satzListe(antwort).automatische.map((s) => ({
    gewerk: s.gewerk, name: s.gewerkName, schluessel: s.schluessel,
    blaetter: s.blaetter.map((b) => ({ planblatt: b.planblatt, nummer: b.nummer, titel: b.titel, format: b.format, gewerk: b.gewerk, gewerkName: b.gewerkName,
      gewerkHerkunft: b.gewerkHerkunft, gewerkQuelle: b.gewerkQuelle })),
  }));
}

/** Auskunft zum Gewerk eines Blatts aus der Antwort (für Kontextmenü und Tooltip), oder null. */
export function blattGewerk(antwort, planblatt) {
  for (const g of gewerkGruppen(antwort)) {
    const b = g.blaetter.find((x) => x.planblatt === planblatt);
    if (b) return b;
  }
  return null;
}

/** Ändert ein Ereignis des Modells etwas, das die Gruppierung betrifft? (Klassen der geänderten Elemente; Löschungen immer) */
export function betrifftPlaene(d, klasseVon) {
  if (!d || d.alles) return true;
  if ((d.geloescht ?? []).length > 0) return true;
  const wichtig = new Set(['Planblatt', 'Plansatz', 'Ansichtsfenster', 'Ansicht', 'Projektinformation']);
  return (d.ids ?? []).some((id) => wichtig.has(klasseVon(id)));
}
