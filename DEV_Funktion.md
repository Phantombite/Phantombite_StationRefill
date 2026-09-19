# DEV Funktion — Phantombite StationRefill

Stand: 2026-09-19 · Version 1.0.0 · Workshop-ID 3723483728 · Core-Kanal 1995016

## Zweck
Hält statische Stationen bestimmter Fraktionen dauerhaft versorgt: Reaktoren bekommen Uran, Geschütze Munition.
Ersetzt das frühere `Core_StationRefill` (aus dem Core ausgezogen, alte Datei liegt nur noch als `.disabled` im Mods-Ordner).

## Ablauf
1. Beim `READY` des Core: Config laden, Fraktions-Mitglieder sammeln, statische Grids suchen, deren Besitzer
   zur Fraktion gehören (`BigOwners`), alle Reaktoren und Geschütze merken, einmal auffüllen.
2. Danach alle `IntervalHours` Stunden erneut auffüllen (`IntervalHours × 216000` Ticks).
3. Aufgefüllt wird bis zum maximalen Inventarvolumen.

## Konfiguration
`StationRefill_Config.ini` im World-Storage (wird beim ersten Start angelegt, nur auf dem Server):
```
[Settings]
IntervalHours=5
AmmoSubtype=RapidFireAutomaticRifleGun_Mag_50rd
FactionTags=SPT          (kommagetrennt)
```

## Commands (`!pbc stationrefill ...`, beide nur Admin)
| Command | Wirkung |
|---|---|
| `refill` | Alle Stationen sofort auffüllen |
| `rescan` | Stationen neu suchen und auffüllen |

## Core-Anbindung
Meldet sich als `stationrefill` an, empfängt `READY`, `LOGLEVEL`, `PERFLEVEL`, `CMD`.
**Performance-Level 1** (Maximum): Statt alles auf einmal zu füllen, wird eine Warteschlange aufgebaut und
pro Tick mit 5 Aufgaben abgearbeitet.

## Dateien
`Core/StationRefill_Session.cs` (Einstieg, Commands, Core-Nachrichten), `Modules/StationRefill_Main.cs` (Suche und Auffüllen),
`StationRefill_FileManager.cs` (Config), `StationRefill_Logger.cs`, `StationRefill_ModuleManager.cs`, `StationRefill_IModule.cs`.

## Offene Punkte / Roadmap
- [ ] Angleichen an `0_Phantombite_MOD_TEMPLATE.md` (patch_notes, thumb.jpg)
- [ ] Neue Stationen, die erst nach dem Start entstehen, werden nur per `rescan` erfasst
