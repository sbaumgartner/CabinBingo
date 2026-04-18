# Cabin guest seed items

Each `guest_*.json` file is a DynamoDB `PutItem` payload for the **CabinGuests** table. `Seed.ps1` uploads every `guest_*.json` in this folder.

## Roster (stable `guestId` → display name)

| `guestId` | Display name |
|-----------|----------------|
| `guest_steve` | Steve |
| `guest_kendra` | Kendra |
| `guest_justin_bang` | Justin! |
| `guest_dave` | Dave |
| `guest_kurt` | Kurt |
| `guest_annmarie` | AnnMarie |
| `guest_matt_bang` | Matt! |
| `guest_haley_bang` | Haley! |
| `guest_ziel` | Ziel |
| `guest_marissa_bang` | marissa! |
| `guest_movie` | Movie |
| `guest_travis` | Travis |
| `guest_evan` | Evan |
| `guest_hannah` | Hannah |
| `guest_jim` | Jim |
| `guest_emily` | Emily |
| `guest_teeroy` | Teeroy |
| `guest_marcy` | Marcy |
| `guest_carr` | Carr |
| `guest_samantha` | Samantha |
| `guest_chris` | Chris |
| `guest_bread` | Bread |
| `guest_ben` | Ben |
| `guest_maiya_bang` | maiya! |
| `guest_matt` | Matt |
| `guest_lindsey` | Lindsey |
| `guest_ron` | Ron |
| `guest_vic` | Vic |
| `guest_maria` | Maria |

**Note:** `Matt!` and `Matt` are two rows (`guest_matt_bang` vs `guest_matt`). Display strings match your list (including capitalization and `!`).

## Replacing an already-seeded table

`PutItem` overwrites the same `guestId`. Old IDs (e.g. `guest_alex`) are **not** removed automatically—delete obsolete items in the DynamoDB console or leave them inactive (`active: false`) if you add that field later.

If anyone had already claimed a **removed** `guestId`, clear `claimedBySub` on that row and fix their `UserData` profile manually (see `OPs.md`).
