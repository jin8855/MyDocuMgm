# MyDocuMgm Phase 2B Provider Feasibility Decision

## 1. 문서 상태

```text
DOCUMENT_PURPOSE = PROVIDER_FEASIBILITY_AND_DECISION_CLOSURE
RESEARCH_DATE = 2026-08-07
RISK_TIER = T2_MODERATE

PROVIDER_FEASIBILITY_RESEARCH = COMPLETED
OFFICIAL_DOCUMENT_ACCESS = COMPLETED_WITH_NOTED_GAPS
OEMBED_2026_CONTRACT_RECHECK = COMPLETED
INSTAGRAM_CONTENT_ACCESS = 0
LOGIN_TOKEN_SECRET_USE = 0
PROVIDER_SIGNUP_PAYMENT = 0
MEDIA_DOWNLOAD = 0

ACCESS_TOKEN_REQUIRED = NO_FOR_TOKENLESS_OEMBED
APP_REVIEW_REQUIRED = NO_FOR_TOKENLESS_OEMBED
META_APP_CONFIGURATION_REQUIRED = NO_FOR_TOKENLESS_OEMBED
TOKENLESS_OEMBED_RATE_LIMIT = UNVERIFIED

P2B_S1_IMPLEMENTATION = NOT_AUTHORIZED
EXTERNAL_FETCH = NOT_AUTHORIZED
INSTAGRAM_ACCESS = NOT_AUTHORIZED
MEDIA_DOWNLOAD = NOT_AUTHORIZED
```

이 문서는 실제 Instagram 콘텐츠나 사용자 계정에 접근하지 않고 Meta 및 후보 provider의 공식 공개 문서만 조사한 결과다. 기술적 가능성, Meta 정책상 허용 경로, 현재 사용자 승인 범위를 서로 분리한다. 이 문서는 법률 자문이 아니며, third-party 자동 수집을 채택하려면 별도의 약관·정책 검토와 사용자 승인이 필요하다.

## 2. 결론 요약

```text
PROVIDER_ROUTE = NO_CURRENTLY_APPROVED_AUTOMATIC_ROUTE
MANUAL_FALLBACK = RETAIN

ARBITRARY_PUBLIC_POST_REEL_AUTOMATION = DEFER
FIRST_COMMENT_SEMANTICS = UNVERIFIED
META_OEMBED_ROLE = DISPLAY_ONLY_NOT_INGESTION
META_OEMBED_PUBLIC_SCOPE = POST_CAROUSEL_REEL_EMBED_DISPLAY
META_API_SCOPE = OWNED_OR_MANAGED_PROFESSIONAL_MEDIA_ONLY
THIRD_PARTY_ROUTE = TECHNICALLY_PARTIAL_POLICY_REVIEW_REQUIRED

P2B_S1_DESIGN_READINESS = FEASIBLE
P2B_S1_IMPLEMENTATION_READINESS = HOLD_DECISIONS
```

2026-06-15 변경으로 Meta tokenless oEmbed는 access token, App Review 또는 Meta app configuration 없이 public Instagram post·carousel·Reel을 embed 표시할 수 있다. 그러나 이 변경은 embed HTML을 통한 표시 계약이며 `CAPTION + FIRST_COMMENT + MEDIA`를 추출·수집하고 로컬에 보존하는 ingestion 계약이 아니다. 따라서 현재 요구인 임의의 공개 Instagram post·Reel permalink 전체 자동 수집 경로를 공식 Meta API나 oEmbed가 제공하지 않는다는 결론은 유지한다. 조사한 third-party 3개는 permalink 기반 metadata 수집을 기술적으로 지원하지만, 모두 외부 서비스 계정과 API key가 필요하고 `FIRST_COMMENT`의 제품 의미를 보장하지 않는다. 또한 unofficial scraping 또는 scraping service에 의존하므로 Meta의 자동 수집 정책과의 정합성이 별도로 승인되어야 한다.

따라서 현재 가능한 최소 기능은 URL을 사용자가 입력하고, caption과 사용자가 화면에서 식별한 정확한 댓글을 직접 붙여넣으며, media는 기존 로컬 업로드 경로로 제공하는 방식이다. 자동 수집은 보류한다.

## 3. 조사 기준과 판정 용어

