# Phantombite StationRefill

Hält **statische Stationen** bestimmter Fraktionen dauerhaft versorgt: Reaktoren bekommen Uran, Geschütze Munition. Praktisch für
NPC-Stationen, damit sie nie leerlaufen.

## Funktionen
- Sucht statische Grids, deren Besitzer zu den eingestellten Fraktionen gehören
- Füllt Reaktoren (Uran) und Geschütze (Munition) bis zum maximalen Inventarvolumen auf
- Beim Start einmal, danach im eingestellten Abstand
- Reagiert auf die Server-Last: bei Überlast verteilt der Mod die Arbeit auf mehrere Ticks (über den Phantombite Core)

## Commands (nur Admin)
```
!pbc stationrefill <command>
```
| Command | Beschreibung |
|---|---|
| `refill` | Alle Stationen sofort auffüllen |
| `rescan` | Stationen neu suchen und auffüllen |

## Konfiguration
`StationRefill_Config.ini` im World-Storage (wird beim ersten Start angelegt):
```
[Settings]
IntervalHours=5
AmmoSubtype=RapidFireAutomaticRifleGun_Mag_50rd
FactionTags=SPT
```
`FactionTags` ist kommagetrennt, z. B. `SPT,SPRT`.

## Voraussetzungen
- **Phantombite Core** (Commands)

Workshop-ID: 3723483728
