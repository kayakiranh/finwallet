# Code Documentation Standard

## Purpose

FinWallet requires bilingual XML documentation for the codebase so that business intent, financial constraints and technical behavior remain understandable in both Turkish and English.

## Mandatory scope

Every manually written C# declaration must be documented when applicable:

- class
- record
- struct
- enum
- interface
- constructor
- method
- property
- generic type parameter
- method parameter
- return value
- meaningful thrown exception

The rule applies to public, internal and private production code where XML documentation is syntactically supported and useful. Generated code is excluded.

## Summary format

Every summary contains Turkish first and English second.

```csharp
/// <summary>
/// TR: Cüzdanın kullanılabilir bakiyesini temsil eder ve finansal işlemlerde harcanabilir tutarı gösterir.
/// EN: Represents the wallet's available balance and indicates the amount that can be spent in financial operations.
/// </summary>
```

## Method documentation

Methods document intent rather than restating the method name.

```csharp
/// <summary>
/// TR: Cüzdandan belirtilen tutarı düşürür ve yetersiz bakiye durumunda işlemi reddeder.
/// EN: Debits the specified amount from the wallet and rejects the operation when the available balance is insufficient.
/// </summary>
/// <param name="amount">
/// TR: Düşülecek para tutarı ve para birimi.
/// EN: Money amount and currency to debit.
/// </param>
/// <returns>
/// TR: Güncellenmiş cüzdan durumunu temsil eder.
/// EN: Represents the updated wallet state.
/// </returns>
```

## Property documentation

Property documentation must explain business meaning, not only the data type.

Bad:

```text
TR: Para birimi.
EN: Currency.
```

Preferred:

```text
TR: Cüzdanın işlem kabul ettiği ISO para birimini belirtir; farklı para birimindeki tutarlar bu cüzdana doğrudan uygulanamaz.
EN: Identifies the ISO currency accepted by the wallet; amounts in another currency cannot be applied directly to this wallet.
```

## Financial code expectations

Financial classes and methods must document important invariants where relevant, including:

- currency consistency
- double-entry balance requirement
- append-only ledger behavior
- idempotency behavior
- concurrency assumptions
- allowed state transitions
- reversal/compensation behavior
- source-of-truth ownership

## Compiler enforcement

`Directory.Build.props` enables XML documentation generation and treats `CS1591` as an error. This guarantees documentation for externally visible members. Agent/review rules extend the requirement to non-public manually written members.

## Review checklist

A change is not complete when:

- a class/interface/method/property lacks TR/EN documentation;
- documentation merely repeats the member name;
- financial side effects are undocumented;
- parameters or return values are ambiguous;
- comments contradict implementation;
- a refactor changes behavior but leaves obsolete documentation.