| 판정 | 의미 |
|---|---|
| `FEASIBLE` | 요구 범위와 의미를 공식 계약으로 충족하며 현재 구현 후보가 될 수 있음 |
| `PARTIALLY_FEASIBLE` | 일부 필드는 제공하지만 계정·정책·의미·보존 조건 중 하나 이상이 미충족 |
| `NOT_FEASIBLE` | 공식 계약 또는 현재 승인 범위에서 요구를 충족할 수 없음 |
| `UNVERIFIED` | 열람 가능한 1차 자료만으로 해당 주장을 확정할 수 없음 |

검색 결과 요약은 근거로 사용하지 않았다. 원문이 접근 가능한 경우에만 확정했고, 정확한 보장 문구를 확인하지 못한 항목은 `UNVERIFIED`로 유지했다. Meta의 공식 발표 및 현재 Instagram oEmbed 문서 URL을 권위 링크로 기록했고, 2026 tokenless 계약 본문은 Meta가 운영하는 `facebook/meta-embeds-for-wordpress` 저장소와 Facebook 명의의 공식 WordPress 배포 원문으로 직접 교차 확인했다. 공식 저장소는 no access tokens or configuration, tokenless API, Instagram image·video·carousel과 Reel URL 범위를 명시한다. tokenless 경로의 수치화된 현재 rate limit은 직접 확인하지 못했으므로 `TOKENLESS_OEMBED_RATE_LIMIT = UNVERIFIED`로 유지한다.

## 4. 수집 방식별 현실성 비교

| 방식 | URL·계정 범위 | 인증 | Caption | FIRST_COMMENT | Media | URL 수명·제한 | 비용 | 보존·attribution | 안정성·로컬 앱 적합성 | 종합 |
|---|---|---|---|---|---|---|---|---|---|---|
| Meta Instagram API | app user가 소유·관리하는 Business/Creator 중심. Business Discovery는 다른 professional account의 기본 metadata 조회를 지원하지만 임의 개인 계정 permalink 계약이 아님 | Meta app, access token, permissions, 경우에 따라 App Review | 관리 범위 media에서는 지원 가능 | comments 조회는 가능하나 ordering을 지원하지 않으며 고정·작성자 우선·화면 순서를 보장하지 않음 | media URL metadata는 가능하지만 임의 공개 permalink binary 취득 계약은 아님 | rate limit과 token 수명 관리 필요. 정확한 전체 한도는 app/use case별로 달라 본 조사에서 단일 값으로 확정하지 않음 | 공식 API 사용 자체의 단일 정액 가격은 `UNVERIFIED`; 앱 운영·검토 비용은 별도 | Meta Platform Terms와 permissions 범위 준수 필요 | 공식 지원 경로지만 계정·token·app lifecycle이 필요하고 순수 로컬·무인증 경로가 아님 | `PARTIALLY_FEASIBLE` |
| Meta tokenless oEmbed | public Instagram post·carousel·Reel의 embed 표시. private content와 Stories는 지원 범위가 아님 | tokenless 경로는 access token, App Review, Meta app configuration 불필요 | embed에 caption이 시각적으로 포함될 수 있어도 caption ingestion 필드·저장 계약은 없음 | 제공 안 함 | embed HTML과 frontend script로 원격 표시. 원본 media binary 수집 경로 아님 | 기존 5,000,000 requests/24h는 token 사용 app access token 경로의 문서화 값이며 tokenless 한도가 아님. tokenless 현재 수치 제한은 `UNVERIFIED` | tokenless endpoint 자체 가격은 확인되지 않음 | 표시를 위한 embed HTML 사용 계약이다. caption·comment·media binary 추출 또는 영속화 경로로 승격하지 않음 | app/token 없이 단일 public URL 표시는 가능하지만 MyDocuMgm ingestion/storage에는 부적합 | `PARTIALLY_FEASIBLE_DISPLAY_ONLY` |
| Apify Instagram Scraper | provider 설명상 공개 post·Reel URL 직접 입력 | Apify 계정/API 사용 | 지원 | `firstComment`, `latestComments` 필드가 있으나 `firstComment`의 정렬·고정·작성자 의미가 정의되지 않음 | 이미지·영상 URL 제공; binary 저장과 URL 수명은 별도이며 정확한 만료 보장은 `UNVERIFIED` | free plan에서 댓글 페이지 약 15개라는 actor 안내. actor 가격은 plan에 따라 약 $2.70~$1.50/1,000 results | Free plan $5 monthly credits, no card. 가입 필요 | unofficial API라고 명시. Meta 허가와 저장 권리 정합성은 `UNVERIFIED` | 기술적으로 간단하지만 외부 cloud/API key 의존, 로컬 전용 아님 | `PARTIALLY_FEASIBLE` |
| Bright Data Instagram Scraper API | 공식 제품 문서상 `/p` post 및 `/reel` URL, comments endpoint | Bright Data 계정/API token | 지원 | comment 데이터는 제공하지만 ordering·pinned·author-first 의미 보장 없음 | photo/video URL 제공. media link는 24시간 후 만료한다고 명시 | sync 최대 20 URLs, async 최대 5,000 URLs. snapshot은 30일 보존 | `FREE_TIER = 5,000_RECORDS_PER_MONTH_NO_CARD`; Instagram 전용 가격표는 PAYG `$1.50/1K`, Scale 추가분 `$1.30/1K`; 사이트 공통 배너는 `$0.75/1K`, Instagram FAQ는 `$1.00/1K`를 표시하므로 확정 적용가는 plan·product별 확인 필요 | provider snapshot 보존과 Meta 정책 정합성에 대한 별도 승인 필요 | 대량 처리에는 적합할 수 있으나 외부 service/token 필수, 로컬 전용 아님 | `PARTIALLY_FEASIBLE` |
| Scrape Creators | 공식 문서상 공개 Instagram post·Reel URL 및 comments endpoint | 계정/API key | 지원 | comments/author/timestamp/cursor 제공. 문서가 comments endpoint 성공률을 약 90%로 설명하며 순서·pin 의미 보장 없음 | image/video URL 제공. `download_media=true` 영구 URL 기능은 별도 유료 동작이며 현재 금지 | comments가 error-prone이라고 명시. 일반 권장은 500 concurrent 미만. 확정 rate limit은 `UNVERIFIED` | 100 free credits, no card. $47/25,000 credits, $497/500,000 credits 등 | cache max age를 1·3·7·14·30일로 선택 가능. 그 밖의 정확한 retention은 `UNVERIFIED` | 외부 service/API key 필수. provider-side permanent media 기능은 현재 storage 계약과 다름 | `PARTIALLY_FEASIBLE` |
| 사용자 직접 붙여넣기·로컬 업로드 | 사용자가 접근할 수 있는 URL과 화면 내용. 외부 자동 조회 없음 | provider 인증 없음 | 사용자가 원문을 직접 입력 | 사용자가 정의된 의미에 맞는 댓글을 직접 식별·입력 | 기존 로컬 upload로 제공 | 자동 rate limit 없음. 입력 크기 검증은 제품 계약으로 관리 | provider 비용 없음 | 사용자가 저장 대상을 선택하고 출처·attribution을 별도 입력해야 함 | 자동화는 낮지만 현재 승인·로컬 구조에 가장 적합 | `FEASIBLE` |
| 자체 scraping 또는 browser-assisted | 공개 페이지라도 UI·login 상태·지역·개인화에 의존 | 경우에 따라 session/cookie/login 필요 | DOM에서 보일 수 있으나 안정 계약 없음 | 표시 순서가 개인화될 수 있고 의미 보장 없음 | URL 추출은 가능할 수 있으나 다운로드는 별도 | 차단·markup 변경·rate limit 위험 | 개발·유지 비용, 차단 위험 | Meta는 express written permission 없는 자동 수집을 금지. 현재 permission 없음 | 현재 승인에서 login/session 및 실제 요청도 금지되어 부적합 | `NOT_FEASIBLE` |

