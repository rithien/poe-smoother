# QoL Analysis - Elasticsearch Index poe-files

> Analiza 333,117 plików gry Path of Exile pod kątem możliwości poprawy Quality of Life.

## Podsumowanie Indeksu

| Rozszerzenie | Ilość plików | Opis |
|--------------|--------------|------|
| .mat | 136,006 | Materiały (tekstury, shadery) |
| .aoc | 59,541 | Animation Object Containers (efekty, cząsteczki) |
| .ao | 59,480 | Animation Objects |
| .sm | 33,466 | Skeletal Meshes |
| .amd | 18,362 | Animation Metadata |
| .ot | 12,348 | Object Templates (UTF-16) |
| .otc | 12,244 | Object Template Containers (UTF-16) |
| .mtp | 847 | Minimap Templates |
| .it | 403 | Item Templates |
| .itc | 197 | Item Template Containers |
| .hlsl | 124 | Shadery HLSL |
| .txt | 64 | Pliki tekstowe |
| .ffx | 29 | Effect Files |
| .json | 4 | Konfiguracje JSON |
| .inc | 2 | Shader Includes |

---

## 1. Screen Shake Reduction (Camera Shake)

### Znalezione pliki: 2,310+

**Kluczowe pliki:**
```
metadata/monsters/atlasexiles/arenamechanics/atlasexile5labscreenshake.aoc
metadata/monsters/atlasexiles/arenamechanics/atlasexile5labscreenshaker.ot
metadata/effects/spells/monsters_effects/act9/garukhan/minions/normal/normalemergenoscreenshake.aoc
```

**Wzorzec w plikach .aoc:**
```
screenshake = true/false
screenshake_intensity = X
```

### Propozycja implementacji

**Patcher: Disable Screen Shake**

```json
{
  "enabled": true,
  "name": "Disable Screen Shake",
  "description": "Removes camera shake effects for more stable gameplay",
  "category": "QoL",
  "impactLevel": 2,
  "markerFile": "metadata/parent_assetviewer.aoc",
  "marker": "{{RITHIEN_screenshake}}",
  "targets": {
    "files": [],
    "extensions": [".aoc"],
    "basePaths": ["metadata/"]
  }
}
```

**Technika:** Wyszukaj `screenshake` w plikach .aoc i zamień wartość na `false` lub `0`.

---

## 2. Minimap Enhancements

### Znalezione pliki shaderów minimapy:

| Plik | Rozmiar | Funkcja |
|------|---------|---------|
| `shaders/minimap_blending_pixel.hlsl` | 4,945 | Blending kolorów minimapy |
| `shaders/minimap_visibility_pixel.hlsl` | ~2KB | Widoczność eksploracji |
| `shaders/minimap_pixel.hlsl` | 7,821 | Główny shader minimapy |
| `shaders/minimap_tile_vertex.hlsl` | 3,814 | Vertex shader kafelków |

### Kluczowe zmienne w `minimap_blending_pixel.hlsl`:

```hlsl
// Obecne wartości:
float4 walkable_color = float4(1.0f, 1.0f, 1.0f, 0.01f);
float4 walkability_map_color = lerp(walkable_color, float4(0.5f, 0.5f, 1.0f, 0.5f), walkable_to_edge_ratio);

// Synteza (decay) kolory:
float3 color = pow(float3(244, 161, 66) / 255.0f, 2.2f); // Pomarańczowy

// Royale mode:
float4(15.0f/255.0f, 159.0f/255.0f, 255.0f/255.0f, 1.0f) // Niebieski
float4(255.0f/255.0f, 35.0f/255.0f, 0.0f/255.0f, 1.0f)   // Czerwony
```

### Propozycja: Minimap Color Customization

```json
{
  "enabled": true,
  "name": "Custom Minimap Colors",
  "description": "Customize minimap walkable area, edges, and explored colors",
  "category": "Visual",
  "impactLevel": 2,
  "markerFile": "shaders/minimap_blending_pixel.hlsl",
  "marker": "{{RITHIEN_minimapcolors}}",
  "targets": {
    "files": ["shaders/minimap_blending_pixel.hlsl"],
    "extensions": [],
    "basePaths": []
  }
}
```

**Konfigurowalne parametry:**
- `walkable_color` - kolor obszaru do chodzenia
- `edge_color` - kolor krawędzi
- `unexplored_opacity` - przezroczystość nieodkrytych obszarów

---

## 3. Vignette Removal

### Lokalizacja: `shaders/postprocessuber.hlsl` (65,555 bytes)

**Znaleziona funkcja:** `ApplyVignette`

### Propozycja implementacji

