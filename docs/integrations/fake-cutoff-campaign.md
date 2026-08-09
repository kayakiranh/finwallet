# FakeCutoff and FakeCampaign Integration Guide

## Purpose

`FakeCutoff.Api` and `FakeCampaign.Api` simulate independent third-party providers. They intentionally remain separate from the FinWallet core so integration contracts, timeouts, failure behavior and anti-corruption mappings can be exercised as if real providers existed.

## FakeCutoff.Api

### Responsibility

FakeCutoff owns:

- business-day evaluation;
- local timezone interpretation;
- weekend handling;
- deterministic simulated fixed-holiday data;
- cutoff time selection by country/currency/transaction type;
- processing date;
- settlement business date.

FinWallet must not duplicate these calculations internally.

### Endpoint

`POST /api/v1/cutoffs/evaluate`

Example request:

```json
{
  "countryCode": "TR",
  "currency": "TRY",
  "transactionType": "Withdrawal",
  "requestedAt": "2026-08-10T16:45:00+03:00"
}
```

Example response:

```json
{
  "referenceId": "19b86128-c16f-4912-bb06-1951340c50d4",
  "canProcessNow": false,
  "processingDate": "2026-08-11",
  "settlementDate": "2026-08-12",
  "cutoffTime": "16:30:00",
  "timeZoneId": "Europe/Istanbul",
  "reason": "AFTER_CUTOFF"
}
```

### Initial deterministic rule seeds

| Country | Currency | Transaction | Timezone | Cutoff | Settlement |
|---|---|---|---|---|---|
| TR | TRY | Withdrawal | Europe/Istanbul | 16:30 | +1 business day |
| TR | USD | Withdrawal | Europe/Istanbul | 15:30 | +2 business days |
| TR | EUR | Withdrawal | Europe/Istanbul | 15:30 | +2 business days |
| TR | TRY | BankTransfer | Europe/Istanbul | 16:00 | +1 business day |
| AZ | USD | Withdrawal | Asia/Baku | 15:00 | +2 business days |
| AZ | EUR | Withdrawal | Asia/Baku | 15:00 | +2 business days |

The holiday set is deliberately simulator data. It contains selected fixed-date holidays to exercise cutoff logic and is not a legally authoritative or complete public-holiday feed. A production integration would source calendar decisions from the contracted provider rather than from FinWallet.

### Failure simulation

`X-Fake-Mode` supports:

- `fail`: HTTP 503;
- `delay`: approximately two seconds;
- `timeout`: approximately thirty seconds/cancellation path.

## FakeCampaign.Api

### Responsibility

FakeCampaign owns:

- merchant campaign eligibility;
- currency and campaign-date eligibility;
- minimum purchase amount;
- percentage/fixed discount calculation;
- maximum-discount cap;
- sponsor type (`Platform` or `Merchant`).

FakeCampaign does **not** own accounting. FinWallet must convert the returned discount/sponsor information into balanced ledger entries.

### Endpoint

`POST /api/v1/campaigns/evaluate`

Example request:

```json
{
  "customerReference": "7af85bd3-c8cc-478f-bdad-90f6a8123a6b",
  "merchantId": "MRC-COFFEE-001",
  "amount": 1000.00,
  "currency": "TRY",
  "requestedAt": "2026-08-09T14:00:00Z"
}
```

Example response:

```json
{
  "providerReference": "2d91cebe-86ae-458e-9d5f-23ef1846e41c",
  "eligible": true,
  "campaignId": "CMP-COFFEE-10",
  "originalAmount": 1000.00,
  "discountAmount": 100.00,
  "finalAmount": 900.00,
  "currency": "TRY",
  "sponsorType": "Platform",
  "reason": "CAMPAIGN_APPLIED"
}
```

### Initial deterministic campaign seeds

- `MRC-COFFEE-001`: TRY, minimum 200, 10%, maximum discount 100, Platform funded.
- `MRC-ELECTRONICS-001`: TRY, minimum 1000, 5%, maximum discount 500, Merchant funded.
- `MRC-TRAVEL-001`: EUR, minimum 100, fixed 20 EUR, Merchant funded.

Seed date range is 2026-01-01 through 2030-12-31 and exists only to make side-project behavior deterministic.

### Accounting implication

For a platform-funded campaign where a 1,000 TRY purchase receives 100 TRY discount:

- Customer economic charge: 900 TRY.
- Campaign/platform expense: 100 TRY.
- Merchant economic receivable: 1,000 TRY.

The above relationship is accounted for only by the future FinWallet ledger module. FakeCampaign merely returns the calculation and sponsor.

### Failure simulation

`X-Fake-Mode` supports the same `fail`, `delay` and `timeout` values as FakeCutoff.

## Reliability policy for future FinWallet adapters

- Cutoff failure on a bank workflow must not silently invent a processing date.
- Campaign failure must not silently charge an undiscounted amount when the customer has confirmed a discounted purchase.
- Provider references and FinWallet transaction/correlation identifiers remain separate.
- Provider HTTP DTOs must remain behind Infrastructure adapters/anti-corruption mappings.
