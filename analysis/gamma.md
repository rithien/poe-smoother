# Analiza Gamma Patcher - Path of Exile

## Problem z obecnym rozwiązaniem

Obecny `GammaPatcher` szuka wzorca:
```
1.0f / 2.2f
```

**Problem:** Ten wzorzec znajduje się w pliku `shaders/include/oetf.hlsl` w **zakomentowanej linii**:
```hlsl
// Simpler transfer function:
//return pow(saturate(linCol), 1.0f / 2.2f);
```

Ta linia **NIE JEST WYKONYWANA** - jest tylko komentarzem!

## Aktualne wartości gamma w grze

### Plik: `shaders/include/oetf.hlsl`

#### Funkcja OETF_REC709 (standard sRGB):
```hlsl
float3 OETF_REC709(float3 linCol)
{
    float a = 0.0031308f;
    float b = 0.055f;
    float c = 12.92f;
    float m = 1.0f / 2.4f;  // <-- TO JEST PRAWDZIWA GAMMA (2.4)

    float3 color = saturate(linCol);
    color.x = color.x > a ? ((1.0f + b) * pow(color.x, m) - b) : (c * color.x);
    color.y = color.y > a ? ((1.0f + b) * pow(color.y, m) - b) : (c * color.y);
    color.z = color.z > a ? ((1.0f + b) * pow(color.z, m) - b) : (c * color.z);

    return color;

    // Simpler transfer function:
    //return pow(saturate(linCol), 1.0f / 2.2f);  // <-- ZAKOMENTOWANE!
}
```

**Prawdziwa wartość gamma:** `m = 1.0f / 2.4f` (standard sRGB gamma = 2.4)

---

## Alternatywne rozwiązania

### Opcja 1: Modyfikacja rzeczywistej zmiennej gamma (REKOMENDOWANE)

**Plik:** `shaders/include/oetf.hlsl`

**Zmiana:**
```hlsl
// Z:
float m = 1.0f / 2.4f;

// Na (dla gamma 2.2):
float m = 1.0f / 2.2f; //{{RITHIEN_gamma}}
```

**Zalety:**
- Modyfikuje faktyczną wartość używaną w grze
- Wpływa na wszystkie miejsca używające OETF_REC709
- Zachowuje poprawną krzywą sRGB z innym wykładnikiem

**Wady:**
- Zmienia tylko część liniową funkcji transferu (poniżej progu `a = 0.0031308f` używany jest współczynnik liniowy `c`)

**Regex do patchowania:**
```regex
float m = 1\.0f / [\d.]+f;(\s*//\{\{RITHIEN_gamma\}\})?
```

---

### Opcja 2: Odkomentowanie prostszej funkcji gamma

**Plik:** `shaders/include/oetf.hlsl`

**Zmiana:**
```hlsl
// Z:
return color;

// Simpler transfer function:
//return pow(saturate(linCol), 1.0f / 2.2f);

// Na:
//return color;  // Standard sRGB disabled //{{RITHIEN_gamma}}

// Simpler transfer function:
return pow(saturate(linCol), 1.0f / 2.2f); //{{RITHIEN_gamma}}
```

**Zalety:**
- Prostsza krzywa gamma (pow bez piecewise linear section)
- Łatwiejsza do modyfikacji
- Bardziej przewidywalny efekt

**Wady:**
- Odchodzi od standardu sRGB
- Może wyglądać inaczej w ciemnych obszarach (brak sekcji liniowej)

---

### Opcja 3: Dodanie post-processing gamma step

**Plik:** `shaders/postprocessoutput.hlsl`

**Zmiana:**
```hlsl
// Z:
return ApplyOETF(color, oetf_id);

// Na:
float gamma_correction = 2.2f / 2.4f; // Brighten //{{RITHIEN_gamma}}
color.xyz = pow(saturate(color.xyz), gamma_correction);
return ApplyOETF(color, oetf_id);
```

**Zalety:**
- Nie modyfikuje standardowej funkcji OETF
- Dodaje dodatkową kontrolę przed konwersją wyjściową

**Wady:**
- Podwójna konwersja gamma (może prowadzić do artefaktów)
- Mniej precyzyjna kontrola

---

### Opcja 4: Modyfikacja pow() w imgui.hlsl (tylko UI)

**Plik:** `shaders/imgui.hlsl`

Linia w vertex shader:
```hlsl
output.col.rgb = pow(output.col.rgb, 2.2f);
```

**UWAGA:** Ta linia dotyczy tylko kolorów UI, nie całej gry!

---

## Rekomendowane rozwiązanie

### Opcja 1 jest najlepsza:

**Plik:** `shaders/include/oetf.hlsl`

**Wzorzec do znalezienia:**
```
float m = 1.0f / 2.4f;
```

**Zamiana na (przykład dla gamma 2.0):**
```
float m = 1.0f / 2.0f; //{{RITHIEN_gamma}}
```

**Regex dla patchera (z obsługą re-patchu):**
```regex
float m = 1\.0f / [\d.]+f;(\s*//\{\{RITHIEN_gamma\}\})?
```

---

## Porównanie wartości gamma

| Gamma | Efekt | m = 1.0f / X |
|-------|-------|--------------|
| 2.4 | Standard sRGB (domyślne) | 1.0f / 2.4f |
| 2.2 | Lekko jaśniejsze półtony | 1.0f / 2.2f |
| 2.0 | Jaśniejsze półtony | 1.0f / 2.0f |
| 1.8 | Znacznie jaśniejsze | 1.0f / 1.8f |
| 1.6 | Bardzo jasne | 1.0f / 1.6f |

**Wzór:** Niższa wartość gamma = jaśniejszy obraz (w półtonach)

---

## Pliki używające OETF

1. **shaders/postprocessoutput.hlsl** - główne wyjście postprocess
2. **shaders/imgui.hlsl** - interfejs użytkownika
3. **shaders/postprocessuber.hlsl** - główny shader postprocess

Wszystkie te pliki używają `#include "Shaders/Include/OETF.hlsl"` i wywołują `ApplyOETF()`.

---

## Aktualizacja GammaPatcher

Należy zmienić w `GammaPatcher.cs`:

```csharp
// Z:
const string originalPattern = "1.0f / 2.2f";

// Na:
const string originalPattern = "float m = 1.0f / 2.4f;";
```

I odpowiednio dostosować regex oraz replacement.

---

## Data analizy
2026-01-23

## Status implementacji
**ZAIMPLEMENTOWANO:** 2026-01-24

Opcja 1 została zaimplementowana w `GammaPatcher.cs`:
- Wzorzec: `float m = 1\.0f / [\d\.]+f;(\s*//\{\{RITHIEN_gamma\}\})?`
- Zamiana: `float m = 1.0f / {gamma}f; //{{RITHIEN_gamma}}`
- Obsługuje zarówno pierwszy patch jak i re-patch

## Źródło
Elasticsearch index: `poe-files`
Pliki: `shaders/include/oetf.hlsl`, `shaders/postprocessoutput.hlsl`, `shaders/imgui.hlsl`