```json
{
  "enabled": true,
  "name": "Disable Vignette",
  "description": "Removes screen edge darkening effect",
  "category": "Visual",
  "impactLevel": 1,
  "markerFile": "shaders/postprocessuber.hlsl",
  "marker": "{{RITHIEN_vignette}}",
  "targets": {
    "files": ["shaders/postprocessuber.hlsl"],
    "extensions": [],
    "basePaths": []
  },
  "replacements": [
    {
      "pattern": "colour = ApplyVignette(colour,",
      "replacement": "//{{RITHIEN_vignette}} colour = ApplyVignette(colour,",
      "isRegex": false
    }
  ]
}
```

---

## 4. Saturation/Desaturation Control

### Znalezione funkcje w `shaders/include/util.hlsl`:

```hlsl
float4 Desaturate( float4 base_colour, float saturation )
{
    return float4( lerp( dot( base_colour.rgb, float3( 0.30, 0.59, 0.11 ) ), base_colour.rgb, saturation ), base_colour.a );
}

float3 Vibrance(float val, float3 color)
{
    val = saturate(val);
    return pow(saturate(3.0f * val * val - 2.0f * val * val * val), 1.0f / (color + 1e-5));
}
```

### Pliki używające Desaturate:

| Plik | Funkcja |
|------|---------|
| `shaders/archnemesiseffects.hlsl` | Efekty archnemesis |
| `shaders/miscuiscreeneffects.hlsl` | UI screen effects |
| `shaders/passiveskillscreeneffects.hlsl` | Passive tree |
| `shaders/draw2d.hlsl` | 2D rendering |
| `shaders/include/tonemapping.hlsl` | Tonemapping |

### Propozycja: Saturation Control

```json
{
  "enabled": true,
  "name": "Saturation x{{MULTIPLIER}}",
  "description": "Adjusts color saturation globally",
  "category": "Visual",
  "impactLevel": 2,
  "markerFile": "shaders/postprocessuber.hlsl",
  "marker": "{{RITHIEN_saturation}}",
  "targets": {
    "files": ["shaders/postprocessuber.hlsl"],
    "extensions": [],
    "basePaths": []
  }
}
```

**Implementacja:** Dodaj post-process saturation przed OutputSDR:
```hlsl
// Przed return:
float saturation = {{SATURATION_VALUE}}f; //{{RITHIEN_saturation}}
colour.rgb = lerp(dot(colour.rgb, float3(0.30, 0.59, 0.11)), colour.rgb, saturation);
```

---

## 5. Volumetric FX Control

### Plik: `shaders/volumetricfx.hlsl` (10,826 bytes)

**Główna funkcja:** `mainSimulate` - Compute shader dla efektów wolumetrycznych

**Kluczowe struktury:**
```hlsl
struct VolumetricVoxel {
    float mass;
    float3 albedo;
    float3 emissive;
    float temperature;
    float scattering;
    float absorption;
    float anisotropy;
    float disturbance;
    float dissipation;
    float3 velocity;
    float viscosity;
    float density;
}
```

### Propozycja: Disable Volumetric FX

```json
{
  "enabled": true,
  "name": "Disable Volumetric FX",
  "description": "Disables volumetric fog and smoke effects for better performance",
  "category": "Performance",
  "impactLevel": 6,
  "markerFile": "shaders/volumetricfx.hlsl",
  "marker": "{{RITHIEN_volumetricfx}}",
  "targets": {
    "files": ["shaders/volumetricfx.hlsl"],
    "extensions": [],
    "basePaths": []
  },
  "replacements": [
    {
      "pattern": "void mainSimulate(uint3 thread_id : SV_GroupThreadID, uint3 group_id : SV_GroupID)",
      "replacement": "void mainSimulate(uint3 thread_id : SV_GroupThreadID, uint3 group_id : SV_GroupID) { return; } //{{RITHIEN_volumetricfx}}\nvoid mainSimulate_DISABLED(uint3 thread_id : SV_GroupThreadID, uint3 group_id : SV_GroupID)",
      "isRegex": false
    }
  ]
}
```

---

## 6. Aura Effects Simplification

### Znalezione pliki aur: 1,365+

**Przykładowe ścieżki:**
```
metadata/effects/microtransactions/aura/*/
metadata/effects/spells/monsters_effects/aura/
```

**Kategorie aur:**
| Typ | Przykład | Ilość plików |
|-----|----------|--------------|
| MTX Auras | carnival_harlequin, dragonhunter | ~50 |
| Unified Auras | grey, purple, red, yellow (t01-t04) | ~16 |
| Monster Auras | puppetmaster, buff effects | ~100+ |

