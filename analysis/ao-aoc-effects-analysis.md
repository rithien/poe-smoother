# Analiza plików .ao/.aoc - Efekty animacji i cząsteczek

## Podsumowanie

Pliki `.ao` (Animation Object) i `.aoc` (Animation Object Client) definiują animowane obiekty w grze wraz z ich efektami wizualnymi. Stanowią znaczącą część zasobów gry i mają duży wpływ na wydajność.

## Statystyki

| Typ pliku | Liczba plików |
|-----------|---------------|
| `.ao`     | 59,480        |
| `.aoc`    | 59,541        |
| **RAZEM** | **119,021**   |

## Struktura plików

### Plik .ao (Animation Object)
Definiuje logikę animacji:
```
version 2
extends "Metadata/Parent"

AnimationController
{
    metadata = "path/to/rig.amd"
    default_animation = "idle"
    animations = '[...]'
}

AttachedAnimatedObject
{
    attached_object = "bone_name path/to/attachment.ao"
}
```

### Plik .aoc (Animation Object Client)
Definiuje efekty klienckie (wizualne):
```
version 2
extends "Metadata/Parent"

ClientAnimationController
{
    skeleton = "path/to/rig.ast"
}

SkinMesh
{
    skin = "path/to/mesh.sm"
}

BoneGroups
{
    bone_group = "name false bone1 bone2"
}

SoundEvents
{
    animations = '[...]'
}

ParticleEffects
{
    continuous_effect = "emitter path/to/effect.pet"
    animations = '[...]'
}

TrailsEffects
{
    animations = '[...]'
}

Lights
{
}

WindEvents
{
}
```

## Kluczowe sekcje wpływające na wydajność

| Sekcja | Wpływ na wydajność | Opis |
|--------|-------------------|------|
| `ParticleEffects` | **WYSOKI** | Systemy cząsteczek - główny koszt GPU |
| `Lights` | **WYSOKI** | Dynamiczne światła - koszt renderowania |
| `TrailsEffects` | **ŚREDNI** | Ślady za obiektami |
| `SoundEvents` | **NISKI** | Efekty dźwiękowe |
| `WindEvents` | **NISKI** | Symulacja wiatru |

---

## Kategorie efektów

### 1. Efekty środowiskowe (Environmental)

#### Mgła/Mist/Dust (252 pliki)
Ścieżki:
- `metadata/terrain/doodads/*/fog*.aoc`
- `metadata/terrain/doodads/*/mist*.aoc`
- `metadata/terrain/doodads/*/dust*.aoc`
- `metadata/terrain/doodads/vaal_sidearea_effects/`

Przykład struktury:
```
ParticleEffects
{
    continuous_effect = "mist Metadata/Particles/dusty_ground/dust.pet"
}
```

**Rekomendacja: USUNĄĆ CAŁKOWICIE**
- Nie wpływają na gameplay
- Czysto dekoracyjne
- Znaczny koszt GPU przy wielu instancjach

#### Glowworms/Świetliki (14+ plików)
Ścieżki:
- `metadata/terrain/cave/fills/cave_glowworms_*.aoc`
- `metadata/terrain/doodads/cave/glowworms_*.aoc`

**Rekomendacja: USUNĄĆ CAŁKOWICIE**
- Czysto atmosferyczne
- Particle emitters działające ciągle

#### Waterfalls/Wodospady (142 pliki)
Ścieżki:
- `metadata/terrain/*/waterfall*.aoc`
- `metadata/terrain/ruinedcity/stream/*waterfall*.aoc`

**Rekomendacja: UPROŚCIĆ**
- Zachować mesh, usunąć ParticleEffects
- Zmniejszyć liczbę cząsteczek o 50-75%

---

### 2. Efekty ognia i pochodni (Torch/Fire) (1,637+ plików)

Ścieżki:
- `metadata/terrain/*/torch*.aoc`
- `metadata/terrain/doodads/*/fire*.aoc`
- `metadata/effects/spells/*fire*.aoc`

Struktura typowa:
```
ParticleEffects
{
    continuous_effect = "torch Metadata/Particles/enviro_effects/torches/encam_torch.pet"
}

Lights
{
}
```

**Rekomendacja: CZĘŚCIOWE USUNIĘCIE**
- Usunąć ParticleEffects (płomienie)
- Zachować Lights (dla atmosfery oświetlenia) LUB
- Opcja "Performance": usunąć wszystko

---

### 3. Ground Effects (179 plików)

Ścieżki:
- `metadata/effects/spells/ground_effects/`

Typy:
| Typ | Pliki | Wpływ na gameplay |
|-----|-------|-------------------|
| fire | 4 | TAK - damage |
| ice | 1 | TAK - slow |
| lava | 4 | TAK - damage |
| caustic | 6 | TAK - damage |
| lightning | 1 | TAK - damage |
| holy | 3 | TAK - buff |
| evil | 6 | TAK - debuff |
| fog/smoke | 10+ | NIE - tylko visual |
| haste | 3 | TAK - buff |

**Rekomendacja: UPROŚCIĆ (nie usuwać)**
- Ground effects mają znaczenie dla gameplay (damage zones)
- Zmniejszyć liczbę cząsteczek
- Zmniejszyć complexity animacji
- Usunąć nadmiarowe warianty (01, 02, 03 -> jedna wersja)

---

### 4. Blood/Gore Effects (1,323 pliki)

Ścieżki:
- `metadata/effects/spells/*blood*.aoc`
- `metadata/terrain/*blood*.aoc`
- `metadata/effects/*gore*.aoc`