## 5. 핵심 주장 분리

| 방식 | `ARBITRARY_PUBLIC_POST_REEL_SUPPORTED` | `CAPTION_SUPPORTED` | `FIRST_COMMENT_SUPPORTED` | `MEDIA_BINARY_SUPPORTED` | `FREE_LOCAL_ONLY_USE_SUPPORTED` |
|---|---|---|---|---|---|
| Meta Instagram API | `NO` | `PARTIAL_MANAGED_SCOPE` | `NO_EXACT_SEMANTICS` | `NO_ARBITRARY_PUBLIC_BINARY_CONTRACT` | `NO` |
| Meta tokenless oEmbed | `YES_FOR_EMBED_DISPLAY_ONLY` | `NO_CONTRACT` | `NO` | `NO` | `NO_FOR_INGESTION` |
| Apify | `PROVIDER_CLAIM_YES` | `YES` | `SEMANTICS_UNVERIFIED` | `URL_ONLY_BINARY_SEPARATE` | `NO` |
| Bright Data | `PROVIDER_CLAIM_YES` | `YES` | `SEMANTICS_UNVERIFIED` | `URL_ONLY_BINARY_SEPARATE` | `NO` |
| Scrape Creators | `PROVIDER_CLAIM_YES` | `YES` | `SEMANTICS_UNVERIFIED` | `OPTIONAL_PROVIDER_DOWNLOAD_NOT_AUTHORIZED` | `NO` |
| 사용자 직접 입력·업로드 | `USER_SUPPLIED_ONLY` | `YES_MANUAL` | `YES_AFTER_USER_DEFINITION_MANUAL` | `YES_LOCAL_UPLOAD` | `YES` |
| 자체 scraping/browser-assisted | `UNSTABLE_AND_NOT_AUTHORIZED` | `UNVERIFIED` | `UNVERIFIED` | `NOT_AUTHORIZED` | `NO` |

