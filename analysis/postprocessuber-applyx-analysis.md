# Analiza postprocessuber.hlsl - Funkcje ApplyX

**Plik:** `shaders/postprocessuber.hlsl`
**Data analizy:** 2026-01-22

## Przegląd

Plik `postprocessuber.hlsl` jest głównym uber shaderem post-processingu w Path of Exile 2. Zawiera **19 funkcji ApplyX**, które stosują różne efekty wizualne w pipeline'ie renderowania.

## Flagi kompilacji (ENABLE_*)

Shader używa warunkowej kompilacji z następującymi flagami:

| Flaga | Opis |
|-------|------|
| `ENABLE_ALPHAMUL` | Mnożenie alpha |
| `ENABLE_COMPOSITION` | Efekt kompozycji |
| `ENABLE_GAMEPLAY` | Efekty gameplay (screen shake, etc.) |
| `ENABLE_JITTER` | Jitter (TAA) |
| `ENABLE_SHIMMER` | Efekt shimmer |
| `ENABLE_TONEMAPPING` | Mapowanie tonów |

---

## Lista funkcji ApplyX

### 1. ApplyAzmeriFog
- **Typ zwracany:** `float4`
- **Parametry:** `float4 colour, float3 world_pos`
- **Zmienne:** `azmeri_detail`, `azmeri_detail_tex`, `azmeri_enable`, `azmeri_map`, `azmeri_map_minmax`, `azmeri_map_tex`
- **Opis:** Nakłada efekt mgły Azmeri na podstawie pozycji w świecie. Używa mapy tekstur do określenia gęstości mgły.
- **Potencjał optymalizacji:** ✅ Można wyłączyć poprzez `azmeri_enable`

---

### 2. ApplyBloom
- **Typ zwracany:** `float4`
- **Parametry:** `float4 colour, float2 screen_pos, float2 screen_jitter`
- **Samplery:** `bloom_sampler`
- **Zmienne:** `bloom_coord`, `original_intensity`
- **Opis:** Dodaje efekt bloom (poświata wokół jasnych obszarów). Próbkuje blur texture i miesza z oryginalnym kolorem.
- **Potencjał optymalizacji:** ✅ Można zmniejszyć `original_intensity` lub zmodyfikować blending

---

### 3. ApplyColorGrading
- **Typ zwracany:** `float4`
- **Parametry:** `float4 color`
- **Zmienne:** `inv_viewproj_matrix`
- **Opis:** Stosuje korekcję kolorów (LUT lub matematyczne przekształcenia).
- **Potencjał optymalizacji:** ⚠️ Może wpływać na czytelność gry

---

### 4. ApplyCompositionEffect
- **Typ zwracany:** `float4`
- **Parametry:** `float4 colour, float depth, float2 screen_pos, float2 screen_to_color, float2 screen_to_dyn, float2 screen_jitter, float2 frame_jitter`
- **Zmienne:** `frame_jitter`
- **Opis:** Złożony efekt kompozycji - prawdopodobnie łączy wiele warstw renderowania.
- **Potencjał optymalizacji:** ❓ Wymaga dalszej analizy

---

### 5. ApplyDecay
- **Typ zwracany:** `float4`
- **Parametry:** `float4 colour, float3 world_pos, float2 screen_to_dyn`
- **Opis:** Efekt wizualny "decay" (rozkład/gnicie) - prawdopodobnie używany w określonych lokacjach lub przy określonych debuffach.
- **Potencjał optymalizacji:** ✅ Efekt specyficzny dla sytuacji

---

### 6. ApplyDesaturation
- **Typ zwracany:** `float4`
- **Parametry:** `float4 color, float2 tex_coord`
- **Samplery:** `desaturation_sampler`, `desaturation_transform_sampler`
- **Zmienne:** `desaturation_enable`, `desaturation_sample`
- **Opis:** Desaturacja (odbarwienie) obrazu - używane np. przy śmierci postaci lub w określonych efektach gameplay.
- **Potencjał optymalizacji:** ✅ Można wyłączyć poprzez `desaturation_enable`

---

### 7. ApplyDithering
- **Typ zwracany:** `float4`
- **Parametry:** `float4 colour, float2 screen_pos`
- **Zmienne:** `oetf_id`
- **Opis:** Dodaje dithering (rozpraszanie) do redukcji bandingu kolorów. Używa OETF (Opto-Electronic Transfer Function).
- **Potencjał optymalizacji:** ⚠️ Wyłączenie może spowodować banding