**Rekomendacja: CZĘŚCIOWE USUNIĘCIE**

Do usunięcia (dekoracyjne):
- `*bloodpool*.aoc` - statyczne plamy krwi
- `*bloodstream*.aoc` - strumienie krwi
- `*gorespike*.aoc` - kolce dekoracyjne
- `*_blood.aoc` warianty wodospadów

Zachować (wpływ na gameplay):
- Efekty związane ze skillami (blood skills)
- Wizualne wskaźniki damage

---

### 5. Aura Effects (666 plików)

Ścieżki:
- `metadata/effects/microtransactions/aura/`
- `metadata/effects/spells/player_auras/`
- `metadata/effects/spells/curse_aura/`

**Rekomendacja: UPROŚCIĆ**
- Nie usuwać - potrzebne do identyfikacji aur
- Zmniejszyć ParticleEffects
- Usunąć TrailsEffects gdzie możliwe

---

### 6. Skill Effects (245+ plików)

Ścieżki:
- `metadata/effects/spells/enchantment_skills/`
- `metadata/effects/spells/*/`

**Rekomendacja: OSTROŻNE UPROSZCZENIE**
- Efekty skilli są ważne dla gameplay feedback
- Możliwe zmniejszenie particle count
- Nie usuwać całkowicie

---

### 7. Monster Effects (10,000+ plików)

Ścieżki:
- `metadata/effects/spells/monsters_effects/`
- `metadata/monsters/*/`

**Rekomendacja: SELEKTYWNE UPROSZCZENIE**
- Usunąć nadmiarowe particle effects
- Zachować kluczowe wizualne wskaźniki ataków

---

## Propozycje Patcherów

### PATCHER 1: Disable Environmental Particles (Wysoki priorytet)

**Cel:** Usunięcie dekoracyjnych efektów środowiskowych

**Pliki docelowe:**
- `metadata/terrain/doodads/*/fog*.aoc`
- `metadata/terrain/doodads/*/mist*.aoc`
- `metadata/terrain/doodads/*/dust*.aoc`
- `metadata/terrain/*/glowworms*.aoc`
- `metadata/terrain/doodads/vaal_sidearea_effects/*.aoc`

**Modyfikacja:**
```
ParticleEffects
{
    // Usunąć wszystkie linie continuous_effect
}
```

**Szacowany wpływ:** Wysoki - setki ciągle aktywnych emitterów

---

### PATCHER 2: Simplify Torch Effects (Średni priorytet)

**Cel:** Usunięcie płomieni z pochodni, zachowanie świateł

**Pliki docelowe:**
- `metadata/terrain/*/torch*.aoc`
- `metadata/terrain/doodads/*/fire*.aoc` (środowiskowe)

**Modyfikacja:**
```
ParticleEffects
{
    // Usunąć continuous_effect dla pochodni
}
// Zachować sekcję Lights
```

**Szacowany wpływ:** Średni-wysoki w dungeonach

---

### PATCHER 3: Reduce Waterfall Particles (Niski priorytet)

**Cel:** Uproszczenie efektów wodospadów

**Pliki docelowe:**
- `metadata/terrain/*waterfall*.aoc`

**Modyfikacja:**
- Usunąć ParticleEffects
- Zachować mesh i animację

---

### PATCHER 4: Remove Decorative Blood (Niski priorytet)

**Cel:** Usunięcie dekoracyjnych efektów krwi

**Pliki docelowe:**
- `metadata/terrain/*bloodpool*.aoc`
- `metadata/terrain/*bloodstream*.aoc`
- `*_blood.aoc` (warianty)

---

## Techniczne aspekty implementacji

### Enkodowanie
Pliki `.ao` i `.aoc` używają **UTF-16 LE** (bez BOM).

### Modyfikacja ParticleEffects

**Przed:**
```
ParticleEffects
{
    continuous_effect = "emitter Metadata/Particles/effect.pet"
    tick_when_not_visible = true
}
```

**Po (wyłączenie):**
```
ParticleEffects
{
    //{{RITHIEN_envparticles}} continuous_effect = "emitter Metadata/Particles/effect.pet"
    // tick_when_not_visible = true
}
```

### Alternatywna metoda - puste sekcje
```
ParticleEffects
{
}
```

### Marker file
Dla patchera środowiskowego:
- `markerFile`: `metadata/terrain/doodads/cave/cave_mist_01.aoc`
- `marker`: `{{RITHIEN_envparticles}}`

---

## Podsumowanie priorytetów

| Patcher | Priorytet | Trudność | Wpływ na wydajność |
|---------|-----------|----------|-------------------|
| Environmental Particles | WYSOKI | Niska | WYSOKI |
| Torch Effects | ŚREDNI | Niska | ŚREDNI |
| Waterfall Particles | NISKI | Niska | NISKI |
| Decorative Blood | NISKI | Średnia | NISKI |
| Ground Effects Simplify | ŚREDNI | Wysoka | ŚREDNI |

---

## Uwagi końcowe

1. **Bezpieczeństwo:** Efekty związane z mechanikami gry (damage zones, buff indicators) NIE powinny być usuwane.

2. **Testowanie:** Każdy patcher wymaga testowania na różnych lokacjach (cave, town, dungeon).

3. **Backup:** System markerów i backupów z CLAUDE.md musi być zastosowany.

4. **Kompatybilność:** Modyfikacje .aoc nie wpływają na .ao - animacje pozostają nienaruszone.