`MEDIA_BINARY_SUPPORTED`는 응답에 media URL 문자열이 있다는 뜻과 실제 binary를 합법적·안정적으로 다운로드하고 보존할 수 있다는 뜻을 구분한다. 조사한 자동 후보 중 현재 승인 아래에서 binary download까지 가능한 경로는 없다.

### 5.1 Bright Data 공식 가격 표시 충돌

2026-08-07에 Instagram 기본, comments, reels 공식 페이지를 다시 확인했다. 세 페이지의 Instagram 전용 가격표는 Free Tier 5K records/month, no credit card, PAYG `$1.5/1K record`, Scale `$499/month`에 384,000 records 포함 및 `$1.3/1K additional records`를 동일하게 표시한다.

동시에 같은 Bright Data 페이지의 사이트 공통 Scraper APIs 배너는 `starts from $0.75/1K records`를 표시하고, Instagram 기본 페이지 FAQ는 `prices start from $0.001 per record`, 즉 `$1.00/1K records`를 표시한다. 이 두 값은 Instagram 전용 PAYG 표의 `$1.50/1K records`와 일치하지 않는다. 따라서 범용 `$0.75` 또는 FAQ `$1.00`를 MyDocuMgm에 적용될 Instagram 확정 가격으로 사용하지 않는다.

```text
FREE_TIER = 5,000_RECORDS_PER_MONTH_NO_CARD
INSTAGRAM_PAYG_DISPLAY = USD_1_50_PER_1K_RECORDS
SCALE_ADDITIONAL_DISPLAY = USD_1_30_PER_1K_RECORDS
SITEWIDE_SCRAPER_API_BANNER = STARTS_FROM_USD_0_75_PER_1K_RECORDS
INSTAGRAM_FAQ_DISPLAY = STARTS_FROM_USD_1_00_PER_1K_RECORDS
EXACT_EFFECTIVE_PRICE = PLAN_AND_PRODUCT_DEPENDENT_OFFICIAL_DISPLAY_CONFLICT
```

실제 계약 가격은 provider 가입·결제 승인 후 선택한 Instagram product와 plan의 checkout 또는 계약서에서 다시 확인해야 한다. 이번 조사에서는 가입, 무료체험, API key 발급 또는 결제를 수행하지 않았다.

Meta tokenless oEmbed의 핵심 계약은 다음처럼 별도로 고정한다.

```text
ACCESS_TOKEN_REQUIRED = NO_FOR_TOKENLESS_OEMBED
APP_REVIEW_REQUIRED = NO_FOR_TOKENLESS_OEMBED
META_APP_CONFIGURATION_REQUIRED = NO_FOR_TOKENLESS_OEMBED

ARBITRARY_PUBLIC_POST_REEL_SUPPORTED = YES_FOR_EMBED_DISPLAY_ONLY
CAPTION_INGESTION_SUPPORTED = NO_CONTRACT
FIRST_COMMENT_SUPPORTED = NO
MEDIA_BINARY_SUPPORTED = NO

TOKEN_BASED_OEMBED_RECORDED_LIMIT = 5_000_000_REQUESTS_PER_24H_APP_TOKEN_PATH
TOKEN_BASED_OEMBED_RECORDED_LIMIT_2026_RECONFIRMED = NO
TOKENLESS_OEMBED_RATE_LIMIT = UNVERIFIED
```

`TOKEN_BASED_OEMBED_RECORDED_LIMIT`는 기존 문서가 인용했던 token 사용 경로의 구분값일 뿐 tokenless quota로 재사용하지 않는다. tokenless 응답이나 embed HTML에 사람이 볼 수 있는 caption 또는 media가 포함되더라도 이를 caption field, comment collection 또는 media binary download 계약으로 해석하지 않는다.

## 6. P2B-D02 — FIRST_COMMENT 의미 검증

현재 요청은 다음과 같다.

