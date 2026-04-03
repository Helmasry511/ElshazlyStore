# UI-BACKEND CUSTOMER CREDIT PROFILE 3A-R1 — ATTACHMENT ACCESS + COPYABLE FIELDS + ATTACHMENT AUDIT FIX — CLOSEOUT

## Phase
**CP-3A-R1 — Attachment Open/Download UX + Copyable Readonly Fields + Attachment Audit Save Fix**

## Status
**COMPLETE** — Build: 0 errors, 0 warnings — Tests: 274 passed, 0 failed

---

## 1) Root Cause of the Attachment Upload / Audit Failure

### Bug Location: **Backend** (`AuditInterceptor.cs`)

### Exact Failure Path
1. User uploads an attachment → `POST /api/v1/customers/{id}/attachments` is called.
2. EF Core saves the `CustomerAttachment` entity which has a `FileContent` property of type `byte[]`.
3. `AuditInterceptor.CaptureChanges()` runs and includes all entity properties — including `FileContent` — into the `NewValues` dictionary.
4. `Serialize(newValues)` calls `JsonSerializer.Serialize(dict, JsonOptions)`. The `byte[]` is JSON-serialised as a base64 string (e.g. ~6.7 MB of base64 text for a 5 MB file).
5. `Truncate(serializedJson, 4096)` cuts the string to exactly 4 096 characters. This splits the string mid-way through a JSON string value, producing structurally **invalid JSON**.
6. `AuditInterceptor.WriteAuditLogsAsync()` tries to insert an `AuditLog` row with `NewValues = <invalid JSON>`.
7. PostgreSQL column `new_values` is configured as `jsonb` (see `AuditLogConfiguration.cs`). PostgreSQL rejects the invalid value with:
   ```
   invalid input syntax for type json
   ```
8. The whole request transaction rolls back → 500 returned to the client.

### Explicit Answer: Backend or Desktop?
**Backend only.** The Desktop client correctly sends a valid multipart upload. The failure occurs entirely inside the backend's `AuditInterceptor` when it attempts to serialize a large binary blob and then truncates the result to an invalid JSON substring before writing to a `jsonb` column.

---

## 2) Fix Applied

### File: `src/ElshazlyStore.Infrastructure/Persistence/Interceptors/AuditInterceptor.cs`

Added `"FileContent"` to the existing `RedactedFields` set:

```csharp
private static readonly HashSet<string> RedactedFields =
[
    "PasswordHash",
    "Token",
    "TokenHash",
    "ReplacedByToken",
    "ReplacedByTokenHash",
    "RefreshToken",
    "Secret",
    "FileContent",  // binary attachment data — excluded to prevent invalid-jsonb truncation
];
```

**Why this is correct:**
- `RedactedFields` already skips sensitive properties from OldValues/NewValues serialisation.
- `FileContent` is a `bytea` blob — it has no meaningful audit value as text. The audit record for an attachment upload still captures `FileName`, `Category`, `FileSize`, `ContentType`, and `CreatedAtUtc`. That is sufficient for a complete audit trail.
- After this fix, `NewValues` for a `CustomerAttachment INSERT` is a small, valid JSON object (all metadata fields), which PostgreSQL accepts as `jsonb` without error.

---

## 3) Exact Files Changed

| File | What changed |
|------|-------------|
| `src/ElshazlyStore.Infrastructure/Persistence/Interceptors/AuditInterceptor.cs` | Added `"FileContent"` to `RedactedFields` with explanatory comment |
| `src/ElshazlyStore.Desktop/ViewModels/CustomersViewModel.cs` | Added `OpenAttachmentCommand` (download to temp + shell-open); added auto-open after `DownloadAttachmentCommand` saves |
| `src/ElshazlyStore.Desktop/Views/Pages/CustomersPage.xaml` | Added `فتح` (Open) button to each attachment item; replaced 6 `TextBlock` fields with readonly `TextBox` for copyability |
| `src/ElshazlyStore.Desktop/Localization/Strings.cs` | Added `Customers_OpenAttachment` accessor |
| `src/ElshazlyStore.Desktop/Localization/Strings.resx` | Added `Customers_OpenAttachment` = `فتح` |

