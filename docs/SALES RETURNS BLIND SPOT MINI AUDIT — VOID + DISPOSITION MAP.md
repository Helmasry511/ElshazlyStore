# SALES RETURNS BLIND SPOT MINI AUDIT — VOID + DISPOSITION MAP

**Date:** 2026-03-28  
**Status:** COMPLETE  
**Scope:** Read-only source audit only. No Sales Returns UI implementation in this phase.

---

## 1. Direct Answers

### 1) What does `VoidAsync` do today?

`VoidAsync` only voids Draft sales returns.

- if return is missing: returns `SALES_RETURN_NOT_FOUND`
- if already voided: returns `SALES_RETURN_ALREADY_VOIDED`
- if status is Posted: returns `SALES_RETURN_VOID_NOT_ALLOWED_AFTER_POST`
- if status is Draft: sets status to `Voided`, writes `VoidedAtUtc` and `VoidedByUserId`, then saves

It is a soft-cancel status change only. It does not reverse stock, and it does not create any accounting reversal entry, because posted returns are not voidable in the current implementation.

### 2) Exact `DispositionType` names in source

The enum names are exactly:

- `Scrap`
- `Rework`
- `ReturnToVendor`
- `ReturnToStock`
- `Quarantine`
- `WriteOff`

### 3) What does `PostAsync` do with dispositions today?

Current RET 1 policy only allows:

- `ReturnToStock`
- `Quarantine`

At posting time, each line is converted into a positive stock delta into the return header warehouse using `MovementType.SaleReturnReceipt`.

That means:
- `ReturnToStock` increases stock in the selected warehouse
- `Quarantine` also increases stock in the same selected warehouse

The distinction is preserved in the movement line note text, but not by routing into different warehouse types or a separate disposition movement model in this service.

The following dispositions are rejected in RET 1:

- `Scrap`
- `Rework`
- `ReturnToVendor`
- `WriteOff`

They fail through `ValidateDisposition()` with `SALES_RETURN_DISPOSITION_NOT_ALLOWED`.

### 4) Is `RequiresManagerApproval` enforced in Sales Returns now?

Not in the current Sales Returns create/post/void flow.

`ReasonCode.RequiresManagerApproval` exists on the entity, but the current `SalesReturnService` only validates that referenced reason codes exist and are active. No approval actor, approval flag, or approval gate is enforced during create or post.

### 5) Is no-invoice return allowed now?

Yes.

`OriginalSalesInvoiceId` is nullable in the create/update contract and service model.

When it is provided, the service validates:
- invoice exists
- invoice is posted
- requested return quantities do not exceed sold minus already-returned quantities

When it is not provided, those original-invoice validations are skipped. So a return without an original invoice is currently backend-allowed.

### 6) What should UI SALES 3 assume from this?

UI SALES 3 should assume:

- posted Sales Returns cannot be voided in the current backend
- void is only for Draft returns
- RET 1 posting supports only `ReturnToStock` and `Quarantine`
- `RequiresManagerApproval` is not yet enforced by backend Sales Returns flows
- original sales invoice linkage is optional
- any richer disposition routing, stock separation, or posted-return reversal behavior would require explicit backend work before UI support is safe

---

## 2. Source-Grounded Map

### 2A. Create / Update

Current Sales Returns service allows:
- draft create
- draft update
- optional customer
- optional original sales invoice
- per-line reason code
- per-line disposition

But create/update still reject disallowed RET 1 dispositions through `ValidateDisposition()`.

### 2B. Post

`PostAsync` does the following:

1. atomically claims a Draft return by setting it to Posted
2. rejects missing / voided / mid-post / already-posted states correctly
3. revalidates active reason codes
4. revalidates original-invoice quantities if an original invoice is linked
5. revalidates disposition policy
6. posts stock movement as `SaleReturnReceipt`
7. stores `StockMovementId`
8. creates a customer `CreditNote` ledger entry when a customer exists

### 2C. Void

`VoidAsync` does not:
- reverse `StockMovementId`
- create reversing stock movement
- create reversing accounting entry
- support void-after-post

So current void behavior is administrative cancellation of Draft only, not transactional rollback of Posted.

---

## 3. Blind Spot Summary

The blind spot is not ambiguity in code anymore; it is product capability gap.

The backend is currently explicit:
- Draft can be voided.
- Posted cannot be voided.
- Only `ReturnToStock` and `Quarantine` can be posted.
- No manager-approval enforcement exists in Sales Returns despite the reason-code field existing.
- No-invoice returns are allowed.

The future UI must not imply broader behavior than that.

---

## 4. UI SALES 3 Guardrails

When Sales Returns UI starts, it should:

- expose void only for Draft returns
- never promise posted-return void/reversal
- restrict disposition options to `ReturnToStock` and `Quarantine` unless backend policy changes first
- treat manager approval as informational only unless backend enforcement is added
- support optional original invoice lookup rather than making it mandatory
- clearly label that `Quarantine` still posts stock into the selected warehouse under current backend behavior

If product wants posted-return void, multi-destination disposition routing, or enforced manager approval, those should be raised as backend contract work first.