---

### 8. ApplyDoF
- **Typ zwracany:** `float4`
- **Parametry:** `float4 colour, float depth, float2 screen_pos, float2 screen_to_color, float2 frame_jitter`
- **Zmienne:** `frame_jitter`
- **Opis:** Depth of Field (głębia ostrości) - rozmywa obiekty poza fokusem kamery.
- **Potencjał optymalizacji:** ✅ **WYSOKI** - DoF może znacząco wpływać na wydajność i czytelność

---

### 9. ApplyEngineLoadingScreen
- **Typ zwracany:** `float4`
- **Parametry:** `float4 color, float2 screen_pos`
- **Zmienne:** `engine_loading_amount`
- **Opis:** Efekt ekranu ładowania silnika (fade in/out).
- **Potencjał optymalizacji:** ❌ Nie zalecane

---

### 10. ApplyFade
- **Typ zwracany:** `float4`
- **Parametry:** `float4 colour`
- **Zmienne:** `fade_amount`, `fade_enable`
- **Opis:** Prosty efekt fade (zanikanie/pojawianie się obrazu).
- **Potencjał optymalizacji:** ❌ Funkcjonalność systemowa

---

### 11. ApplyGameplayEffect
- **Typ zwracany:** `float4`
- **Parametry:** `float4 colour, float depth, float2 screen_pos, float2 screen_to_color, float2 screen_to_dyn, float2 screen_jitter, float2 frame_jitter`
- **Zmienne:** `frame_jitter`, `inv_viewproj_matrix`
- **Opis:** Ogólne efekty gameplay (screen shake, distortion przy atakaach, etc.). Kontrolowane flagą `ENABLE_GAMEPLAY`.
- **Potencjał optymalizacji:** ✅ Można wyłączyć poprzez `ENABLE_GAMEPLAY=0`

---

### 12. ApplyOverlay
- **Typ zwracany:** `float4`
- **Parametry:** `float4 color, float2 screen_coord`
- **Samplery:** `overlay_sampler`
- **Zmienne:** `overlay_intensity`, `overlay_sample`
- **Opis:** Nakłada teksturę overlay na ekran (np. efekty krwi na ekranie, winietowanie dynamiczne).
- **Potencjał optymalizacji:** ⚠️ Zależy od typu overlay

---

### 13. ApplyRadialBlur
- **Typ zwracany:** `float4`
- **Parametry:** `float4 colour, float2 screen_pos, float2 screen_to_color`
- **Zmienne:** `frame_jitter`
- **Opis:** Radialne rozmycie (zoom blur) - używane przy określonych efektach mocy lub ruchu.
- **Potencjał optymalizacji:** ✅ Efekt okazjonalny

---

### 14. ApplyRitualDarkness
- **Typ zwracany:** `float4`
- **Parametry:** `float4 colour, float3 world_pos`
- **Opis:** Efekt ciemności Ritualu - specyficzny dla mechaniki Ritual w grze.
- **Potencjał optymalizacji:** ✅ Efekt specyficzny dla mechaniki

---

### 15. ApplyShimmerEffect
- **Typ zwracany:** `float4`
- **Parametry:** `float4 colour, float depth, inout float2 screen_pos, float2 screen_to_color, float2 screen_to_dyn, float2 screen_jitter, float2 frame_jitter`
- **Samplery:** `depth_sampler`, `source_sampler`
- **Zmienne:** `frame_size`, `initial_alpha`, `jitter_offset`
- **Opis:** Efekt shimmer (migotanie/falowanie) - prawdopodobnie efekt ciepła lub magii. Kontrolowane flagą `ENABLE_SHIMMER`.
- **Potencjał optymalizacji:** ✅ Można wyłączyć poprzez `ENABLE_SHIMMER=0`

---

### 16. ApplyStochasticBlur
- **Typ zwracany:** `float4`
- **Parametry:** `TEXTURE2D_DECL(tex), ...` (makro tekstury)
- **Opis:** Stochastyczne (losowe) rozmycie - używa losowego próbkowania dla gładszego blur efektu.
- **Potencjał optymalizacji:** ⚠️ Może być używane przez inne efekty

---

### 17. ApplyToneMapping
- **Typ zwracany:** `float4`
- **Parametry:** `float4 color`
- **Opis:** Podstawowe mapowanie tonów (HDR → LDR). Konwertuje wartości HDR do wyświetlalnego zakresu.
- **Potencjał optymalizacji:** ⚠️ Kluczowa funkcjonalność renderingu

