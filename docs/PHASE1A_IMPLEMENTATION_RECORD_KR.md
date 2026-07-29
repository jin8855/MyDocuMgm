# MyDocuMgm Phase 1A 구현 기록

## 작업 식별

- 작업명: `MYDOCUMGM_PHASE1A_PROJECT_FOUNDATION_SCHEMA_AND_LOCAL_CONTENT_IMPLEMENTATION`
- 제품 표시명: `나의 생활북`
- 프로젝트: `C:\EtcProject\MyDocuMgm`
- 승인 범위: 프로젝트 기반, 스키마, 로컬 콘텐츠 관리 구현, 비연결 검증
- DB 연결·변경: 0건
- 실제 자료 루트 생성: 0건

## 고정 기술 버전

- .NET SDK `10.0.302`, Target Framework `net10.0`
- Entity Framework Core / SQL Server / Design / dotnet-ef `10.0.10`
- Node.js `24.15.0`, npm `11.12.1`
- Vue `3.5.40`, Vue Router `4.6.4`
- TypeScript `5.9.3`, Vite `8.1.5`, Vue plugin `6.0.8`, vue-tsc `3.3.8`
- Vitest `4.1.10`, jsdom `30.0.1`
- npm 감사 결과: 취약점 0건

모든 직접 npm 의존성은 정확한 버전으로 고정했고 `package-lock.json`을 생성했다. `dotnet-ef`는 전역 설치가 아닌 프로젝트 루트 `dotnet-tools.json`에 고정했다.

## 프로젝트 구조

- `MyDocuMgm.Domain`: 프레임워크 비의존 엔터티, enum, 고정 분류, 상태·분류·상세 일치 규칙, 태그 정규화
- `MyDocuMgm.Application`: 콘텐츠·미디어 유스케이스, 저장소 및 파일 저장 포트, rowversion 충돌
- `MyDocuMgm.Infrastructure`: EF Core SQL Server 모델, 저장소, 로컬 이미지 보안 저장
- `MyDocuMgm.Api`: ProblemDetails 계열 오류, 콘텐츠·태그·분류·미디어 API, 구성/자료 루트 상태를 구분하는 health
- `MyDocuMgm.Web`: Vue Router 기반 요약·목록·등록·상세·편집·이미지 관리 화면
- `MyDocuMgm.UnitTests`, `MyDocuMgm.IntegrationTests`, `MyDocuMgm.Web.Tests`: DB 연결 없는 검증

## Domain 및 스키마

공통 모델은 `Category`, `Content`, `ContentStep`, `Tag`, `ContentTag`, `MediaAsset`, `SourceEvidence`이다. Phase 2의 `ImportJob`과 Phase 4의 `Publication`은 만들지 않았다.

분류별 상세 테이블은 다음과 같다.

- `PlaceDetails`
- `CookingDetails`, `CookingIngredients`
- `ExerciseDetails`
- `CleaningLaundryDetails`
- `TravelDetails`
- `PhotoDetails`
- `StudyDetails`
- `ProductDetails`
- `PhoneComputerDetails`
- `TipDetails`
- `OtherDetails`

11개 분류 GUID는 `CategoryCatalog`에 고정했다. 상세정보가 있는 콘텐츠의 분류를 암묵적으로 바꾸거나 기존 상세정보를 자동 삭제하지 않는다. 분류와 상세정보가 충돌하면 `CATEGORY_DETAIL_CONFLICT` 또는 `CATEGORY_DETAIL_MISMATCH`로 저장을 차단한다.

콘텐츠 상태는 전체 수명주기를 스키마에 보존하지만 Phase 1 UI에서는 `INBOX`, `REVIEW_REQUIRED`, `READY`, `ARCHIVED`만 선택할 수 있다. `DRAFTED`, `PUBLISHED`는 블로그 기능 전에는 선택할 수 없다. 콘텐츠 공개 기본값은 `PRIVATE`, 이미지 공개 허용 기본값은 `false`이다.

## API

