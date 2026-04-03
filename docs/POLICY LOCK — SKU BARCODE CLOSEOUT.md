# POLICY LOCK — SKU / BARCODE SERVER-ONLY + BARCODE IMMUTABLE — CLOSEOUT

**Prepared by**: Claude OPUS 4.6 — AGENT MODE  
**Date**: 2026-03-05  
**Status**: ✅ COMPLETE — awaiting user approval  
**Build**: 0 errors, 0 warnings  
**Tests**: 233 passed, 0 failed, 0 skipped (228 prior + 5 new)

---

## 1) FINAL POLICY (PERMANENT — COPY TO ALL FUTURE PHASES)

### 1.1 — Identifier Generation

| Identifier | Generator | When Generated | Format |
|-----------|-----------|---------------|--------|
| **SKU** | **Backend only** (`IdentifierGenerator.GenerateSkuAsync`) | When `sku` is null/empty/omitted on `POST /variants` | 10-digit zero-padded numeric (counter), e.g. `"0000000042"` |
| **Barcode** | **Backend only** (`IdentifierGenerator.GenerateBarcode`) | When `barcode` is null/empty/omitted on `POST /variants` | 13-digit random numeric (EAN-like), e.g. `"4829103756284"` |

**Manual override**: Client may send a custom SKU or barcode at creation time. Backend preserves the value as-is, subject to global uniqueness (409 on duplicate).

**UI MUST NOT generate SKU or Barcode under any circumstance.** The UI may only send user-typed values or null (for server generation).

### 1.2 — Editability Rules

| Field | On Create | On Update | Immutable? |
|-------|-----------|-----------|------------|
| **SKU** | Optional (leave empty → server generates) | Editable (subject to uniqueness, server validated) | No |
| **Barcode** | Optional (leave empty → server generates), or manual entry (unique) | **READ-ONLY. Cannot be changed. Ever.** | **YES — IMMUTABLE** |

### 1.3 — Error Codes

| Error Code | HTTP Status | Trigger | User Message (Arabic) |
|-----------|-------------|---------|----------------------|
| `BARCODE_IMMUTABLE` | 409 | `PUT /variants/{id}` with barcode different from stored value | الباركود غير قابل للتعديل بعد الإنشاء |
| `CONFLICT` | 409 | `PUT /variants/{id}` with SKU that already exists on another variant | رمز SKU مستخدم بالفعل |
| `BARCODE_ALREADY_EXISTS` | 409 | `POST /variants` with barcode that already exists | الباركود مستخدم بالفعل |
| `BARCODE_RETIRED` | 409 | `POST /variants` with barcode that was retired (deleted variant) | الباركود متقاعد |
| `CONFLICT` | 409 | `POST /variants` with SKU that already exists | رمز SKU مستخدم بالفعل |
| `VALIDATION_FAILED` | 400 | `PUT /variants/{id}` with empty SKU string | رمز SKU مطلوب |

### 1.4 — Absolute Rules (Non-Negotiable)

1. **Backend is the sole source of truth** for all identifiers. Even if UI is compromised or malicious, the backend rejects invalid mutations.
2. **UI MUST NOT contain any identifier generation logic** — no random barcode generators, no SKU counters, no local uniqueness checks that could override server.
3. **Barcode is WRITE-ONCE** — assigned at variant creation, immutable forever after. Backend rejects changes with `BARCODE_IMMUTABLE` (409).
4. **SKU is editable** — but server-validated for global uniqueness on every update.
5. **UI always renders identifiers from server response**, never from local state.

---

## 2) BACKEND ENFORCEMENT — DETAILS

### 2.1 — Routes & Behavior

#### `POST /api/v1/variants` — Create

| Field | Request Value | Backend Action |
|-------|--------------|----------------|
| `sku` | null / empty / omitted | Backend generates 10-digit numeric SKU |
| `sku` | non-empty string | Preserved as-is; 409 if duplicate |
| `barcode` | null / empty / omitted | Backend generates 13-digit numeric barcode |
| `barcode` | non-empty string | Preserved as-is; 409 if duplicate or retired |

#### `PUT /api/v1/variants/{id}` — Update

| Field | Request Value | Backend Action |
|-------|--------------|----------------|
| `sku` | null / omitted | No change to SKU |
| `sku` | non-empty string | Updated if globally unique; 400 if empty string; 409 if duplicate |
| `barcode` | null / omitted | No change (ignored) |
| `barcode` | same as stored | No change (accepted silently) |
| `barcode` | different from stored | **REJECTED — 409 `BARCODE_IMMUTABLE`** |

#### `GET /api/v1/barcodes/{barcode}` — Barcode Lookup

Returns the authoritative variant for any barcode (generated or manual). Globally unique.

#### `GET /api/v1/variants/by-sku/{sku}` — SKU Lookup

Returns the authoritative variant for any SKU (generated or manual). Globally unique.

### 2.2 — Code Changes

