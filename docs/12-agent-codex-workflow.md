# Agent ve Codex Geliştirme Akışı / Agent and Codex Development Workflow

## Türkçe

### Amaç
Bu belge, FinWallet geliştirmesinde Codex veya başka coding agent'ların nasıl scope alacağı, branch açacağı, doğrulama yapacağı, PR oluşturacağı ve review edileceğini tanımlar. Repository dosyaları, `AGENTS.md`, dokümanlar, ADR'ler ve GitHub issue/PR'ları source of truth'tür; chat geçmişi repository dokümantasyonunun yerine geçmez.

### Çalışma modeli
Her agent:
1. bounded bir task/issue alır;
2. repository kurallarını okur;
3. current `main`'den `agent/<task>` branch'i açar;
4. gerekli kod/test/doc değişikliğini yapar;
5. Release build/test ile doğrular;
6. review edilebilir PR açar.

Önerilen roller:
- Solution Architect;
- Security/Auth;
- Financial Domain;
- Persistence/Concurrency;
- Integration;
- QA/Chaos;
- Code Review;
- Documentation.

### Source-of-truth sırası
Çakışma olduğunda:
1. current task acceptance criteria;
2. `AGENTS.md`;
3. master specification;
4. accepted ADR;
5. architecture/security/database/API docs;
6. mevcut implementation convention;
7. agent varsayımı.

Agent üst seviye kuralı sessizce override edemez.

### Branch/commit
Branch: `agent/<bounded-task-name>`.

Commit küçük ve cohesive olmalıdır. `implement everything`, `fix stuff` gibi belirsiz commitlerden kaçınılır. Financial behavior, package change, refactor ve docs mümkünse ayrı anlaşılır commitlerdir.

### Coding agent sorumlulukları
- financial invariant'ları koru;
- class/interface/method/property için gerekli TR/EN XML docs'u yaz;
- paid/freemium package ekleme;
- yeni package inventory'yi güncelle;
- changed behavior için test ekle/güncelle;
- API/architecture/database/security docs'u güncelle;
- external HTTP'yi financial SQL transaction içine alma;
- idempotency/concurrency garantisini bozma;
- Redis'i financial source of truth yapma;
- tüm Markdown dokümanlarında TR+EN senkronunu koru.

### Review agent kontrolü
- architecture dependency violations;
- ledger/financial correctness;
- double-spend/lost update;
- idempotency gaps;
- auth/OTP/token weakness;
- sensitive logging;
- unsafe retry/timeout;
- missing cancellation;
- over-engineering;
- undocumented package;
- missing TR/EN code/doc documentation.

### QA/Chaos senaryoları
- simultaneous overspend;
- repeated idempotency key;
- same key + altered payload;
- Redis outage;
- provider timeout/500/slow;
- fraud unavailable;
- repeated refund/reversal;
- ledger mismatch;
- refresh token replay;
- OTP brute force;
- direct Gateway bypass;
- rate-limit/resource-exhaustion.

### PR akışı
1. Branch'te bounded iş tamamlanır.
2. Diff self-review edilir.
3. Docs/test güncellenir.
4. `dotnet restore`, Release `dotnet build --warnaserror`, `dotnet test` çalıştırılır.
5. Draft PR açılır.
6. Review bulguları küçük commitlerle kapatılır.
7. CI green olur.
8. Financial/security sensitive PR final review alır.
9. Acceptance criteria karşılanınca merge edilir.

### Parallel agent kuralı
Domain boundary, DB/ledger rule, API contract, auth ve error/idempotency convention stabil olmadan aynı feature alanında paralel agent'lar kendi sözleşmelerini invent etmemelidir. File conflict düşük ve contract stabil ise paralel çalışma uygundur.

### DoD
Agent-generated code ancak acceptance criteria, build, relevant test, financial/security invariant, TR/EN XML docs, package inventory ve affected TR/EN Markdown docs tamamlandığında done sayılır.

---

## English

### Purpose
This document defines how Codex or other coding agents scope work, create branches, validate changes, open pull requests and participate in review for FinWallet. Repository files, `AGENTS.md`, documentation, ADRs and GitHub issues/PRs are the source of truth; chat history does not replace repository documentation.

### Operating model
Each agent:
1. receives a bounded task/issue;
2. reads repository rules;
3. creates an `agent/<task>` branch from current `main`;
4. makes required code/test/document changes;
5. validates with Release build/tests;
6. opens a reviewable pull request.

Recommended roles:
- Solution Architect;
- Security/Auth;
- Financial Domain;
- Persistence/Concurrency;
- Integration;
- QA/Chaos;
- Code Review;
- Documentation.

### Source-of-truth order
When instructions conflict:
1. current task acceptance criteria;
2. `AGENTS.md`;
3. master specification;
4. accepted ADR;
5. architecture/security/database/API docs;
6. existing implementation conventions;
7. agent assumptions.

An agent must not silently override a higher-level rule.

### Branch/commit
Branch format: `agent/<bounded-task-name>`.

Commits should be small and cohesive. Avoid vague commits such as `implement everything` or `fix stuff`. Financial behavior, package changes, refactors and documentation should remain independently understandable when practical.

### Coding-agent responsibilities
- preserve financial invariants;
- write required TR/EN XML docs for classes/interfaces/methods/properties;
- do not add paid/freemium dependencies;
- update package inventory for new packages;
- add/update tests for changed behavior;
- update API/architecture/database/security docs;
- never place external HTTP inside a financial SQL transaction;
- preserve idempotency/concurrency guarantees;
- never make Redis the financial source of truth;
- keep TR+EN Markdown documentation synchronized.

### Review-agent checks
- architecture dependency violations;
- ledger/financial correctness;
- double-spend/lost updates;
- idempotency gaps;
- auth/OTP/token weaknesses;
- sensitive logging;
- unsafe retry/timeout behavior;
- missing cancellation;
- over-engineering;
- undocumented packages;
- missing TR/EN code/documentation.

### QA/Chaos scenarios
- simultaneous overspend;
- repeated idempotency key;
- same key + altered payload;
- Redis outage;
- provider timeout/500/slow response;
- fraud unavailable;
- repeated refund/reversal;
- ledger mismatch;
- refresh-token replay;
- OTP brute force;
- direct Gateway bypass;
- rate-limit/resource exhaustion.

### Pull-request flow
1. Complete bounded work on the branch.
2. Self-review the diff.
3. Update docs/tests.
4. Run `dotnet restore`, Release `dotnet build --warnaserror`, and `dotnet test`.
5. Open a draft PR.
6. Resolve review findings with small commits.
7. CI becomes green.
8. Financial/security-sensitive PRs receive final review.
9. Merge only after acceptance criteria are satisfied.

### Parallel-agent rule
Until domain boundaries, DB/ledger rules, API contracts, auth and error/idempotency conventions are stable, parallel agents should not invent incompatible contracts inside the same feature area. Parallel work is appropriate when file overlap is low and contracts are stable.

### DoD
Agent-generated code is done only when acceptance criteria, build, relevant tests, financial/security invariants, TR/EN XML docs, package inventory and affected TR/EN Markdown docs are complete.
