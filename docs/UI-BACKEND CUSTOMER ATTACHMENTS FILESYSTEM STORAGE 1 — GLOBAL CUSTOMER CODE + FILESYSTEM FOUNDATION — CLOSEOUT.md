# UI-BACKEND CUSTOMER ATTACHMENTS FILESYSTEM STORAGE 1 — GLOBAL CUSTOMER CODE + FILESYSTEM FOUNDATION — CLOSEOUT

## Phase
**CAFS-1 — Customer Global Code + Filesystem Storage Foundation**

## Status
**COMPLETE** — Build: 0 errors, 0 warnings — Tests: 274 passed, 0 failed

---

## 1) Summary of What Was Implemented

### Backend
- Added immutable `CustomerCode` field to `Customer` entity (format: `YYYY-NNNNNN`, digits and `-` only)
- `CustomerCode` is auto-generated server-side on customer creation, never changed after that, never reused
- Extended `CustomerAttachment` entity with filesystem metadata: `StoredFileName`, `RelativePath`, `CustomerCode`
- Made `FileContent` (bytea) nullable — new attachments store `null`, legacy attachments retain their blob
- Created `CustomerAttachmentStorageService` for filesystem operations (save, read, delete, validate)
- Modified upload endpoint to write files to disk under `<RootPath>/<CustomerCode>/<StoredFileName>`
- Modified download endpoint to serve from filesystem (new) or database blob (legacy) — safe coexistence
- Modified delete endpoint to also remove the physical file from disk
- Added `AttachmentStorage:RootPath` configuration key in `appsettings.json`
- Generated migration `0020_customer_code_filesystem_storage` with safe backfill of existing customers

### Desktop
- Added `CustomerCode` property to desktop `CustomerDto` so it flows to the UI for future display

---

## 2) Root Cause / Current-State Audit of Previous Attachment Storage

### Previous State
- Customer attachments were stored entirely as PostgreSQL `bytea` blobs in the `customer_attachments` table (`FileContent` column)
- No filesystem storage existed — zero filesystem code found in the codebase
- No `StoredFileName`, `RelativePath`, or `CustomerCode` metadata existed on attachments
- The existing `Code` field on `Customer` used format `CUST-XXXXXX` (contains letters — not filesystem-safe per spec)
- The existing `Code` field was mutable (could be changed via `UpdateCustomerRequest`)
- No file storage service or interface existed anywhere in the infrastructure layer
- No `AttachmentStorage` configuration existed in `appsettings.json`
- No data root path was configured anywhere

### Root Cause of Change
The functional spec (`docs/Customer Attachments Filesystem Storage — Functional Spec.md`) mandates transitioning from blob storage to filesystem storage for customer attachments, requiring:
1. An immutable global customer code safe for folder naming
2. Per-customer folders on disk
3. Database-stored metadata only (not file content) for new attachments

---

## 3) CustomerCode Strategy Implemented

### Exact Format
`YYYY-NNNNNN`

Examples:
- `2026-000001`
- `2026-000002`
- `2027-000001`

### Character Set
Digits (`0-9`) and hyphen (`-`) only. No letters, no slashes, no special characters.

### Generation
- Auto-generated server-side during customer creation (`CreateAsync` in `CustomerEndpoints.cs`)
- Also auto-generated during CSV import (`CommitCustomersAsync` in `ImportService.cs`)
- Year is extracted from `DateTime.UtcNow`
- Sequence number is determined by querying the max existing `CustomerCode` for the current year and incrementing

### Uniqueness Policy
- Unique index in PostgreSQL: `IX_customers_CustomerCode` (UNIQUE)
- Format guarantees no collision across years (year prefix)
- Sequence is monotonically increasing within each year

### Non-Reuse Policy
- `CustomerCode` is never modified after creation (the `UpdateAsync` endpoint does not accept or set `CustomerCode`)
- Soft-delete (`IsActive = false`) does not free the code — the row and code remain in the database
- The deactivated customer's code is still counted in the sequence, so the next customer gets a higher number

### Immutability
- `CustomerCode` is not present in `UpdateCustomerRequest` — it cannot be changed via API
- No endpoint or code path modifies `CustomerCode` after initial creation
- The `CreateAsync` endpoint generates it; the `UpdateAsync` endpoint ignores it

### Relationship to Existing `Code` Field
- The existing `Code` field (`CUST-XXXXXX`) is **preserved unchanged**
- `Code` continues to serve as the business-facing customer code
- `CustomerCode` is the new immutable global identifier for filesystem folder naming
- Both fields coexist; they serve different purposes

