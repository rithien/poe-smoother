## Global Illumination Fix

### Bug 1: Repatch nie aktualizuje wartosci (FIXED)

**Problem**: Po zmianie sliderow repatch pokazuje "modified 0 files"

**Przyczyna**: `UpdateConfig()` wywoływane tylko w konstruktorze, nie po zmianie wartości sliderów.

**Rozwiązanie**: Override `ApplyAsync()` w `GlobalIlluminationPatcher.cs`:

```csharp
public override async Task<PatchResult> ApplyAsync(...)
{
    UpdateConfig();  // Odśwież config przed patchem
    return await base.ApplyAsync(...);
}
```

---

### Bug 2: Revert nie dziala - "No patches have been applied yet" (Fixed)

**Problem**: Po zaaplikowaniu tylko GI patcha, kliknięcie "Restore" pokazuje komunikat "No patches have been applied yet."

**Lokalizacja**: `MainViewModel.cs` linia 1478-1484

**Kod z bugiem**:

```csharp
var patchCount = appliedPatchers.Count;
if (IsZoomApplied) patchCount++;
if (IsMapRevealApplied) patchCount++;
if (IsVignetteApplied) patchCount++;
// BRAKUJE: if (IsGiApplied) patchCount++;

if (patchCount == 0)
{
    StatusText = "No patches have been applied yet.";  // <-- Ten komunikat!
    return;
}
```

**Przyczyna**: `IsGiApplied` nie jest uwzględnione w liczeniu zastosowanych patchy. Gdy tylko GI jest applied, `patchCount` pozostaje 0.

**Rozwiązanie**: Dodać brakującą linię w `RevertPatchesAsync()`:

```csharp
var patchCount = appliedPatchers.Count;
if (IsZoomApplied) patchCount++;
if (IsGiApplied) patchCount++;        // <-- DODAĆ
if (IsMapRevealApplied) patchCount++;
if (IsVignetteApplied) patchCount++;
```

**Plik do modyfikacji**: `src/PoeEditor.UI/ViewModels/MainViewModel.cs` linia ~1483