---

## 4) Attachment Access / Open / Download UX Added

### Before (CP-3A)
- Attachment list was visible in customer details.
- A `⬇` button opened a `SaveFileDialog`, wrote the bytes, showed a notification. File was saved but **never opened** — user had to navigate to the save location manually.
- No "open" action existed. Documents were saved but unreachable without OS file manager.

### After (CP-3A-R1)
Each attachment row in the customer details now has **three action buttons**:

| Button | Label | Behaviour |
|--------|-------|-----------|
| **Open** | `فتح` | Downloads the file bytes from the API, writes to a unique temp path (`%TEMP%\elshazly_{id}.ext`), and immediately opens it with the OS default viewer (`Process.Start` with `UseShellExecute = true`). No save dialog. |
| **Download** | `⬇` | Opens `SaveFileDialog` so user chooses the save location, writes bytes, **then auto-opens** the saved file in its default application. |
| **Delete** | `✕` | Confirms and deletes (unchanged from CP-3A). |

**The "فتح" / Open button is the primary discovery path** — clearly labelled, accent-coloured (visually prominent), no dialog box needed. The user can open a national ID scan or contract PDF with one click.

### Supported Attachment Operations (updated)

| Operation | Status | Notes |
|-----------|--------|-------|
| Upload | ✅ | Unchanged from CP-3A |
| List | ✅ | Unchanged from CP-3A |
| **Open** | ✅ **NEW** | Download to temp + shell-open, instant access |
| Download (Save As) | ✅ | Saves to user-chosen path, then auto-opens |
| Delete | ✅ | Unchanged from CP-3A |

---

## 5) Readonly-Copyable Field Behaviour Added

### Affected Fields
All six fields listed below have been changed from `TextBlock` (not selectable) to `TextBox` with `IsReadOnly="True"` (selectable, copyable):

| Field | Context |
|-------|---------|
| **Phone** | Contact info section, always shown |
| **Phone2** | Contact info section, always shown |
| **WhatsApp** | Contact info section, shown when populated |
| **رقم المحفظة الإلكترونية** (WalletNumber) | Digital payment section, shown when populated |
| **معرف إنستاباي** (InstaPayId) | Digital payment section, shown when populated |
| **رقم بطاقة الرقم القومي** (NationalIdNumber) | Commercial info section, shown when populated |

### How they remain readonly
```xml
IsReadOnly="True"
Background="Transparent"
BorderThickness="0"
Padding="0"
IsTabStop="False"
```
The `TextBox` is visually indistinguishable from the previous `TextBlock` (transparent background, no border, same font/size/foreground). It is **not** a normal editable field — `IsReadOnly="True"` prevents any input. However, the user can:
- Click and drag to select text
- Use `Ctrl+A` to select all
- Use `Ctrl+C` to copy
- Right-click for the standard WPF context menu (Select All / Copy)

`IsTabStop="False"` keeps the Tab navigation flow unchanged.

### Fields intentionally NOT changed
- **Name**: `TextBlock` kept — the name is large/bold display text, not a number to copy.
- **Code**: `TextBlock` kept — displayed inline, not a primary copy target.
- **Notes**: `TextBlock` kept — multi-line display text with `TextWrapping`, not a number to copy.
- **CommercialName**, **CommercialAddress**: `TextBlock` kept — display names/addresses, not the identity numbers that the spec calls out.

---