- `GET /api/categories`
- `GET|POST /api/contents`
- `GET|PUT|DELETE /api/contents/{id}`
- `POST /api/contents/{id}/restore`
- `GET /api/tags`
- `GET|POST /api/contents/{id}/media`
- `PATCH|DELETE /api/contents/{id}/media/{mediaId}`
- `PUT /api/contents/{id}/media/order`
- `POST /api/contents/{id}/media/{mediaId}/restore`
- `GET /health/live`, `GET /health/ready`

API 포트는 `5080`, 허용 프런트엔드 origin은 `http://localhost:5173`이다. 앱 시작 시 DB 연결을 열거나 자료 루트를 만들지 않는다.

## 로컬 이미지 보안

- JPG/JPEG, PNG, WebP(VP8/VP8L/VP8X), 최대 20MB
- 원본 확장자, 선언 MIME, 실제 파일 서명 일치 검증
- GUID 저장명, 원본 파일명은 메타데이터에만 보존
- SHA-256, 폭·높이, 상대경로, 순서, 설명, 공개 허용 여부 기록
- `Path.GetFullPath` 기준 루트 하위 확인
- 절대경로, `..`, 재분석 지점·심볼릭 링크 경유 차단
- `PENDING` 선기록, 파일 저장 후 `READY`, 실패 시 파일 보상 삭제 및 `FAILED`
- soft delete와 복구

Phase 1A에서는 `data\MyDocuMgmData`를 생성하거나 쓰지 않았다. 파일 테스트는 격리된 OS 임시 하위 폴더만 사용하고 자신이 만든 폴더를 정리한다.

## EF 마이그레이션 및 SQL

- 마이그레이션: `src\MyDocuMgm.Infrastructure\Data\Migrations\20260729074010_InitialPhase1LocalContent.cs`
- 모델 스냅샷: `src\MyDocuMgm.Infrastructure\Data\Migrations\MyDocuMgmDbContextModelSnapshot.cs`
- 적용용 SQL: `artifacts\phase1a\sql\MyDocuMgm.InitialPhase1LocalContent.sql`
- 적용용 SQL SHA-256: `871ED9849BF7FADCC8443740BB92495E359CDD803F2F3A06EA02AAB3BCF202A6`
- 검증 SQL: `artifacts\phase1a\sql\MyDocuMgm.VerifyPhase1.sql`
- 검증 SQL SHA-256: `C1F02ACCC32C3110A0779E4DA6337CCE2BBE93FD042727B93403909BC6A708AD`

적용용 SQL은 기존 `MyDocuMgm` DB를 전제로 하며 `CREATE DATABASE`, `DROP DATABASE`, 다른 사용자 DB 참조를 포함하지 않는다. 검증 SQL은 SELECT만 포함한다. `dotnet ef database update`는 실행하지 않았다.

## 공통 문서 정렬

- `LIFEBOOK_COMMON_INSTRUCTION_KR.md`
  - 변경 전: `51AD52A40F6E1B371E4D70C726C2AC60A3081CD61CFBF29CE00A20E42EE789C6`
  - 변경 후: `F2CF11401F3A8944E97F75A3B536EAA75E92D610994AD2556E2E08EAD3CE24F2`
- `LIFEBOOK_PHASE_INSTRUCTIONS_KR.md`
  - 변경 전: `E968319A8BEAD6DAED1A5CE2C8007C87AD772FE5C37CD9770E2135BBCA6359FE`
  - 변경 후: `1637EBDA52CC844713A4E8817E85444D7D4D7B620711190C7101D2C02B4C03F9`

기존 문서를 삭제하거나 이름을 바꾸지 않았다. 프로젝트/namespace, 표시명, DB, 자료 루트, AI 정책, Codex 샌드박스 DB 제한과 충돌하는 부분만 정렬했다.

## Phase 1B 경계

사용자가 다음을 직접 수행하기 전에는 DB 통합 성공을 주장하지 않는다.

1. `.local\appsettings.Local.example.json`을 `.local\appsettings.Local.json`으로 복사하고 값 검토
2. SSMS에서 대상 DB가 `MyDocuMgm`인지 확인
3. 적용용 SQL 검토 및 실행
4. 검증 SQL 실행 결과 보관
5. `data\MyDocuMgmData` 생성 및 접근 권한 확인
6. API/프런트엔드 실행 후 실제 DB 연동 브라우저 스모크 검증
