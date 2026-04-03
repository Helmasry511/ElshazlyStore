# UI-BACKEND CUSTOMER CREDIT PROFILE 3A — SERVER-PERSISTED FIELDS + ATTACHMENTS — CLOSEOUT

## Phase
**CP-3A — Customer Credit Profile Foundation + Server-Persisted Fields + Attachments**

## Status
**COMPLETE** — Build: 0 errors, 0 warnings — Tests: 274 passed, 0 failed

---

## 1) Summary of What Was Implemented

### Backend
- Extended the `Customer` entity with 6 new server-persisted credit profile fields
- Created `CustomerAttachment` entity for server-side file storage (PostgreSQL `bytea`)
- Created EF Core configuration for `CustomerAttachment` with FK to Customer (cascade delete)
- Generated migration `0019_customer_credit_profile_attachments` (adds columns + creates table)
- Extended all customer CRUD endpoints to handle the new fields
- Added 4 new attachment endpoints: list, upload, download, delete

### Desktop
- Extended `CustomerDto`, `CreateCustomerRequest`, `UpdateCustomerRequest` with new fields
- Created `CustomerAttachmentDto` for client-side attachment data
- Added `PostMultipartAsync` and `GetBytesAsync` to `ApiClient` for file upload/download
- Extended `CustomersViewModel` with form fields for all 6 new credit profile fields
- Added attachment commands: upload (with category), download, delete
- Upgraded create/edit modal with 3 new sections: Digital Payment, Commercial Info
- Upgraded details modal to show all new fields conditionally (only when populated)
- Added full attachment section in details: categorized upload buttons, list, download, delete
- Added 20 new Arabic localization strings

---

## 2) Exact Backend Fields Added/Reused

### New fields on `customers` table (all nullable, all new — no prior equivalent existed):

| Field | DB Column | Type | MaxLength | Purpose |
|-------|-----------|------|-----------|---------|
| `WhatsApp` | `WhatsApp` | `varchar(30)` | 30 | واتساب |
| `WalletNumber` | `WalletNumber` | `varchar(50)` | 50 | رقم المحفظة الإلكترونية |
| `InstaPayId` | `InstaPayId` | `varchar(50)` | 50 | معرف إنستاباي |
| `CommercialName` | `CommercialName` | `varchar(300)` | 300 | الاسم التجاري |
| `CommercialAddress` | `CommercialAddress` | `varchar(500)` | 500 | العنوان التجاري |
| `NationalIdNumber` | `NationalIdNumber` | `varchar(20)` | 20 | رقم بطاقة الرقم القومي |

**No existing fields were reused under different names.** All 6 fields are genuinely new additions to the Customer entity that did not previously exist in any form.

### New `customer_attachments` table:

| Column | Type | Constraint |
|--------|------|-----------|
| `Id` | `uuid` | PK |
| `CustomerId` | `uuid` | FK → customers (CASCADE) |
| `FileName` | `varchar(255)` | NOT NULL |
| `ContentType` | `varchar(100)` | NOT NULL |
| `FileSize` | `bigint` | NOT NULL |
| `Category` | `varchar(30)` | NOT NULL, default "other" |
| `FileContent` | `bytea` | NOT NULL |
| `CreatedAtUtc` | `timestamp with time zone` | NOT NULL |

Index on `CustomerId` for fast lookups.

---

## 3) Exact Files Created/Modified

### Created (new files):
| File | Purpose |
|------|---------|
| `src/ElshazlyStore.Domain/Entities/CustomerAttachment.cs` | Domain entity for customer attachments |
| `src/ElshazlyStore.Infrastructure/Persistence/Configurations/CustomerAttachmentConfiguration.cs` | EF Core config |
| `src/ElshazlyStore.Infrastructure/Persistence/Migrations/20260401174337_0019_customer_credit_profile_attachments.cs` | Migration Up/Down |
| `src/ElshazlyStore.Infrastructure/Persistence/Migrations/20260401174337_0019_customer_credit_profile_attachments.Designer.cs` | Migration designer |

### Modified (existing files):
| File | What changed |
|------|-------------|
| `src/ElshazlyStore.Domain/Entities/Customer.cs` | Added 6 new properties |
| `src/ElshazlyStore.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs` | Added 6 new field mappings |
| `src/ElshazlyStore.Infrastructure/Persistence/AppDbContext.cs` | Added `DbSet<CustomerAttachment>` |
| `src/ElshazlyStore.Api/Endpoints/CustomerEndpoints.cs` | Updated DTOs, added attachment endpoints |
| `src/ElshazlyStore.Desktop/Models/Dtos/CustomerDto.cs` | Added new fields + CustomerAttachmentDto |
| `src/ElshazlyStore.Desktop/Services/Api/ApiClient.cs` | Added PostMultipartAsync + GetBytesAsync |
| `src/ElshazlyStore.Desktop/ViewModels/CustomersViewModel.cs` | Added form fields + attachment commands |
| `src/ElshazlyStore.Desktop/Views/Pages/CustomersPage.xaml` | Updated create/edit/details modals |
| `src/ElshazlyStore.Desktop/Localization/Strings.cs` | Added 20 new string accessors, removed 2 orphans |
| `src/ElshazlyStore.Desktop/Localization/Strings.resx` | Added 20 new string resources, removed 2 orphans |
| `src/ElshazlyStore.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` | Auto-updated by EF |