### Propozycja: Simplified Aura Effects

Modyfikacja plików .aoc aur aby zmniejszyć:
- Liczbę cząsteczek
- Intensywność efektów
- Rozmiar efektów

---

## 7. On-Death Effects

### Znalezione pliki: 1,347+ z wzorcem `on_death`

**Przykładowe ścieżki:**
```
metadata/effects/spells/monsters_effects/league_synthesis/synthesis_spiker/on_death/
metadata/monsters/malachai/mapmalachai2.otc (on_death handlers)
```

**Kategorie:**
- Eksplozje przy śmierci
- Efekty cząsteczkowe
- Screen effects

### Propozycja: Simplified Death Effects

```json
{
  "enabled": true,
  "name": "Simplified Death Effects",
  "description": "Reduces visual clutter from monster death effects",
  "category": "Performance",
  "impactLevel": 5,
  "markerFile": "metadata/effects/spells/monsters_effects/league_synthesis/synthesis_spiker/on_death/explode.aoc",
  "marker": "{{RITHIEN_deathfx}}",
  "targets": {
    "files": [],
    "extensions": [".aoc"],
    "basePaths": ["metadata/effects/"]
  }
}
```

---

## 8. Monster Rarity Highlights

### Znalezione pliki: `art/particles/rarityhighlights/monster_rarity/`

| Plik | Funkcja |
|------|---------|
| `bestiary_override.mat` | Bestiary creatures |
| `bestiary_shade.mat` | Bestiary shade |
| `bloodlines_override.mat` | Bloodlines mod |
| `magic_override.mat` | Magic monsters |
| `nemesis_override.mat` | Nemesis mod |
| `rare_override.mat` | Rare monsters |
| `minion_*.mat` | Minion highlights |

### Propozycja: Enhanced Monster Highlights

Modyfikacja materiałów aby zwiększyć widoczność:
- Zwiększenie intensywności kolorów
- Zwiększenie kontrastu
- Dodanie outline effect

---

## 9. Item Effects Shader

### Plik: `shaders/include/itemeffects.hlsl` (47,304 bytes)

Duży shader odpowiedzialny za efekty przedmiotów - potencjalnie:
- Loot beams
- Item glow effects
- Rarity visual effects

### Do dalszej analizy

---

## 10. Weather Effects

### Znalezione pliki: 10,000+ (rain, snow, weather)

**Kluczowe ścieżki:**
```
art/models/terrain/doodads/hideouts/karuiweathertotem/
art/particles/monster_particles/*/snow*
art/particles/monster_particles/*/rain*
```

### Propozycja: Disable Weather Effects

Modyfikacja plików .aoc/material aby wyłączyć:
- Deszcz
- Śnieg
- Mgłę pogodową

---

## Priorytetyzacja QoL Features

### Natychmiastowa implementacja (Niska złożoność):

| # | Feature | Plik docelowy | Impact |
|---|---------|---------------|--------|
| 1 | Disable Vignette | postprocessuber.hlsl | Low |
| 2 | Screen Shake Reduction | *.aoc | Medium |
| 3 | Custom Minimap Colors | minimap_blending_pixel.hlsl | Medium |

### Krótkoterminowa (Średnia złożoność):

| # | Feature | Pliki docelowe | Impact |
|---|---------|----------------|--------|
| 1 | Saturation Control | postprocessuber.hlsl | Medium |
| 2 | Volumetric FX | volumetricfx.hlsl | High |
| 3 | Simplified Death FX | *.aoc | High |

### Długoterminowa (Wysoka złożoność):

| # | Feature | Pliki docelowe | Impact |
|---|---------|----------------|--------|
| 1 | Aura Simplification | *.aoc (1000+) | Very High |
| 2 | Monster Highlights | *.mat | High |
| 3 | Weather Control | *.aoc, *.mat | Medium |

---

## Statystyki Przeszukiwania

| Wzorzec | Wyniki |
|---------|--------|
| screenshake | 2,310 |
| aura | 1,365 |
| on_death | 1,347 |
| Saturation | 1,900 |
| highlight | 82 |
| beam | 6,971 |
| death | 7,648 |
| minimap | 847 (.mtp) |

---

## Następne Kroki

1. **Implementacja Vignette Removal** - najprostsza zmiana
2. **Analiza postprocessuber.hlsl** - identyfikacja wszystkich ApplyX funkcji
3. **Stworzenie batch processor** - dla modyfikacji tysięcy plików .aoc
4. **Kategoryzacja efektów śmierci** - które są bezpieczne do usunięcia

---

*Analiza wykonana: 2026-01-22*
*Indeks: poe-files (333,117 dokumentów)*
