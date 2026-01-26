1. Moduł Bazowy i Transformacje (common.ffx)
   Plik ten stanowi fundament silnika renderującego, definiując globalne stałe, dane o scenie oraz podstawowe operacje matematyczne.

Zarządzanie macierzami: Funkcje takie jak GetViewProjectionTransform(), GetViewTransform() czy GetProjectionTransform() dostarczają niezbędnych macierzy do przekształcania współrzędnych z przestrzeni świata do przestrzeni ekranu.

Analiza terenu: Funkcja GetHeightmapSample() pozwala na pobieranie danych o wysokości terenu w konkretnym punkcie świata, co jest kluczowe dla efektów kolizji wizualnej i cieniowania terenu.

System spalania powierzchni: GetBurnSample() oraz BurnTemperature() obliczają wizualny postęp efektu ognia i zwęglenia na obiektach.

Optymalizacja potoku: PerformAlphaTestClip() odpowiada za odrzucanie pikseli o niskiej przezroczystości, co zapobiega nadmiernemu obciążeniu procesora graficznego (overdraw).

Adaptacja platformowa: GetWavefrontSize() rozpoznaje architekturę sprzętową (PC, PlayStation, Xbox), optymalizując wykonanie kodu dla konkretnych jednostek obliczeniowych GPU.

2. Oświetlenie i Fizyka Powierzchni (lighting.ffx)
   Ten moduł implementuje zaawansowane modele oświetlenia, w tym podejście PBR (Physically Based Rendering).

Oświetlenie GGX: Funkcja GGXSpecular() implementuje standard przemysłowy dla fizycznie poprawnych odbić błyszczących, uwzględniając strukturę mikrofasetek.

Anizotropia: WardSpecular() pozwala na renderowanie materiałów o kierunkowej strukturze, takich jak szczotkowany metal czy włosy.

Okluzja otoczenia: GetBentNormalSpecularOcclusion() wykorzystuje technikę "zgiętych normalnych", aby realistycznie przyciemniać odbicia w zagłębieniach geometrii.

Reprojekcja GI: ReprojectLighting() to zaawansowana technika Screen Space, która stabilizuje oświetlenie globalne (Global Illumination) pomiędzy klatkami animacji.

Oświetlenie punktowe i kierunkowe: Funkcje ComputePointLightParams() oraz ComputeDirectionalLightParams() obliczają tłumienie i intensywność światła dla różnych źródeł.

3. Renderowanie i Mieszanie Terenu (ground.ffx)
   Koncentruje się na specyficznych potrzebach renderowania podłoża, umożliwiając łączenie wielu tekstur.

Mieszanie materiałów: GroundMaterialMulAdd() to funkcja matematyczna łącząca właściwości (albedo, chropowatość, normalne) kilku różnych typów podłoża w jeden finalny wynik.

Współczynnik Fresnela: GetReflectionCoefficient3() oblicza, jak silne powinno być odbicie w zależności od kąta patrzenia gracza na ziemię.

Generowanie UV: GenerateBlendUVs() tworzy współrzędne dla masek mieszania tekstur na podstawie ich pozycji w świecie, co zapewnia brak widocznych "szwów" na dużych powierzchniach.

4. Definicje Materiałów i Post-procesy (texturing.ffx)
   Zawiera struktury danych i pomocnicze funkcje teksturujące.

Inicjalizacja materiałów: Funkcje InitMaterial() przygotowują domyślne parametry dla różnych modeli oświetlenia (Phong, GGX, Anizotropowe).

Korekcja Vibrance: Vibrance() umożliwia nasycenie kolorów w sposób inteligentny, chroniąc już nasycone barwy przed przesterowaniem.

5. System Cieni (shadows.ffx)
   Odpowiada za rzucanie cieni i ich jakość wizualną.

Miękkie cienie (PCSS): GetShadowmapBlockerDist() oblicza odległość od przeszkody, co pozwala funkcji ShadowMap() na dynamiczne rozmywanie krawędzi cienia (im dalej od obiektu, tym cień jest bardziej miękki).

Cienie chmur: GetCloudsIntensity() dodaje warstwę dynamicznych cieni rzucanych przez chmury poruszane wiatrem.

Integracja cieni: IntegrateShadowMap() wykonuje wielokrotne próbkowanie mapy cieni, co eliminuje artefakty schodkowania (aliasing).

Momenty VSM: ComputeMoments() oblicza dane statystyczne głębi, używane do techniki Variance Shadow Maps w celu uzyskania bardzo płynnych przejść tonalnych w cieniach.