---

## 4) Exact Endpoints/Contracts Touched

### Modified endpoints (new fields flowing through):
| Method | Route | Change |
|--------|-------|--------|
| `GET` | `/api/v1/customers` | Returns new fields in `CustomerDto` |
| `GET` | `/api/v1/customers/{id}` | Returns new fields in `CustomerDto` |
| `POST` | `/api/v1/customers` | Accepts new fields in `CreateCustomerRequest` |
| `PUT` | `/api/v1/customers/{id}` | Accepts new fields in `UpdateCustomerRequest` |

### New endpoints (CP-3A attachments):
| Method | Route | Permission | Purpose |
|--------|-------|-----------|---------|
| `GET` | `/api/v1/customers/{id}/attachments` | `CUSTOMERS_READ` | List all attachments for a customer |
| `POST` | `/api/v1/customers/{id}/attachments?category=` | `CUSTOMERS_WRITE` | Upload attachment (multipart/form-data) |
| `GET` | `/api/v1/customers/{id}/attachments/{attachmentId}` | `CUSTOMERS_READ` | Download attachment file |
| `DELETE` | `/api/v1/customers/{id}/attachments/{attachmentId}` | `CUSTOMERS_WRITE` | Delete attachment |

---

## 5) Attachment Persistence Approach

**Storage:** PostgreSQL `bytea` column in `customer_attachments` table, matching the existing pattern used by the import system (`ImportJob.FileContent`).

**Max size:** 5 MB per file, server-enforced.

**Allowed file types:** `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.pdf`, `.doc`, `.docx` — server-enforced.

**Categories:** `national_id`, `contract`, `other` — validated server-side.

**Relationship:** Each attachment belongs to exactly one customer via FK. Cascade delete ensures attachments are removed when customer is deleted.

---

## 6) Supported Attachment Operations

| Operation | Status | Notes |
|-----------|--------|-------|
| **Upload** | ✅ Fully supported | Multipart upload with category, size validation, extension whitelist |
| **List** | ✅ Fully supported | Returns all attachments for a customer, ordered by date |
| **Download** | ✅ Fully supported | Returns file bytes with correct content-type and filename |
| **Delete** | ✅ Fully supported | Removes attachment from DB after user confirmation |
| **Replace** | ⏳ Deferred | Not implemented — user can delete old + upload new as workaround |

**Replace** was not implemented as a dedicated atomic operation. The user can delete an attachment and upload a new one. An atomic replace endpoint can be added in a future phase if needed.

---

## 7) Conflicts Found Between Spec and Backend Truth

**None.** All 6 requested fields were genuinely new — no prior backend field existed under a different name. The spec's field requirements were fully compatible with the existing database schema (simple nullable column additions). No fields were renamed, reused, or faked.

---

## 8) What Was Intentionally NOT Implemented

| Item | Status | Reason |
|------|--------|--------|
| Payment batch / A4 statement printing | ❌ NOT IMPLEMENTED | Scope of CP-3B |
| Prepayment / "له / عليه" | ❌ NOT IMPLEMENTED | Deferred to CP-Future |
| Supplier-side mirror work | ❌ NOT IMPLEMENTED | Out of scope |
| Print Config | ❌ NOT IMPLEMENTED | Out of scope |
| Whole-app redesign | ❌ NOT IMPLEMENTED | Out of scope |
| Atomic attachment replace operation | ⏳ DEFERRED | Can be added later; delete+upload workaround exists |
| Attachment preview/thumbnails in list | ⏳ DEFERRED | Can be added in future enhancement |

**Explicit confirmation:**
- ❌ Payment batch / A4 statement printing was NOT implemented
- ❌ Prepayment / "له / عليه" was NOT implemented
- ❌ CP-3B was NOT started

---