---

## 4) Resolved Root Storage Path Strategy

### Configuration
```json
{
  "AttachmentStorage": {
    "RootPath": "CustomerAttachments"
  }
}
```

### Resolution Logic (in `CustomerAttachmentStorageService`)
1. If `AttachmentStorage:RootPath` is set and is an absolute path → use as-is
2. If `AttachmentStorage:RootPath` is set and is a relative path → resolve relative to `AppContext.BaseDirectory`
3. If not configured → default to `<AppContext.BaseDirectory>/CustomerAttachments`

### Effective Path at Runtime
`<API binary output directory>\CustomerAttachments\`

Example: `C:\Users\Dell\Desktop\New Shazly\src\ElshazlyStore.Api\bin\Debug\net8.0\CustomerAttachments\`

This is the same root as the running server. For production deployment, `AttachmentStorage:RootPath` can be set to an absolute path (e.g., `D:\ElshazlyData\CustomerAttachments`).

### Per-Customer Folder Structure
```
CustomerAttachments/
├── 2026-000001/
│   ├── 20260401192345_a1b2c3d4.pdf
│   └── 20260401192501_e5f6g7h8.jpg
├── 2026-000002/
│   └── 20260401193012_i9j0k1l2.png
```

---

## 5) Exact Files Created/Modified

### Created (new files)
| File | Purpose |
|------|---------|
| `src/ElshazlyStore.Infrastructure/Services/CustomerAttachmentStorageService.cs` | Filesystem storage service: save, read, delete, validate, folder creation |
| `src/ElshazlyStore.Infrastructure/Persistence/Migrations/20260401190246_0020_customer_code_filesystem_storage.cs` | Migration: adds CustomerCode to customers, adds metadata columns to customer_attachments, backfills existing customers |
| `src/ElshazlyStore.Infrastructure/Persistence/Migrations/20260401190246_0020_customer_code_filesystem_storage.Designer.cs` | Migration designer (auto-generated) |

### Modified (existing files)
| File | What Changed |
|------|-------------|
| `src/ElshazlyStore.Domain/Entities/Customer.cs` | Added `CustomerCode` property with XML doc |
| `src/ElshazlyStore.Domain/Entities/CustomerAttachment.cs` | Added `StoredFileName`, `RelativePath`, `CustomerCode`; made `FileContent` nullable (`byte[]?`) |
| `src/ElshazlyStore.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs` | Added `CustomerCode` column config (max 20, required) + unique index |
| `src/ElshazlyStore.Infrastructure/Persistence/Configurations/CustomerAttachmentConfiguration.cs` | Added `StoredFileName`, `RelativePath`, `CustomerCode` columns; `FileContent` no longer required |
| `src/ElshazlyStore.Infrastructure/DependencyInjection.cs` | Registered `CustomerAttachmentStorageService` as singleton |
| `src/ElshazlyStore.Api/Endpoints/CustomerEndpoints.cs` | Added `CustomerCode` to DTOs, auto-generation on create, filesystem storage for upload/download/delete, `GenerateCustomerCodeAsync` method |
| `src/ElshazlyStore.Api/appsettings.json` | Added `AttachmentStorage:RootPath` config key |
| `src/ElshazlyStore.Infrastructure/Services/ImportService.cs` | Generate `CustomerCode` during CSV customer import |
| `src/ElshazlyStore.Desktop/Models/Dtos/CustomerDto.cs` | Added `CustomerCode` property to desktop DTO |
| `src/ElshazlyStore.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` | Auto-updated by EF |

---

## 6) Exact Backend/Storage Contracts/Entities/DTOs Touched

### Domain Entities
| Entity | Change |
|--------|--------|
| `Customer` | Added `CustomerCode` (string, required, max 20, unique) |
| `CustomerAttachment` | Added `StoredFileName` (string?, max 255), `RelativePath` (string?, max 500), `CustomerCode` (string?, max 20); changed `FileContent` from `byte[]` to `byte[]?` |

### API DTOs (server-side records)
| DTO | Change |
|-----|--------|
| `CustomerDto` | Added `CustomerCode` parameter (position 3, after `Code`) |
| `CreateCustomerRequest` | Unchanged (CustomerCode is server-generated, not client-provided) |
| `UpdateCustomerRequest` | Unchanged (CustomerCode is immutable, not client-modifiable) |
| `CustomerAttachmentDto` | Unchanged (StoredFileName/RelativePath are internal, not exposed to client) |

### Desktop DTOs
| DTO | Change |
|-----|--------|
| `CustomerDto` | Added `CustomerCode` property |

### Endpoints
| Method | Route | Change |
|--------|-------|--------|
| `GET` | `/api/v1/customers` | Returns `CustomerCode` in response |
| `GET` | `/api/v1/customers/{id}` | Returns `CustomerCode` in response |
| `POST` | `/api/v1/customers` | Auto-generates `CustomerCode`; returns it in response |
| `POST` | `/api/v1/customers/{id}/attachments` | Injects `CustomerAttachmentStorageService`; saves file to filesystem; stores metadata |
| `GET` | `/api/v1/customers/{id}/attachments/{attachmentId}` | Injects `CustomerAttachmentStorageService`; reads from filesystem (new) or blob (legacy) |
| `DELETE` | `/api/v1/customers/{id}/attachments/{attachmentId}` | Injects `CustomerAttachmentStorageService`; deletes filesystem file + DB record |

---

## 7) Exact Metadata Model Now Used

### For new attachments (CAFS-1+)
| Field | Source | Value |
|-------|--------|-------|
| `Id` | Server-generated | GUID |
| `CustomerId` | From URL parameter | FK to customers |
| `FileName` | From upload | Original file name (for display) |
| `StoredFileName` | Server-generated | `{yyyyMMddHHmmss}_{8-char-guid-prefix}{ext}` |
| `RelativePath` | Server-generated | `{CustomerCode}/{StoredFileName}` |
| `CustomerCode` | From customer record | Denormalized `YYYY-NNNNNN` |
| `ContentType` | From upload | MIME type |
| `FileSize` | From upload | Bytes |
| `Category` | From query parameter | `national_id`, `contract`, or `other` |
| `FileContent` | Not stored | `null` — file is on disk |
| `CreatedAtUtc` | Server-generated | UTC timestamp |

### For legacy attachments (pre-CAFS-1)
| Field | Value |
|-------|-------|
| `StoredFileName` | `null` |
| `RelativePath` | `null` |
| `CustomerCode` | `null` |
| `FileContent` | Original bytea blob (preserved) |

---

## 8) New-Attachment Storage Behavior

1. Client calls `POST /api/v1/customers/{id}/attachments?category=national_id` with multipart file
2. Server validates: customer exists, customer has `CustomerCode`, file size ≤ 5 MB, extension allowed
3. Server calls `CustomerAttachmentStorageService.SaveFileAsync(customerCode, originalFileName, stream)`
4. Service validates customer code (digits + `-` only), ensures folder exists (`Directory.CreateDirectory`)
5. Service generates safe stored file name: `{timestamp}_{shortGuid}{ext}`
6. Service writes file to `<RootPath>/<CustomerCode>/<StoredFileName>` using `FileMode.CreateNew`
7. Server creates `CustomerAttachment` record with metadata: `StoredFileName`, `RelativePath`, `CustomerCode`, `FileContent = null`
8. Server saves to database
9. File is now on disk; metadata is in database; no blob stored

---

## 9) Old/New Attachment Coexistence

### Explicit Statement
Old (pre-CAFS-1) and new (CAFS-1+) attachments **coexist safely**.

### How Coexistence Works
- **New attachments**: `RelativePath` is non-null, `FileContent` is null → served from filesystem
- **Legacy attachments**: `RelativePath` is null, `FileContent` is non-null → served from database blob
- The download endpoint checks `RelativePath` first; if present, reads from disk. If not, falls back to `FileContent` blob
- The delete endpoint deletes the filesystem file (if `RelativePath` is set) and always removes the database record
- No old attachment is modified, moved, or deleted by this phase

### What Happens If You Upload to a Customer With Old Attachments
- The new attachment goes to the filesystem; the old ones remain as blobs
- Both appear in the attachment list
- Both can be downloaded/opened/deleted independently

---

## 10) What Was NOT Implemented

| Item | Status | Reason |
|------|--------|--------|
| **CAFS-2: Open folder button, final attachment UI** | ❌ NOT IMPLEMENTED | Scope of CAFS-2 |
| **CAFS-3: Legacy migration/backfill** | ❌ NOT IMPLEMENTED | Scope of CAFS-3 |
| **Supplier attachments** | ❌ NOT IMPLEMENTED | Out of scope |
| **Whole-app storage redesign** | ❌ NOT IMPLEMENTED | Out of scope |
| **Print Config** | ❌ NOT IMPLEMENTED | Out of scope |
| **Prepayment / "له / عليه"** | ❌ NOT IMPLEMENTED | Out of scope |
| **Payment/reporting work** | ❌ NOT IMPLEMENTED | Out of scope |

**Explicit confirmation:**
- ❌ CAFS-2 was NOT implemented
- ❌ CAFS-3 / legacy migration/backfill was NOT implemented
- ❌ No open-folder button was added to the customer grid

---

## 11) Recommended Staged Migration Path (for CAFS-3)

When CAFS-3 is ready, the recommended approach is:

1. Query all `customer_attachments` where `RelativePath IS NULL AND FileContent IS NOT NULL`
2. For each, look up the customer's `CustomerCode`
3. Write the `FileContent` blob to `<RootPath>/<CustomerCode>/<GeneratedStoredFileName>`
4. Update the record: set `StoredFileName`, `RelativePath`, `CustomerCode`; set `FileContent = NULL`
5. Run in batches to avoid memory pressure
6. After all records are migrated, consider making `FileContent` permanently null (optional schema cleanup)

This is safe because both old and new paths are already handled by the download/delete endpoints.

---

## 12) Build / Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 274, Skipped: 0, Total: 274
```