```text
REQUESTED_INPUT =
CAPTION
- FIRST_COMMENT
- MEDIA
```

`FIRST_COMMENT`를 대표 댓글, 좋아요가 가장 많은 댓글, AI 선택 댓글 또는 provider 응답의 첫 항목으로 바꾸지 않는다.

| 후보 의미 | 필요한 보장 | 조사 결과 | 판정 |
|---|---|---|---|
| 화면상 가장 위에 표시되는 댓글 | 동일 시점·계정·지역에서의 화면 정렬과 pinned/ranking 상태 | 화면은 session·개인화·시점 영향을 받을 수 있으며 조사한 API/provider는 이 화면 의미를 보장하지 않음 | `UNVERIFIED` |
| 게시물 작성자가 작성한 첫 댓글 | 전체 댓글 집합, 작성자 식별, 안정적인 chronological ordering | author/timestamp 필드가 있는 provider는 있으나 완전한 집합과 정렬 의미를 함께 보장하지 않음 | `UNVERIFIED` |
| 고정 댓글 중 첫 번째 | pinned 여부 필드와 pinned 영역의 안정적 순서 | 검토한 공식 문서에서 pinned 여부와 순서를 보장하는 계약을 확인하지 못함 | `UNVERIFIED` |
| API 반환 순서의 첫 댓글 | 응답 배열 순서의 명시적 의미 | 첫 배열 항목은 관찰할 수 있어도 화면상 첫 댓글·작성자 첫 댓글·고정 첫 댓글과 동일하지 않음. Meta 공식 comments 자료는 ordering을 지원하지 않음 | `NOT_ACCEPTABLE_AS_PRODUCT_SEMANTIC` |

따라서 다음 상태를 유지한다.

```text
FIRST_COMMENT_SEMANTICS_UNVERIFIED = YES
P2B_D02_AUTOMATIC_IMPLEMENTATION_READY = NO
```

사용자가 의도한 의미가 화면상 가장 위의 댓글이라면 권장 최소 계약은 다음과 같다.

```text
P2B-D02_RECOMMENDATION = SCREEN_TOP_FIRST_COMMENT_MANUAL_TRANSCRIPTION_AT_CAPTURE_TIME
```

이는 자동 수집이 아니다. 사용자가 해당 시점에 직접 확인한 문자열을 입력하고, MyDocuMgm은 이를 임의의 다른 댓글로 대체하지 않는다. 다른 세 가지 의미를 원한다면 provider 선택 전에 별도 정의와 증거가 필요하다.

## 7. 후보별 주요 실패 시나리오

### 7.1 Meta Instagram API

- 입력 URL이 app user가 관리하지 않는 개인 또는 공개 계정 콘텐츠이면 필요한 media/comments를 조회할 수 없다.
- token, permission, App Review 또는 professional account 연결이 없으면 실행 경로가 성립하지 않는다.
- comment 결과가 있어도 순서가 제품의 `FIRST_COMMENT` 의미와 일치한다고 보장할 수 없다.
- Business Discovery에서 반환한 다른 계정 media ID를 일반 media endpoint로 다시 조회하면 permission 부족이 발생할 수 있다.

### 7.2 Meta tokenless oEmbed

- tokenless 경로는 public Instagram post·carousel·Reel 단건 embed 표시를 access token, App Review, Meta app configuration 없이 지원한다.
- private, inactive, age-restricted, embed-disabled 콘텐츠와 Stories는 지원 범위가 아니다.
- embed HTML은 Meta frontend script를 이용해 원격 콘텐츠를 표시하는 결과이며 ingestion metadata 계약이 아니다.
- caption이 embed 화면에 보이거나 HTML 일부로 전달되더라도 caption field 추출·영속화 계약으로 승격하지 않는다.
- first comment와 원본 media binary를 제공하지 않으므로 Phase 2B 저장 소스로 사용할 수 없다.
- 기존 token-based integration은 별도 경로이고, 기록된 5,000,000 requests/24h를 tokenless limit으로 간주하지 않는다.

### 7.3 Third-party provider

- provider가 `firstComment` 또는 comments 배열을 반환하더라도 그 값이 화면상 첫 댓글, 작성자 첫 댓글 또는 첫 pinned 댓글인지 확인할 수 없다.
- Meta UI·anti-automation 정책·markup 변경으로 성공률, schema 또는 제공 범위가 바뀔 수 있다.
- free quota가 있어도 외부 계정/API key/cloud processing이 필요하므로 `FREE_LOCAL_ONLY_USE_SUPPORTED`가 아니다.
- media URL은 만료될 수 있고 binary 보존은 별도 요청·비용·권리 검토가 필요하다.
- provider가 기술적으로 수집할 수 있다는 사실만으로 MyDocuMgm의 저장·재사용 권리가 확인되는 것은 아니다.