## 9) Build / Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 274, Skipped: 0, Total: 274
```

---

## 10) Cleanup Audit

### Touched files reviewed: 14 files (4 new, 10 modified)

### Unused code/resources detected:
| String Key | Location | Status |
|-----------|----------|--------|
| `Customers_PaymentsSectionPlaceholder` | Strings.cs + Strings.resx | Orphaned — was a CP-1 placeholder replaced by live payments in CP-2. Never referenced anywhere. |
| `Customers_CreditInfo` | Strings.cs + Strings.resx | Orphaned — added in CP-3A but never consumed (XAML uses `Customers_DigitalPayment` and `Customers_CommercialInfo` instead). |

### What was removed:
- `Customers_PaymentsSectionPlaceholder` from both `Strings.cs` and `Strings.resx`
- `Customers_CreditInfo` from both `Strings.cs` and `Strings.resx`

### Why each removal was safe:
- `Customers_PaymentsSectionPlaceholder`: grep confirms zero references in any XAML, ViewModel, or code file. It was a placeholder label from CP-1 that CP-2 replaced with actual payments navigation. Removing it has no runtime effect.
- `Customers_CreditInfo`: grep confirms zero references anywhere. It was added as a potential section title but the XAML uses `Customers_DigitalPayment` and `Customers_CommercialInfo` as the actual section headers. Removing it has no runtime effect.

### What was intentionally left:
- All other localization strings: verified as actively referenced
- All existing code patterns in touched files: verified as still functional
- The `UpdatedAtUtc` property on Customer entity: exists but is not exposed in DTOs (same as before CP-3A — not a CP-3A decision)

### Build/test verification after cleanup:
- Build: 0 errors, 0 warnings
- Tests: 274 passed, 0 failed

### Explicit statement:
No safe cleanup was blocked. All identified dead code was successfully removed.

---

## 11) Human Test Script for CP-3A

### Prerequisites
- Server running with migration 0019 applied
- Desktop app connected to server
- User with `CUSTOMERS_READ` and `CUSTOMERS_WRITE` permissions

### Test 1: Create customer with new credit profile fields
1. Open **العملاء** page
2. Click **إضافة** (Create)
3. Fill in all fields:
   - الاسم: "عميل تجريبي CP-3A"
   - الهاتف: "01012345678"
   - واتساب: "01098765432"
   - رقم المحفظة: "01155555555"
   - معرف إنستاباي: "instapay@test"
   - الاسم التجاري: "شركة الاختبار"
   - العنوان التجاري: "١٢ شارع التحرير، القاهرة"
   - رقم بطاقة الرقم القومي: "29901011234567"
4. Click **حفظ**
5. **Expected:** Success notification, customer appears in list

### Test 2: Verify fields persisted
1. Find the customer just created in the list
2. Click **تفاصيل**
3. **Expected:** All entered fields appear correctly in the details modal:
   - Contact section: Phone, Phone2, WhatsApp
   - Digital payment section: Wallet number, InstaPay ID (section only visible because values exist)
   - Commercial section: Commercial name, commercial address, national ID number (section only visible because values exist)

### Test 3: Edit customer credit profile
1. From the details modal, click **تعديل**
2. Change the commercial name to "شركة الاختبار المعدلة"
3. Save
4. Open details again
5. **Expected:** Updated commercial name appears

### Test 4: Upload attachment — National ID
1. Open customer details
2. Click **رفع صورة بطاقة**
3. Select a JPG/PNG file (under 5 MB)
4. **Expected:** Success notification, attachment appears in list with category "national_id" and filename

### Test 5: Upload attachment — Contract
1. In same customer details
2. Click **رفع ملف تعاقد**
3. Select a PDF file
4. **Expected:** Success notification, second attachment appears

### Test 6: Download attachment
1. Click the ⬇ button on an attachment
2. Choose save location
3. **Expected:** File saved, success notification

### Test 7: Delete attachment
1. Click the ✕ button on an attachment
2. Confirm deletion
3. **Expected:** Attachment removed from list, success notification

### Test 8: Upload rejection (too large)
1. Try uploading a file larger than 5 MB
2. **Expected:** Error message "حجم الملف يتجاوز الحد المسموح (5 ميجابايت)"

### Test 9: Empty fields — details display
1. Create a new customer with ONLY name (no credit profile fields)
2. Open details
3. **Expected:** Digital payment and commercial info sections are NOT shown (collapsed when empty). Only contact info section shows.

### Test 10: Existing functionality preserved
1. Verify the customer list still looks professional (same as before)
2. Verify search works across code/name/phone
3. Verify deactivate/reactivate works
4. Verify "عرض الدفعات" navigation still works
5. Toggle dark/light mode
6. **Expected:** All existing behaviors work. Dark and light modes are correct.

### Test 11: Data persistence after restart
1. Close and reopen the Desktop app
2. Navigate to the customer created in Test 1
3. Open details
4. **Expected:** All credit profile fields and attachments still present

---

## End of CP-3A Closeout

**Next phase:** CP-3B (Customer Payment Statement / Batch Print / A4), after human test approval.
**Not started.** Waiting for agent report + human test result.