## 6) Build / Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 274, Skipped: 0, Total: 274
Duration: 18 s
```

---

## 7) Concrete Human Test Script

### Prerequisites
- Backend running on localhost:5238
- Logged in as admin (has CUSTOMERS_READ + CUSTOMERS_WRITE)

### Test A — Attachment Audit Fix (GOAL C)
1. Navigate to Customers page.
2. Open details for any customer.
3. Click **رفع صورة بطاقة** and upload a JPG or PDF file (any size ≤ 5 MB).
4. **Expected:** Green notification "تم رفع المرفق بنجاح" appears. No red error, no 500.
5. **Verify:** Attachment appears in the list immediately.
6. (Optional) Check server logs / audit_logs table — no `invalid input syntax for type json` error. The `CustomerAttachment` INSERT audit row has `new_values` = small valid JSON with `fileName`, `category`, `fileSize`, `contentType`, `createdAtUtc` — but **no** `fileContent` field.

### Test B — Open Attachment (GOAL A)
1. In the attachment list (same customer details), find any listed attachment.
2. Click the **فتح** button (accent blue button on the left of each row).
3. **Expected:** File opens immediately in the default application (PDF → PDF viewer, image → image viewer).
4. No save dialog appears.

### Test C — Download Attachment (GOAL A)
1. Click the **⬇** button on the same attachment.
2. A `SaveFileDialog` appears. Pick any destination.
3. Click Save.
4. **Expected:** File is saved to the chosen path AND immediately opened in the default application.

### Test D — Copyable Fields (GOAL B)
1. Open customer details for a customer that has Phone, WhatsApp, WalletNumber or NationalIdNumber filled in.
2. Click on the phone number value → a text cursor (I-beam) appears.
3. Select all text in the field (`Ctrl+A`) → text highlights.
4. Copy (`Ctrl+C`).
5. Paste into any text app → the number pastes correctly.
6. **Verify field is still readonly:** try typing in the field → nothing happens.
7. Repeat for WhatsApp, WalletNumber, InstaPayId, NationalIdNumber.

### Test E — Attachments survive customer edit
1. Upload an attachment.
2. Click **Edit** on the same customer and change any field (e.g., notes).
3. Save.
4. Reopen details.
5. **Expected:** Attachment is still visible in the list.

### Test F — No regression
1. Create a new customer → succeeds.
2. Edit a customer → succeeds.
3. Delete a customer that has attachments → succeeds (cascade).
4. Dark mode → all fields readable, no visual regression.

---

## 8) Cleanup Audit

### Touched files reviewed: 5 files (0 new, 5 modified)

| File | Changes | Cleanup action |
|------|---------|----------------|
| `AuditInterceptor.cs` | Added `"FileContent"` to `RedactedFields` | No cleanup needed |
| `CustomersViewModel.cs` | Added `OpenAttachmentAsync`; modified `DownloadAttachmentAsync` | No orphaned code; previous download method intact and improved |
| `CustomersPage.xaml` | 6 field replacements + 1 new button | All 6 replaced `TextBlock` elements confirmed removed; no dangling bindings |
| `Strings.cs` | 1 accessor added | Verified with grep: `Customers_OpenAttachment` is referenced in XAML |
| `Strings.resx` | 1 resource added | Value `فتح` set |

### Unused code detected after changes
- **None.** All new code is directly referenced.
- `Customers_OpenAttachment` in Strings.cs is consumed at line 846 of `CustomersPage.xaml`.
- `OpenAttachmentCommand` (generated from `[RelayCommand]` on `OpenAttachmentAsync`) is bound at line 847 of `CustomersPage.xaml`.

### What was intentionally left
- The `⬇` download button was kept alongside the new `فتح` button. Some users prefer to choose the save location. Both operations are useful and serve different intent.
- The `GetAttachmentCategoryDisplay` helper method was left unchanged (no reference to it was added in this phase, it exists from CP-3A as a utility).

---

## 9) Explicit Scope Confirmation

| Item | Status |
|------|--------|
| **CP-3B (Payment Statement batch printing)** | ❌ NOT implemented |
| **Prepayment / "له / عليه"** | ❌ NOT implemented |
| **Supplier parity work** | ❌ NOT implemented |
| **Print Config** | ❌ NOT implemented |
| **Broad redesign of customer page** | ❌ NOT done — only surgical targeted changes |
| **Attachment platform for other entities** | ❌ NOT done — only customer attachments touched |
