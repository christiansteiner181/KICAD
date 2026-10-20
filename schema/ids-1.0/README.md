# IDS 1.0 – offizielles XML-Schema (Information Delivery Specification)

Für Modul 7 Teil B2 (IFC-Export mit IDS-Prüfung). Die IDS-Dateien in `kataloge/ids/` und fremde IDS (Bauherr, Architekt) werden
vor der Prüfung gegen dieses Schema geprüft (`IdsSchema` in `src/Hlks.Kern/IfcExport/IdsPruefer.cs`).

## ids.xsd – unverändert

- Quelle: https://github.com/buildingSMART/IDS, Tag `v1.0.0` (Release «v1.0 Final buildingSMART Standard», 03.06.2024),
  Commit `1effec6f419798ce09617416d258a35bdc58320a`, Datei `Development/ids.xsd` (`version="1.0.0"`)
- Geladen: 20.10.2026 (Agent hlks-kern im Auftrag des Hauptlaufs) über
  `https://raw.githubusercontent.com/buildingSMART/IDS/1effec6f419798ce09617416d258a35bdc58320a/Development/ids.xsd`
- Prüfsummen: SHA-256 `8975DC18BD18F08A345A430A22BF317A54D94671B7DB01400042FA43B6C0D1F3`,
  Git-Blob `edab179d836d0c3e9b21d93c5e2b29f86ec56885` – gleich dem Blob im Repository von buildingSMART (GitHub-API), 13 158 Bytes
- Lizenz: © buildingSMART International Ltd., Creative Commons Attribution-NoDerivatives 4.0 International
  (`LICENSE.buildingSMART.txt`, aus demselben Commit) – Weitergabe nur unverändert und mit Quellenangabe; **diese Datei nicht ändern**.
- Dokumentation der Facetten: https://github.com/buildingSMART/IDS/tree/v1.0.0/Documentation

## Importe von ids.xsd (nur lokal aufgelöst, nichts aus dem Internet)

`ids.xsd` importiert drei Schemas des W3C. `IdsSchema.LokalAufloesen` liefert sie aus diesem Ordner:

| Import in ids.xsd | geliefert | Bemerkung |
| --- | --- | --- |
| `http://www.w3.org/2001/xml.xsd` | `xml.xsd` | W3C, unverändert; geladen 20.10.2026 von https://www.w3.org/2001/xml.xsd, SHA-256 `61960FB3131E38022CAAD5360E2F33A3382578AB3C80CD58BD74320EDE61B20C`, Git-Blob `aea7d0db0a423b962247aa2b4d3d48fc73cda659` (Nutzungsbedingungen im Kopf der Datei: W3C Document License) |
| `http://www.w3.org/2001/XMLSchema.xsd` | `XMLSchema-teil.xsd` | KICAD: nur xs:restriction und xs:occurs, die ids.xsd braucht – .NET kann das vollständige W3C-Metaschema nicht übersetzen (Fehler «Pattern constraining facet is prohibited for anySimpleType», geprüft 20.10.2026); Kommentar in der Datei |
| `http://www.w3.org/2001/XMLSchema-instance` | leeres Schema | in .NET eingebaut (ein eigenes Schema dieses Namensraums lehnt .NET ab) |

## Prüfer

Eigener Prüfer für die Facetten entity, attribute, property, classification, partOf (dazu material einfach) – Begründung und Umfang
im Kopf von `IdsPruefer.cs`. Das Paket Xbim.IDS.Validator.Core 1.0.187 passt technisch zu Xbim.Essentials 6.1.605, steht aber unter
AGPL-3.0-only (Entscheid Christian offen).