### 7.4 자체 scraping/browser-assisted

- express written permission 없이 자동 수집하면 Meta의 공개 약관과 충돌한다.
- login/cookie가 필요한 경우 현재 금지 범위를 위반한다.
- 화면 정렬은 개인화될 수 있어 동일 URL에서도 `FIRST_COMMENT`가 달라질 수 있다.
- DOM 변경, challenge, rate limiting으로 유지보수성과 재현성이 낮다.

## 8. 비용·인증·로컬 전용 여부

| 경로 | 무료 시작 가능 | account/login | API key/token | 외부 server/service | 순수 로컬·무인증 자동 수집 |
|---|---:|---:|---:|---:|---:|
| Meta Instagram API | 비용 자체는 단일 값으로 `UNVERIFIED` | 필요 | 필요 | Meta API 필요 | 불가 |
| Meta tokenless oEmbed | endpoint 가격과 수치 rate limit은 `UNVERIFIED` | 불필요 | 불필요 | Meta endpoint와 frontend script 필요 | embed 표시는 가능, ingestion은 불가 |
| Apify | $5 monthly credits | 필요 | 필요 | Apify cloud | 불가 |
| Bright Data | 5,000 records/month, no card. 유료 적용가는 공식 표시 충돌로 plan·product별 확인 필요 | 필요 | 필요 | Bright Data cloud | 불가 |
| Scrape Creators | 100 credits | 필요 | 필요 | Scrape Creators service | 불가 |
| 수동 붙여넣기·로컬 업로드 | 가능 | provider account 불필요 | 불필요 | 자동 수집 service 없음 | 가능, 단 수동 입력 |

유료 결제가 없어도 세 provider의 제한된 free quota는 존재한다. Meta tokenless oEmbed는 가입·API key 없이 public 단건을 표시할 수 있지만, 이는 caption·first comment·media binary를 자동 수집하는 경로가 아니다. 따라서 가입·API key·외부 ingestion service 없이 순수 로컬에서 임의 공개 post·Reel을 자동 수집하는 승인 가능한 경로는 확인되지 않았다.

## 9. P2B-D01, D02, D05 결정안

### P2B-D01 — 최초 URL 범위

권장값:

```text
P2B-D01 = PUBLIC_INSTAGRAM_POST_AND_REEL_PERMALINK_MANUAL_INPUT_ONLY_INITIAL
P2B-D01_STATE = USER_SELECTION_REQUIRED
```

- 최초 구현은 사용자가 public Instagram post 또는 Reel permalink를 참고 식별자로 직접 입력하는 범위다.
- URL 입력이 자동 fetch 승인을 뜻하지 않는다.
- tokenless oEmbed로 같은 public permalink를 표시할 수 있게 되었지만, D01은 수집·영속화 범위를 결정하므로 표시 기능만으로 자동 fetch 범위로 확대하지 않는다.
- 공식 Meta 자동 경로를 나중에 선택한다면 app user가 소유·관리하는 professional media 범위로 축소해야 한다.
- 임의 공개 permalink 자동 수집을 원한다면 third-party 정책·비용·secret 관리 결정을 별도로 승인해야 한다.

### P2B-D02 — 첫 댓글의 정확한 의미

권장값:

```text
P2B-D02 = SCREEN_TOP_FIRST_COMMENT_MANUAL_TRANSCRIPTION_AT_CAPTURE_TIME
P2B-D02_STATE = USER_SELECTION_REQUIRED
FIRST_COMMENT_SEMANTICS_UNVERIFIED_FOR_AUTOMATION = YES
```

- 사용자가 의도한 댓글을 화면에서 직접 식별하고 문자열을 붙여넣는다.
- 자동화가 provider 배열의 첫 항목으로 의미를 바꾸지 않는다.
- 화면상 첫 댓글이 아니라 작성자 첫 댓글 또는 첫 pinned 댓글이 목적이라면 이 값을 승인하지 말고 의미를 다시 선택해야 한다.

### P2B-D05 — 현실적인 provider 조합

권장값:

```text
P2B-D05 = MANUAL_CAPTION_AND_FIRST_COMMENT_PLUS_LOCAL_MEDIA_UPLOAD_NOW
META_OEMBED = DISPLAY_ONLY_NOT_INGESTION
META_API = FUTURE_OWNED_PROFESSIONAL_ONLY_IF_AUTHORIZED
THIRD_PARTY_AUTOMATION = DEFER_POLICY_AND_PROVIDER_APPROVAL
P2B-D05_STATE = USER_SELECTION_REQUIRED
```

- 현재는 caption과 정확한 first comment를 수동 입력하고 media는 로컬 업로드한다.
- 2026 tokenless 변경으로 app/token 없이 oEmbed 표시가 가능해졌지만, caption field·first comment·media binary 계약은 추가되지 않았다. 따라서 D05 권장 결론은 바뀌지 않는다.
- oEmbed는 별도 표시 요구가 승인될 때만 embed 용도로 검토하며 저장용 ingestion source로 사용하지 않는다.
- Meta API는 향후 app/token/App Review와 관리 professional account 범위가 승인될 때 별도 slice로 검토한다.
- third-party 자동화는 Meta 정책 정합성, provider 약관, 비용, secret 보관, media URL 수명과 `FIRST_COMMENT` 의미가 승인되기 전까지 보류한다.

## 10. P2B-S1 진행 가능성

```text
P2B_S1_PROVIDER_NEUTRAL_DESIGN = MAY_PROCEED_AFTER_USER_DECISION
P2B_S1_AUTOMATIC_FETCH_IMPLEMENTATION = NOT_READY
P2B_S1_IMPLEMENTATION = NOT_AUTHORIZED
```

provider-neutral한 schema·상태 설계는 다음 조건을 반영해 준비할 수 있다.

- source URL과 acquisition mode를 구분한다.
- `MANUAL`과 미래의 `META_API`·`THIRD_PARTY`를 구분한다.
- caption과 first comment의 원문, 입력 방식, 획득 시각을 구분한다.
- media URL metadata와 로컬 managed binary를 같은 상태로 취급하지 않는다.
- provider 응답의 첫 comment를 `FIRST_COMMENT`로 자동 승격하지 않는다.

그러나 본 문서는 schema·상태 구현을 승인하지 않는다. 최소한 P2B-D01, D02, D05의 사용자 선택이 끝나야 하며, 자동 provider를 선택할 경우 별도의 인증·policy·secret·비용 계약이 필요하다.

## 11. 사용자 결정 블록

아래 값은 권장안이며 아직 `USER_SELECTED`가 아니다.

```text
P2B-D01 = PUBLIC_INSTAGRAM_POST_AND_REEL_PERMALINK_MANUAL_INPUT_ONLY_INITIAL
P2B-D01_STATE = RECOMMENDED_USER_SELECTION_REQUIRED

P2B-D02 = SCREEN_TOP_FIRST_COMMENT_MANUAL_TRANSCRIPTION_AT_CAPTURE_TIME
P2B-D02_STATE = RECOMMENDED_USER_SELECTION_REQUIRED

P2B-D05 = MANUAL_CAPTION_AND_FIRST_COMMENT_PLUS_LOCAL_MEDIA_UPLOAD_NOW
P2B-D05_STATE = RECOMMENDED_USER_SELECTION_REQUIRED

PROVIDER_ROUTE = NO_CURRENTLY_APPROVED_AUTOMATIC_ROUTE
MANUAL_FALLBACK = RETAIN
AUTOMATIC_COLLECTION = DEFER

P2B_S1_IMPLEMENTATION = NOT_AUTHORIZED
EXTERNAL_FETCH = NOT_AUTHORIZED
INSTAGRAM_ACCESS = NOT_AUTHORIZED
MEDIA_DOWNLOAD = NOT_AUTHORIZED
```

## 12. 공식 자료

### Meta