| File | Change |
|------|--------|
| `src/ElshazlyStore.Domain/Common/ErrorCodes.cs` | Added `BarcodeImmutable = "BARCODE_IMMUTABLE"` |
| `src/ElshazlyStore.Api/Endpoints/VariantEndpoints.cs` | `UpdateAsync`: added barcode immutability guard — loads `BarcodeReservation` via Include, compares `req.Barcode` against stored barcode, returns 409 `BARCODE_IMMUTABLE` if different. Added `Barcode` field to `UpdateVariantRequest` record. |

### 2.3 — UpdateAsync Logic (Pseudocode)

```
PUT /variants/{id}
1. Load variant + BarcodeReservation
2. If variant not found → 404
3. If req.Barcode is not null:
   a. Get storedBarcode from BarcodeReservation
   b. If req.Barcode ≠ storedBarcode → 409 BARCODE_IMMUTABLE
4. If req.Sku is not null:
   a. If empty → 400 VALIDATION_FAILED
   b. If duplicate → 409 CONFLICT
   c. Else → update SKU
5. Apply other fields (Color, Size, Prices, IsActive)
6. SaveChanges → 200 OK
```

---

## 3) UI ENFORCEMENT — DETAILS

### 3.1 — Variant Form Field Rules

| Field | Create Mode | Edit Mode |
|-------|------------|-----------|
| **SKU** | Optional. TextBox editable. Helper text: "اتركه فارغًا ليتم توليده تلقائيًا من السيرفر". If empty → sends `null` → server generates. | **Required** (client-side validation). TextBox editable. |
| **Barcode** | Optional. TextBox editable. Helper text: "اتركه فارغًا ليتم توليده تلقائيًا من السيرفر". If empty → sends `null` → server generates. | **Read-only + Disabled.** `IsReadOnly="{Binding IsEditMode}"` + `IsEnabled="{Binding IsEditMode, Converter={StaticResource InvBool}}"`. Cannot be typed into or changed. |
| **Product** | Required (product picker). | Read-only (display only, no picker). |

### 3.2 — UI Does NOT Send Barcode on Update

The `UpdateVariantRequest` DTO in the Desktop project (`Models/Dtos/VariantDto.cs`) has **no `Barcode` property**. The `SaveAsync` method in `VariantsViewModel.cs` constructs `UpdateVariantRequest` without any barcode field. Even if the barcode TextBox were somehow modified in edit mode, the value would never be sent to the server.

**Double protection:**
1. **UI layer**: TextBox is `IsReadOnly` + `IsEnabled=false` in edit mode — user cannot modify.
2. **DTO layer**: `UpdateVariantRequest` has no `Barcode` field — even if VM state changes, it's not serialized.
3. **Backend layer**: Even if a malicious client sends `barcode` in the JSON body, the server compares it against stored value and rejects with 409.

### 3.3 — Code Changes

| File | Change |
|------|--------|
| `src/ElshazlyStore.Desktop/Views/Pages/VariantsPage.xaml` | Barcode TextBox: added `IsReadOnly="{Binding IsEditMode}"` and `IsEnabled="{Binding IsEditMode, Converter={StaticResource InvBool}}"` — field is visually disabled and non-editable in edit mode. |

### 3.4 — No Changes Needed

| File | Reason |
|------|--------|
| `Models/Dtos/VariantDto.cs` — `UpdateVariantRequest` | Already has no `Barcode` property. No change needed. |
| `ViewModels/VariantsViewModel.cs` — `SaveAsync` edit branch | Already does not include barcode in update body. No change needed. |

---

## 4) AUTOMATED TEST RESULTS

### Build
```
Build succeeded.    0 Warning(s)    0 Error(s)
```

### Tests
```
Passed!  - Failed: 0, Passed: 233, Skipped: 0, Total: 233, Duration: 16 s
```

### New Policy Lock Tests (5/5 pass)

| # | Test Name | Description | Status |
|---|-----------|-------------|--------|
| 15 | `UpdateVariant_ChangeBarcode_Returns409_BarcodeImmutable` | Update with different barcode → 409 + `BARCODE_IMMUTABLE` in response body | ✅ |
| 16 | `UpdateVariant_SameBarcode_Succeeds` | Update sending same barcode back → 200 OK (no-op, accepted) | ✅ |
| 17 | `UpdateVariant_OmitBarcode_Succeeds` | Update without barcode field → 200 OK (field ignored) | ✅ |
| 18 | `UpdateVariant_ChangeSkuToExisting_Returns409` | Update SKU to one already taken by another variant → 409 | ✅ |
| 19 | `UpdateVariant_ChangeSkuToNewUnique_Succeeds` | Update SKU to a new globally unique value → 200 OK | ✅ |

### Prior Tests (228/228 pass — no regressions)

All identifier generation tests (1–14) and all other tests (214) continue to pass unchanged.

---

## 5) HUMAN RUNNABLE VERIFICATION

### Prerequisites
- Backend running: `cd src/ElshazlyStore.Api; dotnet run`  
- Obtain admin token:
```powershell
$login = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/auth/login" -Method POST -ContentType "application/json" -Body '{"username":"admin","password":"Admin@123!"}'
$TOKEN = $login.accessToken
$headers = @{ Authorization = "Bearer $TOKEN"; "Content-Type" = "application/json" }
```

