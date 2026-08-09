# Mimari Karar Kayıtları / Architecture Decision Records

## Türkçe

Bu klasör geri döndürülmesi maliyetli, şaşırtıcı veya önemli trade-off içeren mimari kararların ADR formatında saklanacağı yerdir.

Güncel durumda mimari kararların kronolojik ve açıklayıcı özeti `../17-ai-architecture-decisions.md` içindedir. Bu belge ADR yerine geçmez; tekil ve kalıcı kararlar gerektiğinde bu klasörde ayrı dosya olarak oluşturulmalıdır.

Önerilen ADR formatı:

```text
# ADR-NNN: Başlık
Durum: Proposed / Accepted / Superseded / Rejected
Tarih: YYYY-MM-DD

## Bağlam
Kararı neden vermek gerekiyor?

## Karar
Tam olarak neye karar verildi?

## Alternatifler
Hangi seçenekler değerlendirildi?

## Sonuçlar
Neyi kolaylaştırır, neyi zorlaştırır?
```

İlk aday ADR konuları:
- MSSQL'nin financial source of truth olması;
- modular monolith'in başlangıç mimarisi olması;
- YARP Gateway trust-boundary modeli;
- durable MSSQL idempotency;
- external HTTP'nin financial SQL transaction dışında tutulması.

---

## English

This directory is the location for ADRs that capture architecture decisions that are expensive to reverse, surprising without context, or involve meaningful trade-offs.

The current chronological architecture narrative is maintained in `../17-ai-architecture-decisions.md`. That document does not replace ADRs; individual durable decisions should be recorded here as separate files when appropriate.

Recommended ADR format:

```text
# ADR-NNN: Title
Status: Proposed / Accepted / Superseded / Rejected
Date: YYYY-MM-DD

## Context
Why is a decision required?

## Decision
What exactly was decided?

## Alternatives
Which options were considered?

## Consequences
What becomes easier and what becomes harder?
```

Initial ADR candidates:
- MSSQL as the financial source of truth;
- modular monolith as the starting architecture;
- YARP Gateway trust-boundary model;
- durable MSSQL idempotency;
- keeping external HTTP outside financial SQL transactions.