---

### 18. ApplyToneMappingEffect
- **Typ zwracany:** `float4`
- **Parametry:** `float4 colour, float depth, float2 screen_pos, float2 screen_to_color, float2 screen_to_dyn, float2 screen_jitter, float2 frame_jitter`
- **Zmienne:** `frame_jitter`, `inv_viewproj_matrix`
- **Opis:** Rozszerzony efekt mapowania tonów z dodatkowymi parametrami. Kontrolowane flagą `ENABLE_TONEMAPPING`.
- **Potencjał optymalizacji:** ✅ Można kontrolować poprzez `ENABLE_TONEMAPPING`

---

### 19. ApplyVignette
- **Typ zwracany:** `float4`
- **Parametry:** `float4 color, float2 screen_coord`
- **Opis:** Efekt winiety - przyciemnia krawędzie ekranu dla dramatycznego efektu.
- **Potencjał optymalizacji:** ✅ **WYSOKI** - czysto kosmetyczny efekt, łatwy do wyłączenia

---

## Kolejność wywołań w mainPS

Pipeline post-processingu wykonuje funkcje w następującej kolejności:

```hlsl
1.  ApplyStochasticBlur      // Wstępne rozmycie
2.  ApplyDecay               // Efekt decay
3.  ApplyOverlay             // Overlay #1
4.  ApplyRitualDarkness      // Ciemność ritualu
5.  ApplyAzmeriFog           // Mgła Azmeri
6.  ApplyRadialBlur          // Rozmycie radialne
7.  ApplyDoF                 // Głębia ostrości
8.  ApplyBloom               // Bloom #1
9.  ApplyToneMapping         // Tone mapping podstawowy
10. ApplyDesaturation        // Desaturacja
11. ApplyOverlay             // Overlay #2
12. ApplyColorGrading        // Korekcja kolorów
13. ApplyBloom               // Bloom #2
14. ApplyRitualDarkness      // Ciemność ritualu (ponownie)
15. ApplyAzmeriFog           // Mgła Azmeri (ponownie)
16. ApplyDithering           // Dithering
17. ApplyVignette            // Winieta
18. ApplyFade                // Fade
19. ApplyEngineLoadingScreen // Ekran ładowania
20. ApplyShimmerEffect       // Shimmer (ENABLE_SHIMMER)
21. ApplyGameplayEffect      // Gameplay (ENABLE_GAMEPLAY)
22. ApplyCompositionEffect   // Kompozycja (ENABLE_COMPOSITION)
23. ApplyToneMappingEffect   // Tone mapping (ENABLE_TONEMAPPING)
```

---

## Rekomendacje dla patcherów

### Wysokie priorytety (łatwe do wyłączenia, duży wpływ):

| Funkcja | Wpływ na wydajność | Wpływ na wizualia | Metoda wyłączenia |
|---------|-------------------|-------------------|-------------------|
| **ApplyVignette** | Niski | Kosmetyczny | Return early: `return color;` |
| **ApplyDoF** | Wysoki | Czytelność+ | Return early lub zmiana parametrów |
| **ApplyBloom** | Średni | Kosmetyczny | Zmiana `original_intensity` |
| **ApplyDesaturation** | Niski | Sytuacyjny | `desaturation_enable = 0` |

### Średnie priorytety:

| Funkcja | Notatki |
|---------|---------|
| ApplyAzmeriFog | Specyficzne dla lokacji |
| ApplyRitualDarkness | Specyficzne dla mechaniki |
| ApplyRadialBlur | Okazjonalne użycie |
| ApplyDecay | Specyficzne dla lokacji |

### Nie modyfikować:

| Funkcja | Powód |
|---------|-------|
| ApplyToneMapping | Kluczowa dla wyświetlania |
| ApplyEngineLoadingScreen | Funkcjonalność systemowa |
| ApplyFade | Funkcjonalność systemowa |
| ApplyDithering | Zapobiega bandingowi |

---

## Powiązane pliki

- `shaders/postprocess_common.inc` - Wspólne definicje
- `shaders/bloom_*.hlsl` - Shaders bloom
- `shaders/dof_*.hlsl` - Shaders głębi ostrości
- `shaders/tonemapping.inc` - Funkcje tone mapping

---

## Zobacz również

- [visual-enhancements.md](visual-enhancements.md) - Ogólne ulepszenia wizualne
- [performance-optimizations.md](performance-optimizations.md) - Optymalizacje wydajności