### Step 1 — Create variant with omitted SKU + barcode (server generates)

```powershell
$prod = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/products" -Method POST -Headers $headers -Body '{"name":"PolicyLock Test"}'
$pid = $prod.id

$v = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/variants" -Method POST -Headers $headers -Body "{`"productId`":`"$pid`",`"color`":`"Red`"}"
Write-Host "SKU: $($v.sku)      (expect 10-digit numeric)"
Write-Host "Barcode: $($v.barcode)  (expect 13-digit numeric)"
$vid = $v.id
$bc = $v.barcode
```

### Step 2 — Attempt to change barcode → REJECTED (409 BARCODE_IMMUTABLE)

```powershell
try {
    $null = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/variants/$vid" -Method PUT -Headers $headers -Body '{"barcode":"CHANGED-BARCODE"}'
    Write-Host "ERROR: Should have been rejected!"
} catch {
    Write-Host "Status: $($_.Exception.Response.StatusCode)  (expect 409)"
    $err = $_.ErrorDetails.Message | ConvertFrom-Json
    Write-Host "Error: $($err.title)  (expect BARCODE_IMMUTABLE)"
}
```

### Step 3 — Send same barcode back → accepted (no-op)

```powershell
$null = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/variants/$vid" -Method PUT -Headers $headers -Body "{`"barcode`":`"$bc`",`"color`":`"Blue`"}"
Write-Host "Same-barcode update: OK"
```

### Step 4 — Change SKU to existing → REJECTED (409 CONFLICT)

```powershell
$v2 = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/variants" -Method POST -Headers $headers -Body "{`"productId`":`"$pid`",`"color`":`"Green`"}"
$sku1 = $v.sku

try {
    $null = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/variants/$($v2.id)" -Method PUT -Headers $headers -Body "{`"sku`":`"$sku1`"}"
    Write-Host "ERROR: Should have been rejected!"
} catch {
    Write-Host "Status: $($_.Exception.Response.StatusCode)  (expect 409)"
}
```

### Step 5 — Change SKU to new unique → accepted

```powershell
$null = Invoke-RestMethod -Uri "http://localhost:5238/api/v1/variants/$($v2.id)" -Method PUT -Headers $headers -Body '{"sku":"NEW-UNIQUE-SKU-001"}'
Write-Host "SKU change to unique: OK"
```

### Step 6 — UI manual check

| Check | Action | Expected |
|-------|--------|----------|
| A | Open variant edit form (click تعديل on any variant) | Barcode field is greyed out, non-editable, cannot type |
| B | Try clicking into barcode field | No cursor, no typing allowed |
| C | Open variant create form (click إضافة) | Barcode field is editable, helper text visible |
| D | Leave SKU empty, save | Server generates SKU, toast shows generated value |

---

## 6) DECISION RECORD

| Decision | Choice | Justification |
|----------|--------|---------------|
| Barcode immutability enforcement | Triple-layer: UI (IsReadOnly+Disabled) + DTO (no field) + Backend (409 guard) | Defense in depth — even malicious API clients cannot change barcode |
| Error code for barcode change attempt | `BARCODE_IMMUTABLE` (409 Conflict) | Clear, machine-readable, distinct from other barcode errors |
| UpdateVariantRequest includes optional Barcode field | Yes — backend explicitly accepts and validates | Silent ignoring of unknown JSON fields is not an enforcement strategy. Explicit field + explicit rejection = auditable policy. |
| SKU editability | Allowed on update, server-validated uniqueness | Business requirement — SKUs may need correction. Barcode is the permanent physical label. |
| UI barcode disabled vs. hidden on edit | Disabled (greyed out, shown) | User sees the barcode value for reference but cannot modify. Hidden would lose visibility. |

---

## 7) FILES MODIFIED (COMPLETE LIST)

| File | Change Summary |
|------|---------------|
| `src/ElshazlyStore.Domain/Common/ErrorCodes.cs` | +1 const: `BarcodeImmutable = "BARCODE_IMMUTABLE"` |
| `src/ElshazlyStore.Api/Endpoints/VariantEndpoints.cs` | `UpdateAsync`: include BarcodeReservation, barcode immutability guard (409). `UpdateVariantRequest`: added `Barcode` optional field. |
| `src/ElshazlyStore.Desktop/Views/Pages/VariantsPage.xaml` | Barcode TextBox: `IsReadOnly="{Binding IsEditMode}"` + `IsEnabled` inverted — read-only & disabled in edit mode. |
| `tests/ElshazlyStore.Tests/Api/IdentifierGenerationTests.cs` | +5 tests: barcode immutability (change/same/omit), SKU update conflict, SKU update valid. +1 helper: `PutAuth`. |

---

## 8) STOP — AWAITING USER APPROVAL

All implementation is complete. All tests pass (233/233). 

**Do not proceed to any further phase until the user explicitly approves this closeout.**