---

## 13) Concrete Human Test Script for CAFS-1

### Test A: Verify CustomerCode on New Customer
1. Start the server and desktop app
2. Go to العملاء (Customers page)
3. Create a new customer with name "عميل تجربة CAFS-1"
4. Open the customer's details
5. **Verify**: The returned data includes `CustomerCode` in format `2026-NNNNNN` (digits and `-` only)
6. Note the `CustomerCode` value

### Test B: Verify CustomerCode Immutability
1. Edit the customer created in Test A (change the name or phone)
2. Save the update
3. Open details again
4. **Verify**: `CustomerCode` has not changed — same value as before

### Test C: Verify CustomerCode Uniqueness
1. Create a second customer "عميل تجربة CAFS-1 ب"
2. Open details
3. **Verify**: `CustomerCode` is different from the first customer's code
4. **Verify**: Sequence is incremented (e.g., `2026-000001` → `2026-000002`)

### Test D: Upload New Attachment — Filesystem Storage
1. Open details of customer from Test A
2. Upload an attachment (e.g., a PDF or JPG, max 5 MB)
3. **Verify**: Upload succeeds, attachment appears in the list
4. Navigate to the filesystem: `<API output directory>\CustomerAttachments\<CustomerCode>\`
5. **Verify**: A file exists in that folder with a name like `20260401HHMMSS_xxxxxxxx.pdf`
6. **Verify**: The file is the correct content (open it)

### Test E: Download Attachment
1. From the customer details, click download/open on the attachment uploaded in Test D
2. **Verify**: File downloads/opens correctly with the original file name

### Test F: Delete Attachment — Filesystem Cleanup
1. Delete the attachment from the customer details
2. **Verify**: Attachment disappears from the list
3. Navigate to the filesystem folder
4. **Verify**: The file has been deleted from disk

### Test G: Verify Existing Customer Backfill
1. If there were existing customers before this migration, check one of them
2. **Verify**: They now have a `CustomerCode` in `YYYY-NNNNNN` format
3. **Verify**: Each existing customer has a unique code

### Test H: Verify Legacy Attachment Access (if any exist)
1. If any attachments were uploaded before this phase (stored as blob)
2. Open customer details and try to download/open that attachment
3. **Verify**: Old attachment still works (served from database blob)

---

## Cleanup Audit

### Touched files reviewed: 13 files (3 new, 10 modified)

### Unused code/resources detected
None. All code in touched files is actively referenced and necessary.

### What was removed
Nothing was removed in this phase.

### Why no removal was performed
- All existing code in touched files remains functional and referenced
- The `FileContent` property was made nullable but not removed — it's needed for legacy attachment coexistence
- The existing `Code` field was kept — it serves a different purpose than `CustomerCode`
- No orphaned strings, imports, or dead code paths were found in touched files

### What was intentionally left
| Item | Reason |
|------|--------|
| `FileContent` property on `CustomerAttachment` | Required for legacy blob-stored attachments until CAFS-3 migration |
| `Code` property on `Customer` (CUST-XXXXXX format) | Serves as business code, separate purpose from `CustomerCode` |
| Existing attachment bytea data in database | Will be migrated in CAFS-3, not this phase |

### Build/test verification after cleanup
- Build: 0 errors, 0 warnings
- Tests: 274 passed, 0 failed
- No runtime errors observed

### Explicit statement
No safe cleanup was identified in touched files. All existing code paths are still live and necessary.
