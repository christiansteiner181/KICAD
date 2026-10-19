// Hilfe (F1) – reine Teile, ohne DOM: Schlüssel der Sprungziele und Bau der Adresse der Bedienungsanleitung.
// Die Bedienungsanleitung ist eine Webseite, die KICAD selbst unter /hilfe/ ausliefert (Server: AnleitungEndpunkte.cs; gebaut mit
// werkzeuge/anleitung_bauen.mjs, Ordner in kataloge/buero.json › anleitung.ordner); die Oberfläche öffnet sie nur.
// Den Rest (Symbol «?», F1, Fenster öffnen, Meldungen) macht hilfe-oberflaeche.js.
//
// Sprungziele der Seite (sie rechnet den Schlüssel identisch, Abweichung = Sprung ins Leere):
//   /hilfe/                                  Anfang der Anleitung
//   /hilfe/#k-<register>~<knopf>             Funktion zu einem Knopf des Menübands (Register und Knopftitel wie angezeigt)
//   /hilfe/#s-<suchwörter>                   Suche, z. B. nach dem Titel des offenen Dialogs

/** Schlüssel eines Titels: klein, ä→ae ö→oe ü→ue ß→ss, Akzente weg, alles andere zu «-». IDENTISCH mit der Anleitungsseite (werkzeuge/anleitung/vorlage.html). */
export const hilfeSchluessel = t => String(t || '').toLowerCase().replace(/ä/g, 'ae').replace(/ö/g, 'oe').replace(/ü/g, 'ue').replace(/ß/g, 'ss')
  .normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');

/** Letzte Zeile im Tooltip der Menüband-Knöpfe. */
export const HILFE_ZEILE = 'F1: Hilfe zu diesem Befehl';
/** Tooltip des Symbols «?» in der Kopfleiste. */
export const HILFE_TOOLTIP = 'Hilfe – Bedienungsanleitung (F1)';
/** Meldungen, wenn die Anleitung nicht geöffnet werden kann. */
export const HILFE_NICHT_EINGERICHTET = 'Die Bedienungsanleitung ist auf diesem Rechner noch nicht erstellt.';
/** Adresse der Anleitung auf dem Server der Oberfläche (gleicher Ursprung wie die Oberfläche selbst). */
export const HILFE_URL = '/hilfe/';
export const hilfeBlockiert = (adresse) => `Das Hilfefenster wurde blockiert – Adresse: ${adresse}`;

/** Name des Fensters der Anleitung: immer dasselbe (window.open öffnet nie ein zweites). */
export const HILFE_FENSTER = 'kicad-hilfe';

/**
 * Wählt den Kontext der Hilfe: ein offener Dialog geht vor (er ist bedienbar, der Zeiger steht oft noch auf dem Knopf, der ihn geöffnet hat),
 * dann ein Knopf des Menübands (Zeiger darüber oder Tastaturfokus), sonst keiner.
 * dialogTitel: Titel des obersten Dialogs oder null · knopf: { register, knopf } (Titel wie angezeigt) oder null.
 * Ergebnis: { art: 'dialog', titel } | { art: 'knopf', register, knopf } | { art: 'keiner' }.
 */
export function hilfeKontext({ dialogTitel = null, knopf = null } = {}) {
  if (dialogTitel != null && String(dialogTitel).trim() !== '') return { art: 'dialog', titel: String(dialogTitel) };
  if (knopf && String(knopf.knopf ?? '').trim() !== '') return { art: 'knopf', register: String(knopf.register ?? ''), knopf: String(knopf.knopf) };
  return { art: 'keiner' };
}

/**
 * Adresse der Anleitung für einen Kontext (siehe hilfeKontext), z. B. '/hilfe/#k-ansicht~serien-pdf'. Fehlt die Basisadresse (Anleitung auf diesem Rechner
 * nicht erstellt), ergibt das null (→ Meldung «noch nicht erstellt»).
 * Ein Fragment der Basisadresse wird verworfen. Ergibt der Titel keinen Schlüssel (nur Sonderzeichen), bleibt es bei der Basisadresse.
 */
export function hilfeAdresse(basis, kontext = { art: 'keiner' }) {
  const b = String(basis ?? '').trim().split('#')[0];
  if (b === '') return null;
  if (kontext?.art === 'knopf') {
    const k = hilfeSchluessel(kontext.knopf);
    if (k === '') return b;
    const r = hilfeSchluessel(kontext.register);
    return `${b}#k-${r === '' ? '' : r + '~'}${k}`;
  }
  if (kontext?.art === 'dialog') {
    const s = hilfeSchluessel(kontext.titel);
    return s === '' ? b : `${b}#s-${s}`;
  }
  return b;
}
