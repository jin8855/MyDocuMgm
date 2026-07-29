# Phase 1A Repair 1 구현 기록

## 작업 식별

- 작업명: `MYDOCUMGM_PHASE1A_REPAIR1_WIREFRAME_V7_SCHEMA_UI_AND_SQL_SAFETY_ALIGNMENT`
- 기준 커밋: `5a6a1111008cc40bcee3df4d2031b528a5389a6d`
- 구현 기준: `docs\wireframes\MYDOCUMGM_WIREFRAME_V7_KR.html`
- 구현 기준 bytes: `54,387`
- 구현 기준 SHA-256: `9A686ECE38520DFCD0D116117A9F582E1EF53FEBFA2DDFDDA201E7DCD48AB0A1`
- 실제 DB 연결·SQL 실행·자료 루트 생성·push: 모두 `0`

## ALREADY_DONE 보존

기존 .NET 10/EF Core 10, Vue 3/TypeScript/Vite, 계층 분리, 11개 고정 분류 코드,
rowversion, soft delete, 로컬 미디어 보안 저장 정책, ProblemDetails와 health endpoint를
교체하거나 되돌리지 않고 확장했다.

## Domain·DB 변경

- `WorkflowStep`: `URL → ANALYSIS_REVIEW → CATEGORY_EDIT → MEDIA → DETAIL → BLOG_DRAFT → COMPLETED`
- 인접 단계 이동 규칙과 `CK_Contents_CurrentWorkflowStep`
- `CookingIngredients`: `IngredientType`, `IsPrimary`, `RowVersion`
- `Categories`: `IsActive`, `RowVersion`; `PHONE_COMPUTER` 표시명 `폰&컴`
- `CategorySearchAttributes`: 고정 코드 카탈로그 기반 검색 속성 메타데이터
- `MediaAssets`: `SourceTimestampMs`, `IsSelected`, 페이징·중복 후보용 인덱스
- `ContentSteps.MediaAssetId`: 삭제된/다른 콘텐츠 미디어 지정 차단, `Restrict` FK
- 보정 마이그레이션: `20260729144229_AlignWireframeV7Schema`
- 기존 데이터 안전 기본값: workflow `URL`, ingredient type `부재료`

## API·Application 변경

- 콘텐츠 검색: `majorCategory`, `attributeKey`, `attributeValue`, `status`, `searchScope`,
  `keyword`, `page`, `pageSize`
- `searchScope`: `ALL | TAG`; `pageSize`: `24 | 48 | 96`
- 임의 `attributeKey`를 SQL 식별자로 사용하지 않고 고정 매핑 외 입력을 거절
- 재료 전용 조회·추가·수정·삭제·순서 API와 복수 검색용 주재료 지원
- 11개 고정 분류의 표시명·순서·활성 상태와 검색 속성 관리 API
- 미디어 `ALL | SELECTED | DUPLICATE`, 시간순, 서버 페이지 응답과 단일 선택 편집
- workflow 단계 갱신 API와 목록의 현재 단계·블로그 초안 상태 표시

## Vue 구조와 책임

- `app`: 앱 셸과 라우터
- `pages`: 작업보드, 작업목록, workflow 조정, 분류 관리, 모바일 읽기, 오류 상태
- `features\content-search`: 동적 분류 검색
- `features\content-workflow`: stepper, footer, URL·상세·완료 단계
- `features\analysis-review`: 분석 검토 단계
- `features\category-editing`: 분류별 편집 단계 조합
- `features\ingredient-editing`: 재료 표·행·대화상자·검색용 주재료
- `features\media-management`: 필터·수량·썸네일·페이지·선택 편집·상태
- `features\category-management`: 고정 분류와 검색 속성 편집
- `features\blog-draft`: 블로그 초안 단계
- `shared`: API, 타입, proxy 안전 복제, base/shell/feature CSS

왼쪽 메뉴는 `작업보드`, `작업목록`, `분류 관리`, `모바일 읽기` 네 항목만 유지했다.
내부 단계는 상단 7단계와 하단 이전·임시저장·다음으로 제공하며, 상세 화면의
편집·삭제·닫기는 상단에만 둔다. 미저장 이동은 자체 확인 대화상자로 차단한다.

## SQL 산출물

| 파일 | 이전 SHA-256 | 변경 후 SHA-256 |
|---|---|---|
| `artifacts\phase1a\sql\MyDocuMgm.InitialPhase1LocalContent.sql` | `871ED9849BF7FADCC8443740BB92495E359CDD803F2F3A06EA02AAB3BCF202A6` | `5085BDD3FB9DE1E7370450BB59DE97AF24820EE5CE6AE621D5529A93D1AC3BBE` |
| `artifacts\phase1a\sql\MyDocuMgm.VerifyPhase1.sql` | `C1F02ACCC32C3110A0779E4DA6337CCE2BBE93FD042727B93403909BC6A708AD` | `BC7D10DFE99039051FDCFA1D16F22AFE84881D9DBE0CC1BEE766953E94BC4CB5` |

적용 SQL은 DDL·transaction보다 먼저 `DB_NAME()`, `SET NOCOUNT ON`,
`SET XACT_ABORT ON`과 `THROW 51000`을 수행한다. 검증 SQL은 SELECT 전용이며
첫 결과 집합 이후 모든 결과 집합을 `DB_NAME() = N'MyDocuMgm'`으로 제한한다.

## 보류 범위

- `DEFERRED_PHASE2`: 지각적 유사 이미지 분석·유사도 정렬
- `OUT_OF_SCOPE`: Instagram 수집, AI 분석 실행, 동영상 추출, 블로그 자동 게시,
  Publication/ImportJob, 실제 DB 적용, 배포