- [Meta: Tokenless Access to Meta oEmbed APIs — 2026-06-15](https://developers.facebook.com/blog/post/2026/06/15/tokenless-access-to-meta-oembed-apis/)
- [Meta official meta-embeds-for-wordpress repository](https://github.com/facebook/meta-embeds-for-wordpress)
- [Current Instagram Platform oEmbed documentation](https://developers.facebook.com/documentation/instagram-platform/oembed)
- [Legacy token-based Meta oEmbed Read reference](https://developers.facebook.com/docs/features-reference/oembed-read)
- [Instagram oEmbed](https://developers.facebook.com/docs/instagram-platform/oembed)
- [Instagram Business Discovery](https://developers.facebook.com/docs/instagram-platform/instagram-api-with-facebook-login/business-discovery)
- [Meta official Instagram API Postman documentation](https://www.postman.com/meta/instagram/documentation/6yqw8pt/instagram-api)
- [Meta official Instagram comments request](https://www.postman.com/meta/instagram/request/23987686-c91bedd7-ac95-43c9-af29-8570fe293ace)
- [Meta Terms for Automated Data Collection](https://www.facebook.com/legal/automated_data_collection_terms)
- [Instagram Terms of Use](https://www.facebook.com/help/581066165581870)
- [Instagram embed help](https://www.facebook.com/help/620154495870484/)

### Third-party provider

- [Apify Instagram Scraper](https://apify.com/apify/instagram-scraper)
- [Apify pricing](https://apify.com/pricing)
- [Bright Data Instagram Scraper API introduction](https://docs.brightdata.com/datasets/scrapers/instagram/introduction)
- [Bright Data Instagram first request](https://docs.brightdata.com/datasets/scrapers/instagram/send-first-request)
- [Bright Data scraper FAQ](https://docs.brightdata.com/datasets/scrapers/scrapers-library/faqs)
- [Bright Data Instagram pricing](https://brightdata.com/products/web-scraper/instagram)
- [Bright Data Instagram Comments pricing](https://brightdata.com/products/web-scraper/instagram/comments)
- [Bright Data Instagram Reels pricing](https://brightdata.com/products/web-scraper/instagram/reels)
- [Scrape Creators Instagram post endpoint](https://docs.scrapecreators.com/v1/instagram/post/)
- [Scrape Creators Instagram comments endpoint](https://docs.scrapecreators.com/v2/instagram/post/comments/)
- [Scrape Creators pricing](https://scrapecreators.com/)

## 13. 최종 조사 판정

```text
RESEARCH_COMPLETENESS = PASS
CORE_PRIMARY_SOURCE_AVAILABLE = YES
UNVERIFIED_CLAIMS_EXPLICITLY_MARKED = YES
OEMBED_2026_TOKENLESS_CONTRACT_CORRECTED = YES
PROVIDER_FEATURE_AND_PRICING_RECHECKED = YES_WITH_BRIGHT_DATA_OFFICIAL_DISPLAY_CONFLICT_NOTED

PROVIDER_ROUTE = NO_CURRENTLY_APPROVED_AUTOMATIC_ROUTE
MANUAL_FALLBACK = RETAIN

P2B-D01 = DECISION_READY_USER_SELECTION_REQUIRED
P2B-D02 = DECISION_READY_USER_SELECTION_REQUIRED
P2B-D05 = DECISION_READY_USER_SELECTION_REQUIRED

P2B_D01_D02_D05_USER_APPROVED = NO
P2B_S1_IMPLEMENTATION = NOT_AUTHORIZED
EXTERNAL_FETCH = NOT_AUTHORIZED
MEDIA_DOWNLOAD = NOT_AUTHORIZED
PRODUCTION_READY = NO

PASS_MYDOCUMGM_PHASE2B_S0_REPAIR2_BRIGHT_DATA_PRICE_CONFLICT
```

## 14. Phase 2B S1 사용자 승인

이 절은 위 S0 조사 이력과 당시의 미승인 상태를 변경하지 않고, 이후 사용자가 확정한 현재 권위 결정만 기록한다.

```text
P2B-D01 = PUBLIC_INSTAGRAM_POST_AND_REEL_PERMALINK_MANUAL_INPUT_ONLY_INITIAL
P2B-D01_STATE = USER_APPROVED

P2B-D02 = POST_AUTHOR_PINNED_COMMENT_MANUAL_TRANSCRIPTION
P2B-D02_STATE = USER_APPROVED
PINNED_COMMENT_TARGET = COMMENT_WRITTEN_BY_POST_AUTHOR_AND_PINNED_BY_AUTHOR
MULTIPLE_PINNED_COMMENTS = TOPMOST_AUTHOR_WRITTEN_PINNED_COMMENT_ONE
AUTOMATIC_COMMENT_DETECTION = NOT_AUTHORIZED

P2B-D05 = MANUAL_CAPTION_AND_AUTHOR_PINNED_COMMENT_PLUS_LOCAL_MEDIA_UPLOAD_NOW
P2B-D05_STATE = USER_APPROVED

P2B_D01_D02_D05_USER_APPROVED = YES
P2B_S1_IMPLEMENTATION = AUTHORIZED
EXTERNAL_FETCH = NOT_AUTHORIZED
INSTAGRAM_ACCESS = NOT_AUTHORIZED
MEDIA_DOWNLOAD = NOT_AUTHORIZED
PRODUCTION_READY = NO
```
