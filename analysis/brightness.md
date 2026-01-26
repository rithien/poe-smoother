## 1. Analiza Metod Zwiększenia Jasności (Shadery HLSL)

> [!NOTE]
> Poniższa analiza została przeprowadzona poprzez przeszukanie 46 plików HLSL w indeksie Elasticsearch pod kątem manipulacji jasnością obrazu.

### 1.1 Bezpośrednie Funkcje Jasności (`shaders/include/util.hlsl`)

```hlsl
float4 SetBrightness(float4 base_colour, float brightness)
{
    float4 added_brightness = float4(brightness, brightness, brightness, 1) * base_colour.a;
    return base_colour * added_brightness;
}

float4 SetBrightnessNoAlpha(float4 base_colour, float brightness)
{
    float3 colour = base_colour.rgb * float3(brightness, brightness, brightness);
    return float4(colour, base_colour.a);
}
```

**Modyfikacja:** Zwiększ wartość `brightness` powyżej `1.0` dla jaśniejszego obrazu.

### 1.2 Bloom (Efekt Poświaty) (`shaders/bloomcutoff.hlsl`)

```hlsl
float3 Luminance(float3 color)
{
    return dot(color, float3(1.0f, 1.0f, 1.0f)) / 3.0f;
}

float4 BloomCutoff(PInput input) : PIXEL_RETURN_SEMANTIC
{
    // ...
    float luminance = Luminance(color_sample.rgb).r;
    float mult = max(0.0f, luminance - cutoff) * intensity;
    return float4(color_sample.rgb * mult, color_sample.a);
}
```

**Parametry do modyfikacji:**

| Parametr    | Efekt zwiększenia                               |
| ----------- | ----------------------------------------------- |
| `cutoff`    | Obniż wartość → więcej pikseli będzie "świecić" |
| `intensity` | Zwiększ wartość → silniejszy efekt bloom        |

### 1.3 Tone Mapping (`shaders/include/tonemapping.hlsl`)

Gra implementuje **11 algorytmów tonemappingu**:

| ID  | Algorytm      | Charakterystyka                |
| --- | ------------- | ------------------------------ |
| 0   | None          | Brak kompresji - najjaśniejszy |
| 1   | Reinhard      | Delikatna kompresja            |
| 2   | ACES Simple   | Filmowa krzywa (uproszczona)   |
| 3   | ACES          | Pełna krzywa ACES              |
| 4   | Uncharted     | Krzywa z gry Uncharted 2       |
| 5   | AgX           | Nowoczesny, zachowuje kolory   |
| 6   | Lottes        | Kontrolowany kontrast          |
| 7   | Uchimura      | GT Tonemap (Gran Turismo)      |
| 8   | Drago         | Logarytmiczny                  |
| 9   | RomBinDaHouse | Eksponencjalny                 |
| 10  | Ottosson      | Perceptualny (LMS)             |

**Najłatwiejsze zwiększenie jasności:**

- Użyj `TONEMAP_ID_NONE` (0) - całkowicie wyłącza kompresję HDR→SDR
- Zmień parametry `minLum`/`maxLum` w funkcji `ApplyToneMappingTransform`

### 1.4 SDR Scale (`shaders/include/oetf.hlsl`)

```hlsl
float4 OutputSDR(float4 linCol, float sdr_scale)
{
    return float4(linCol.xyz * sdr_scale, linCol.w);
}
```

**Modyfikacja:** Zwiększ `sdr_scale` dla globalnego zwiększenia jasności SDR output.

### 1.5 OETF - Funkcje Transferu Gamma (`shaders/include/oetf.hlsl`)

```hlsl
// Standardowa funkcja (REC709):
float3 OETF_REC709(float3 linCol)
{
    // ...
    // Uproszczona wersja (zakomentowana):
    // return pow(saturate(linCol), 1.0f / 2.2f);
}
```

**Modyfikacja wykładnika gamma:**

| Wykładnik     | Efekt                  |
| ------------- | ---------------------- |
| `1.0f / 1.8f` | Jaśniejszy obraz       |
| `1.0f / 2.2f` | Standardowy (domyślny) |
| `1.0f / 2.6f` | Ciemniejszy obraz      |

### 1.6 Vibrance i Saturacja (`shaders/include/util.hlsl`)

```hlsl
float3 Vibrance(float val, float3 color)
{
    val = saturate(val);
    return pow(saturate(3.0f * val * val - 2.0f * val * val * val), 1.0f / (color + 1e-5));
}

float4 Desaturate(float4 base_colour, float saturation)
{
    return float4(lerp(dot(base_colour.rgb, float3(0.30, 0.59, 0.11)), base_colour.rgb, saturation), base_colour.a);
}
```

### 1.7 Rekomendowane Podejścia do Zwiększenia Jasności

| Priorytet | Metoda                          | Plik               | Efekt                              |
| --------- | ------------------------------- | ------------------ | ---------------------------------- |
| 1         | Zwiększ `sdr_scale`             | `oetf.hlsl`        | Globalny boost jasności SDR        |
| 2         | Zmień gamma (`1/2.2` → `1/1.8`) | `oetf.hlsl`        | Rozjaśnienie średnich tonów        |
| 3         | Tonemapping `NONE`              | `tonemapping.hlsl` | Usunięcie kompresji HDR→SDR        |
| 4         | Zwiększ bloom `intensity`       | `bloomcutoff.hlsl` | Więcej "glow" na jasnych obszarach |
| 5         | Użyj `SetBrightness()`          | `util.hlsl`        | Mnożnik jasności per-obiekt        |
