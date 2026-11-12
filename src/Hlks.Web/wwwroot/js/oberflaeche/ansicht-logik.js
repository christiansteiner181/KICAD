// Ansichten duplizieren und aus einer Ansichtsvorlage anlegen: die Parameter für element_anlegen (rein, ohne DOM; aktionen-ansicht.js sendet sie).

const kopie = (x) => JSON.parse(JSON.stringify(x));

/** Parameter für die Kopie einer Ansicht: alle Werte, das Geschoss und – bei Schnitt und Ansicht – die Schnittlinie (ohne sie hätte die
 *  Kopie keine Geometrie und liesse sich nicht öffnen: «… hat keine Schnittlinie»). Eine Vorlage bleibt ohne Linie, sie wird nie gezeichnet. */
export function duplikatParameter(a, kennzeichen, name) {
  const param = { klasse: 'Ansicht', kennzeichen, name, eigenschaften: kopie(a.werte ?? {}) };
  if (a.geschoss) param.geschoss = a.geschoss;
  if (a.geometrie && a.werte?.vorlage !== true) param.geometrie = kopie(a.geometrie);
  return param;
}

/** Was eine neue Ansicht der Art «art» zusätzlich zu den Feldern des Dialogs (Massstab, Detailgrad, Gewerke) aus der Vorlage übernimmt. */
export function vorlageEigenschaften(v, art) {
  const t = v?.werte ?? {};
  const eig = { ansichtsvorlage: v.kennzeichen };
  if (art === 'grundriss' && t.schnitthoehe !== undefined) eig.schnitthoehe = t.schnitthoehe;
  if (art === 'grundriss' && t.schnitthoeheNachSia !== undefined) eig.schnitthoeheNachSia = t.schnitthoeheNachSia;   // N21
  if (Array.isArray(t.klassenAusblenden)) eig.klassenAusblenden = kopie(t.klassenAusblenden);
  if (t.ifcDarstellung !== undefined) eig.ifcDarstellung = t.ifcDarstellung;   // N12, wie «Vorlage anwenden»
  return eig;
}
