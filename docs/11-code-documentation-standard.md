# Kod Dokümantasyon Standardı / Code Documentation Standard

## Türkçe

### Amaç
FinWallet codebase'inde business intent, financial invariant ve teknik davranışın hem Türkçe hem İngilizce anlaşılabilmesi için C# XML documentation çift dilli tutulur.

### Zorunlu kapsam
Uygun olan her manually-written declaration dokümante edilir:
- class/record/struct/enum/interface;
- constructor;
- method;
- property;
- generic type parameter;
- method parameter;
- return value;
- anlamlı thrown exception.

Generated code hariçtir.

### Format
Her XML summary'de Türkçe önce, İngilizce sonra:
```csharp
/// <summary>
/// TR: Cüzdanın kullanılabilir bakiyesini temsil eder.
/// EN: Represents the wallet's available balance.
/// </summary>
```

Method/param/returns açıklamaları isim tekrarı yapmak yerine intent ve constraint anlatmalıdır.

### Financial code beklentisi
İlgili yerde dokümantasyon şu invariant'ları açıklamalıdır:
- currency consistency;
- Debit = Credit;
- append-only/reversal davranışı;
- idempotency;
- concurrency/locking assumptions;
- allowed state transitions;
- source-of-truth ownership;
- external HTTP / transaction boundary.

### Compiler enforcement
`Directory.Build.props` XML documentation üretimini açar ve `CS1591` warning'ini error yapar. Bu externally visible member'lar için compiler-level enforcement sağlar. Review standardı internal/private production member'larda da gerekli açıklamayı ister.

### Markdown standardı
`docs/**/*.md` ve root `README.md` Türkçe + İngilizce olmalıdır. Tercih edilen yapı:
```text
## Türkçe
...
---
## English
...
```
Code snippets, endpoint path'leri ve teknik identifier'lar çevrilmeden kullanılabilir; açıklama metni iki dilde verilmelidir.

### Review checklist
Değişiklik tamamlanmamıştır eğer:
- gerekli declaration'da TR/EN XML doc yoksa;
- açıklama sadece member adını tekrar ediyorsa;
- financial side effect/invariant gizliyse;
- code davranışı değiştiği halde Markdown eski davranışı anlatıyorsa;
- yalnız bir dil güncellenmişse.

---

## English

### Purpose
FinWallet keeps C# XML documentation bilingual so business intent, financial invariants and technical behavior remain understandable in both Turkish and English.

### Mandatory scope
Document every applicable manually written declaration:
- class/record/struct/enum/interface;
- constructor;
- method;
- property;
- generic type parameter;
- method parameter;
- return value;
- meaningful thrown exception.

Generated code is excluded.

### Format
Every XML summary contains Turkish first and English second:
```csharp
/// <summary>
/// TR: Cüzdanın kullanılabilir bakiyesini temsil eder.
/// EN: Represents the wallet's available balance.
/// </summary>
```

Method/param/returns documentation should explain intent and constraints instead of merely repeating member names.

### Financial-code expectations
Where relevant, documentation should explain:
- currency consistency;
- Debit = Credit;
- append-only/reversal behavior;
- idempotency;
- concurrency/locking assumptions;
- allowed state transitions;
- source-of-truth ownership;
- external HTTP / transaction boundaries.

### Compiler enforcement
`Directory.Build.props` enables XML documentation generation and treats `CS1591` as an error. This provides compiler enforcement for externally visible members. Review standards extend useful documentation expectations to internal/private production members.

### Markdown standard
`docs/**/*.md` and root `README.md` must be Turkish + English. Preferred structure:
```text
## Türkçe
...
---
## English
...
```
Code snippets, endpoint paths and technical identifiers may remain unchanged; explanatory prose should exist in both languages.

### Review checklist
A change is not complete when:
- a required declaration lacks TR/EN XML docs;
- documentation only repeats the member name;
- financial side effects/invariants are hidden;
- code behavior changed while Markdown still describes the old behavior;
- only one language was updated.
