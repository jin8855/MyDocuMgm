# MyDocuMgm Phase 1B 로컬 미디어 저장 범위 결정안

## 1. 문서 상태

```text
TASK = MYDOCUMGM_PHASE1B_LOCAL_MEDIA_STORAGE_SCOPE_DECISION
RISK_GRADE = T2_MODERATE
BASELINE_BRANCH = main
BASELINE_HEAD = 3388081b8c71b9bfefdc1e3c08cc02fbf3942c28
DECISION_TRACE = D-MEDIA-001..D-MEDIA-012
PHASE1B_MEDIA_POLICY = NOT_IMPLEMENTED
PHASE1B_MEDIA_POLICY_RECOMMENDATION = PREPARED
USER_SELECTION = REQUIRED
```

이 문서는 구현 결과가 아니라 사용자 결정을 받기 위한 권고안이다. 사용자가 아래 결정값을 승인하기 전에는 이 문서의 권고값을 구현 승인이나 DB 변경 승인으로 해석하지 않는다.

## 2. 조사 범위와 금지 경계

읽기 전용으로 다음을 대조했다.

- 공통·단계 계약: `docs/LIFEBOOK_COMMON_INSTRUCTION_KR.md`, `docs/LIFEBOOK_PHASE_INSTRUCTIONS_KR.md`
- Phase 1A 기록: `docs/PHASE1A_*_KR.md`
- 권위 UI 기준: `docs/wireframes/MYDOCUMGM_WIREFRAME_V7_KR.html`
- API·애플리케이션: `src/MyDocuMgm.Api`, `src/MyDocuMgm.Application`
- 도메인·DB 모델: `src/MyDocuMgm.Domain`, `src/MyDocuMgm.Infrastructure/Data`
- 저장 구현·설정: `src/MyDocuMgm.Infrastructure/Storage`, `src/MyDocuMgm.Api/appsettings*.json`
- 관련 단위·통합 테스트: `tests/MyDocuMgm.UnitTests`, `tests/MyDocuMgm.IntegrationTests`
- 현재 Vue 미디어 화면: `src/MyDocuMgm.Web/src/features/media-management`

이번 조사에서는 다음 작업을 수행하지 않았다.

- 코드·SQL·migration·설정 변경
- DB/API 연결 또는 실행
- 실제 이미지·자료 루트·임시 파일 생성
- 외부 URL 다운로드 또는 네트워크 접근
- 실제 사용자 이미지·secret·인증정보 사용
- build, test, runtime, browser 실행
- 보호 와이어프레임 수정·이동·staging·commit

## 3. 현재 구조와 확인된 제약

### 3.1 공통 계약

현재 공통 계약은 다음 방향을 요구한다.

- 논리적 자료 루트는 프로젝트 기준 `data/MyDocuMgmData`이다.
- DB에는 파일 바이너리가 아니라 루트 기준 상대경로와 메타데이터를 저장한다.
- 원본 파일명과 실제 저장 파일명을 분리하고, 실제 저장명은 충돌하지 않아야 한다.
- 확장자만 신뢰하지 않고 MIME, 시그니처, 크기, 이미지 정보를 검증한다.
- 경로 이탈, `..`, 절대경로 주입, symlink 또는 reparse point를 통한 루트 탈출을 차단한다.
- 임시 저장, DB 반영, 최종 파일 승격과 실패 보상을 하나의 수명주기로 다룬다.
- 콘텐츠 삭제가 즉시 영구 파일 삭제를 의미하지 않으며 복구 가능성을 보존한다.
- DB와 `data/MyDocuMgmData`는 함께 백업·복원하고 상대경로, 크기, 해시를 검증한다.
- 원본 이미지와 블로그용 파생 이미지를 구분한다.
- 이미지 기본 공개 상태는 비공개이며 명시적으로 허용된 이미지만 외부 산출물에 포함한다.
- 향후 외부 자료 수집 단계에서는 출처 URL과 작성자 정보를 보존한다.

공통 문서의 해당 절은 `권장 구조:`라는 권위 수준으로 `media/place`, `media/cooking` 등의 예시를 제시한다. 이어지는 규칙의 “DB에는 `media/cooking/...` 형태의 상대경로만 저장한다”는 문구도 절대경로가 아닌 루트 기준 상대경로를 설명하는 예시다. 분류 segment를 반드시 물리 경로에 포함하라는 강제 문구는 없다. 따라서 Phase 1B 기본 권고는 분류를 DB 메타데이터로만 관리하고 `media/{contentId}/{mediaId}`를 사용하는 것이다.

### 3.2 현재 설정과 자료 루트

`src/MyDocuMgm.Api/appsettings.json`에는 다음 항목만 있다.

```text
Storage.RootPath = ""
Storage.MaxImageBytes = 20971520
```

- 실제 루트는 구성되지 않았다.
- 프로젝트의 로컬 전용 설정 파일을 선택적으로 읽는 구조는 있으나 이번 조사에서 그 파일을 생성하거나 비밀값을 조회하지 않았다.
- readiness 확인은 루트 미구성, 루트 없음, 사용 가능 상태를 구분한다.
- `LocalMediaStorage`는 완전한 절대경로만 허용한다.
- `.gitignore`는 `data/MyDocuMgmData/`와 로컬 설정·runtime artifact를 추적 대상에서 제외한다.
- 웹 애플리케이션 시작 시 자료 루트를 자동 생성하는 명시적 정책은 없다.

따라서 논리적 표준 위치와 runtime 구성값을 구분해야 한다. 논리적 표준은 `data/MyDocuMgmData`이고, runtime에는 이를 가리키는 정규화된 절대경로를 명시하는 것이 현재 코드와 맞는다.

### 3.3 현재 파일 저장 동작

`src/MyDocuMgm.Infrastructure/Storage/LocalMediaStorage.cs`는 다음 방어를 이미 갖는다.

- JPEG, PNG, WebP 시그니처·MIME·확장자 일치 확인
- 이미지 크기와 가로·세로 정보 확인
- `20,971,520 bytes` 파일 크기 제한
- 원본 파일명의 경로 성분 거부
- 완전한 절대 루트 요구
- 루트 밖으로 벗어나는 결합 경로 거부
- reparse point 검사
- GUID 저장 파일명, SHA-256, 상대경로 반환

그러나 현재 수명주기는 정책 계약으로 확정하기에 부족하다.

- 입력 스트림 전체를 메모리에 적재한다.
- `MemoryStream` 뒤 `ToArray()`를 호출하므로 입력 크기에 비례한 복사본이 추가로 생긴다.
- PNG·JPEG·WebP 헤더에서 형식과 가로·세로를 읽지만 전체 이미지 decode로 손상 여부를 끝까지 확인하지 않는다.
- 최대 가로·세로와 전체 pixel 수 제한이 없다.
- GIF, HEIC, SVG는 지원하지 않지만 각각의 보류·거부 이유가 정책값으로 분리되어 있지 않다.
- EXIF, GPS, 촬영일, orientation을 처리하거나 로그 노출을 제한하는 명시적 정책이 없다.
- 최종 위치 `yyyy/MM/{guid}.{ext}`에 직접 쓴다.
- 같은 볼륨의 임시 파일에서 최종 파일로 원자적 승격하지 않는다.
- 취소, 디스크 부족, 쓰기 중 오류 때 부분 파일을 정리한다는 보장이 없다.
- 디렉터리를 만든 뒤 reparse point를 검사하는 순서가 있어 “첫 쓰기 전에 모든 경로 경계 검증” 계약과 맞지 않는다.
- `DeleteIfExists`는 즉시 물리 삭제만 제공하며 격리, 보존기간, purge 정책이 없다.
- 원본과 파생 파일을 구분하는 저장 역할이 없다.

### 3.4 현재 DB·서비스 수명주기

`MediaAsset`은 다음 핵심 필드를 가진다.

- 원본명, 저장명, 상대경로, MIME, 크기, SHA-256, 가로·세로
- 정렬, 원본 시각, 선택 여부, 설명, 공개 허용 여부
- `PENDING | READY | FAILED`
- soft delete와 삭제 시각
- 생성 시각과 `RowVersion`

DB는 `RelativePath`의 유일성, SHA-256 및 콘텐츠별 검색 인덱스, soft-delete 필터를 갖는다. 이미지 바이너리는 DB에 저장하지 않는다.

현재 업로드 흐름은 다음과 같다.

```text
DB PENDING placeholder 저장
-> 최종 파일 직접 저장
-> DB READY 및 실제 메타데이터 저장
```

두 번째 DB 저장이 실패하면 반환된 최종 파일을 삭제하고 레코드를 `FAILED`로 바꾸는 보상이 있다. 하지만 파일 쓰기 자체가 중간에 실패하여 저장 결과가 반환되지 않으면 부분 파일의 존재 여부를 서비스가 알 수 없다. 오래된 `PENDING`, 누락 파일, 고아 파일, 해시 불일치를 찾는 reconciliation도 없다.

삭제와 복구는 DB soft delete 상태만 바꾼다.

- 삭제 시 파일은 남는다.
- 보존기간과 물리 purge 절차는 없다.
- 복구 시 파일 존재, 루트 경계, 크기, 해시를 다시 확인하지 않는다.
- 콘텐츠 soft delete는 연결 미디어를 물리 삭제하거나 별도 soft delete하지 않는다.
- `ContentStep`이 soft-deleted 미디어를 참조한 상태는 보존된다.

현재 모델에 없는 항목은 다음과 같다.

- 미디어 취득 유형과 소유권: local upload, external URL, capture, derivative
- 미디어별 출처 URL, 작성자, 라이선스 또는 사용 동의
- 원본과 파생물의 관계
- purge 예정일·실행 이력·보존 예외
- 마지막 무결성 검증 시각과 결과
- 격리 상태와 reconciliation 결과
- 저장 루트 또는 저장 정책 버전

권고안의 최소 범위는 기존 `DeletedAtUtc`, `StorageStatus`, SHA-256으로 운용할 수 있다. 다만 동시 업로드까지 포함한 중복 보장, 미디어별 외부 출처, 파생물을 DB 자산으로 관리하거나 purge 감사를 영속화하려면 별도 schema/migration 승인이 필요하다.

### 3.5 API와 UI 계약

- API에는 조회, 업로드, 메타데이터 수정, 정렬, soft delete, 복구가 있다.
- 응답 DTO는 thumbnail URL을 만들지만 현재 컨트롤러에는 해당 thumbnail action이 없다.
- Vue 화면은 실제 `<img>` 대신 placeholder를 표시하며 현재 workflow에 업로드 조작이 노출되지 않는다.
- v7은 이미지 선택, 24·48·96 서버 페이징, 시간순 정렬, 설명, 단계 순서, 블로그 공개 허용을 보여 준다.
- v7의 유사도순, 중복 후보, 후보 다시 만들기는 향후 후보 생성·분석 기능이며 Phase 1B 로컬 저장의 물리 파일 중복 제거 계약으로 간주하지 않는다.
- v7은 외부 URL 자동 다운로드, 물리 hard-delete, 저장 경로 노출, 사용자가 직접 파일을 이동하는 UI를 요구하지 않는다.

## 4. 결정 항목과 선택지

### 4.1 `D-MEDIA-001~012` 추적표

`D-MEDIA-001~012`는 이 문서의 권위 추적 ID다. 현재 D1~D11에서 빠졌던 입력 형식·검증, 자원 한도, EXIF·GPS, 원본 파일명·로그 정책은 D12~D15로 독립시켰다. 하나의 원래 Decision ID가 파일과 DB의 같은 수명주기를 다루는 경우 여러 현재 결정 항목에 대응한다.

| Decision ID | 추적 계약 | 현재 결정 항목 | 현재 근거·공백 | Codex 권장값 |
|---|---|---|---|---|
| `D-MEDIA-001` | 자료 루트, 구성, 생성 책임 | D1 | `RootPath`는 비어 있고 실제 루트 생성은 미승인 | `PROJECT_LOCAL_EXPLICIT_INIT` |
| `D-MEDIA-002` | 로컬 취득·관리 복사본 소유권과 외부 취득 경계 | D2, D10 | 로컬 upload는 있으나 `MediaAsset` provenance가 없음 | `LOCAL_FILE_MANAGED_COPY`, `DEFER_EXTERNAL_URL_ENTIRELY` |
| `D-MEDIA-003` | 상대경로, 저장명, 원본 파일명 | D3, D15 | 현재 `yyyy/MM/{guid}`와 `OriginalFileName`만 존재 | `CONTENT_MEDIA_ID_TREE`, `PRESERVE_BASENAME_DB_ONLY` |
| `D-MEDIA-004` | 허용 형식과 GIF·HEIC·SVG 처리 | D12 | JPEG·PNG·WebP header parser만 존재 | `JPEG_PNG_WEBP_ONLY`, GIF·HEIC 보류, SVG 거부 |
| `D-MEDIA-005` | 확장자·MIME·magic bytes·decode·손상·정확 중복 | D11, D12 | 전체 decode가 없고 SHA 인덱스는 non-unique | `EXTENSION_MIME_MAGIC_BYTES_IMAGE_DECODE`, 콘텐츠 내 재사용 |
| `D-MEDIA-006` | 파일 bytes, 가로·세로, 총 pixel 한도 | D13 | 20 MiB만 있고 pixel 제한이 없음 | `20_MIB_8192_AXIS_40MP` |
| `D-MEDIA-007` | EXIF·GPS·orientation·촬영일 | D14 | `SourceTimestampMs` 외 provenance·privacy 상태가 없음 | `PRIVATE_ORIGINAL_PRESERVE_PUBLIC_DERIVATIVE_STRIP` |
| `D-MEDIA-008` | 임시 파일, DB 상태, 최종 승격, 보상 | D4 | 최종 경로 직접 쓰기 | `TEMP_PENDING_ATOMIC_PROMOTE_READY` |
| `D-MEDIA-009` | soft delete, 보존기간, purge | D5 | soft delete는 있으나 purge 계약이 없음 | `SOFT_DELETE_30D_EXPLICIT_PURGE` |
| `D-MEDIA-010` | 복구 무결성, reconciliation, 백업·복원 | D6, D8, D9 | 복구 전 검증·불일치 진단·통합 backup set이 미구현 | 검증 차단, 보고 전용 진단, DB·원본·manifest 일체 |
| `D-MEDIA-011` | thumbnail·파생물·공개 경계 | D7 | DTO URL은 있으나 endpoint와 파생물 정책이 없음 | `REGENERABLE_THUMBNAIL_CACHE` |
| `D-MEDIA-012` | 사용자 표시용 원본명과 진단 로그 정보 노출 | D15 | basename은 DB에 있으나 로그 redaction 계약이 없음 | `BASENAME_DB_IDS_ONLY_LOG` |

추적 결과:

```text
D_MEDIA_EXPECTED_COUNT = 12
D_MEDIA_MAPPED_COUNT = 12
D_MEDIA_MISSING_COUNT = 0
```

### D1. 자료 루트와 초기화 책임

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `PROJECT_LOCAL_EXPLICIT_INIT` | 논리적 위치는 `data/MyDocuMgmData`, runtime은 그 절대경로를 구성하며 별도 승인된 초기화 절차만 루트를 생성 | 공통 계약, 백업 단위, 현재 절대경로 검사와 일치 | 프로젝트 이동 시 구성값 갱신 필요 |
| `USER_PROFILE_APPDATA` | 사용자 프로필의 애플리케이션 데이터 폴더 사용 | 저장소와 runtime 자료 분리 | 현재 공통 계약·백업 안내와 불일치 |
| `CONFIGURABLE_ANY_ABSOLUTE_ROOT` | 임의 절대경로를 허용 | 유연함 | 지원·복구·경계 검증 경우의 수 증가 |

권장값: `PROJECT_LOCAL_EXPLICIT_INIT`

추가 계약:

- 일반 웹 애플리케이션 시작이 루트를 자동 생성하지 않는다.
- 루트 미구성·부재·권한 오류는 fail-fast 또는 readiness 실패로 명확히 보고한다.
- 초기화, 경로 변경, 기존 루트 채택은 각각 별도 사용자 승인 작업이다.

### D2. 로컬 파일 취득과 소유권

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `LOCAL_FILE_MANAGED_COPY` | 사용자가 고른 원본은 그대로 두고 검증된 복사본을 관리 루트에 저장하며 앱이 복사본을 소유 | 원본 비파괴, 일관된 백업·삭제·복구 | 저장 공간이 추가로 필요 |
| `REFERENCE_SOURCE_IN_PLACE` | 원본의 외부 절대경로만 참조 | 복사 비용 없음 | 이동·권한·드라이브 분리 시 즉시 깨지고 루트 경계를 상실 |
| `MOVE_SOURCE_INTO_ROOT` | 원본을 관리 루트로 이동 | 중복 저장 감소 | 사용자 원본을 파괴적으로 변경 |

권장값: `LOCAL_FILE_MANAGED_COPY`

관리 복사본의 소유권은 MyDocuMgm에 있고, 원본 파일의 소유권은 사용자에게 그대로 남는다. 관리 복사본의 삭제가 원본 파일 삭제를 의미하지 않는다.

### D3. 상대경로와 이름

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `CONTENT_MEDIA_ID_TREE` | `media/{contentId}/{mediaId}/original/{guid}.{ext}` | 분류 변경과 무관한 안정 경로, 콘텐츠·자산·역할 경계가 명확 | 사람이 폴더만 보고 분류를 알 수 없음 |
| `CATEGORY_SNAPSHOT_ID_TREE` | `media/{category-at-ingest}/{contentId}/{mediaId}/original/{guid}.{ext}` | 공통 문서의 범주 예시와 유사 | 현재 분류와 과거 snapshot 혼동 |
| `DATE_GUID_TREE` | 현재처럼 `yyyy/MM/{guid}.{ext}` | 단순함 | 콘텐츠·자산 경계와 원본·파생 역할이 경로에 드러나지 않음 |

권장값: `CONTENT_MEDIA_ID_TREE`

추가 계약:

- 분류는 DB 메타데이터이며 물리 경로 구성요소가 아니다.
- 콘텐츠 분류가 바뀌어도 파일 이동, 이름 변경, 과거 분류 snapshot이 발생하지 않는다.
- DB의 `RelativePath`가 조회 기준이며 디렉터리에서 분류를 추론하지 않는다.
- 원본 파일명은 표시·감사용 메타데이터이며 실제 경로 성분으로 사용하지 않는다.
- DB에는 `/`로 정규화한 루트 기준 상대경로만 저장한다.

### D4. 쓰기·DB 일관성 프로토콜

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `TEMP_PENDING_ATOMIC_PROMOTE_READY` | 검증된 임시 파일, DB `PENDING`, 같은 볼륨 원자적 승격, DB `READY` 순서 | 부분 파일 노출을 줄이고 복구 가능한 상태를 남김 | 단계별 보상·reconciliation 필요 |
| `DIRECT_FINAL_THEN_READY` | 현재처럼 최종 경로에 직접 쓰고 DB를 `READY`로 변경 | 구현이 단순 | 부분 파일과 고아 파일 위험 |
| `DB_READY_BEFORE_FILE` | DB를 먼저 `READY`로 만든 뒤 파일 저장 | DB 흐름 단순 | 존재하지 않는 파일을 정상으로 노출 |

권장값: `TEMP_PENDING_ATOMIC_PROMOTE_READY`

권장 순서:

```text
1. 루트와 모든 상위 경로의 경계·reparse point·쓰기 가능성을 파일 생성 전에 검증
2. root/temp의 고유 .part 파일로 제한 스트리밍하며 크기·시그니처·해시·이미지 정보를 검증
3. 의도한 최종 상대경로와 메타데이터로 DB PENDING 저장
4. 같은 볼륨에서 임시 파일을 최종 경로로 원자적 승격
5. DB READY 저장
6. 실패 지점에 따라 임시 또는 최종 파일을 보상 정리하고 DB는 실패 사실을 숨기지 않음
```

DB와 파일 시스템 사이에는 분산 transaction이 없으므로 완전한 단일 원자성은 주장하지 않는다. 프로세스 중단 뒤 남을 수 있는 `PENDING`, 고아 파일, 누락 파일은 D8의 보고형 reconciliation로 찾는다.

### D5. soft delete, 보존기간, 물리 purge

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `SOFT_DELETE_30D_EXPLICIT_PURGE` | 삭제 즉시 숨기되 파일과 레코드를 30일 보존하고, 승인된 maintenance에서만 purge | 실수 복구와 공간 회수의 균형 | purge 전 백업·참조·무결성 검사 필요 |
| `SOFT_DELETE_KEEP_FOREVER` | soft-deleted 파일을 계속 보존 | 복구 가능성 최대 | 저장 공간이 계속 증가 |
| `IMMEDIATE_HARD_DELETE` | 삭제 요청 즉시 레코드와 파일 제거 | 공간 즉시 회수 | 복구 불가, 현재 승인 범위 위반 |

권장값: `SOFT_DELETE_30D_EXPLICIT_PURGE`

추가 계약:

- 이번 결정 작업과 후속 일반 CRUD는 hard-delete를 승인하지 않는다.
- 30일은 `DeletedAtUtc` 기준이며 실제 purge 기능은 별도 승인 후 구현한다.
- 콘텐츠 soft delete는 자식 `MediaAsset`의 개별 삭제 상태를 자동 변경하지 않는다. UI의 유효 가시성만 부모 상태를 따른다.
- 콘텐츠 복구 시 자식의 기존 개별 삭제 상태를 보존한다.
- purge는 다른 활성 참조, 보존기간, 백업 정책, 경로 경계와 해시를 확인한 뒤 수행한다.
- purge 실패는 DB 레코드를 먼저 지우지 않고 재시도 가능한 상태로 남긴다.

### D6. 복구 전 무결성

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `VERIFY_AND_BLOCK_ON_MISMATCH` | 복구 전 경로 경계, 존재, 크기, SHA-256, 시그니처를 확인하고 불일치면 복구를 거부 | 깨진 READY 자산 노출 방지 | 대용량 파일 해시 비용 |
| `VERIFY_METADATA_ONLY` | 존재와 크기만 확인 | 빠름 | 바뀐 파일을 놓칠 수 있음 |
| `RESTORE_DB_FLAG_ONLY` | 현재처럼 DB 상태만 복구 | 단순함 | 누락·변조 파일도 정상처럼 보임 |

권장값: `VERIFY_AND_BLOCK_ON_MISMATCH`

불일치 시 파일을 자동 다운로드하거나 자동 교체하지 않는다. 레코드는 삭제 상태를 유지하고 사용자에게 `MISSING`, `HASH_MISMATCH`, `INVALID_SIGNATURE` 중 진단 결과를 반환한다. 해당 결과를 DB에 영속화하려면 별도 schema 결정이 필요하며, 최소 구현에서는 요청 결과와 진단 로그만 사용할 수 있다.

### D7. thumbnail과 파생 파일

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `REGENERABLE_THUMBNAIL_CACHE` | 원본은 권위 자산, thumbnail은 미디어 ID·원본 SHA·profile로 재생성 가능한 cache | DTO의 thumbnail 계약을 충족하면서 원본 소유권과 분리 | 이미지 decoder와 cache 정리 정책 필요 |
| `SERVE_ORIGINAL_AS_THUMBNAIL` | 원본을 thumbnail route로 전송 | 구현이 단순 | 큰 파일 전송, decoder·노출 위험, 성능 저하 |
| `DERIVATIVE_AS_MEDIA_ASSET` | 모든 파생물을 별도 DB 자산으로 관리 | 완전한 이력 | schema·수명주기·UI 복잡도 증가 |

권장값: `REGENERABLE_THUMBNAIL_CACHE`

추가 계약:

- 원본만 권위 백업 대상이다.
- cache는 `derived/thumbnails/{mediaId}/{sha256-prefix}-{profile}.webp`처럼 원본 버전과 profile이 드러나는 결정적 키를 쓴다.
- cache는 DB `MediaAsset`로 등록하지 않고 언제든 재생성할 수 있다.
- cache 누락은 원본 누락으로 판정하지 않는다.
- thumbnail route는 상대경로를 입력으로 받지 않고 media ID만 받으며, soft-deleted·비공개 자산의 접근 정책을 적용한다.
- 블로그용 편집 이미지와 내보내기 자산은 thumbnail cache가 아니며 Phase 1B 범위 밖이다.

### D8. DB·파일 reconciliation

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `REPORT_ONLY_NO_AUTO_REPAIR` | 명시적 진단에서 불일치를 보고하고 자동 삭제·수정하지 않음 | 데이터 손실 위험 최소 | 사용자 또는 후속 승인 필요 |
| `AUTO_REPAIR_SAFE_CASES` | 고아·오래된 PENDING을 자동 정리 | 운영 편의 | “안전한 경우” 오판 시 데이터 손실 |
| `NO_RECONCILIATION` | 별도 검사 없음 | 구현 비용 없음 | 불일치가 누적되고 발견이 늦음 |

권장값: `REPORT_ONLY_NO_AUTO_REPAIR`

최소 보고 항목:

- `READY_FILE_MISSING`
- `READY_SIZE_MISMATCH`
- `READY_HASH_MISMATCH`
- `STALE_PENDING`
- `FAILED_FILE_PRESENT`
- `ORPHAN_FINAL_FILE`
- `ORPHAN_TEMP_FILE`
- `RELATIVE_PATH_ESCAPE_OR_REPARSE`
- `DUPLICATE_ACTIVE_SHA_WITHIN_CONTENT`

애플리케이션 시작 시 전체 해시 스캔이나 자동 삭제를 실행하지 않는다. readiness는 루트 접근 가능성만 빠르게 확인하고, 전체 진단은 사용자가 명시적으로 실행하는 별도 maintenance로 둔다.

### D9. 백업·복원 단위

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `DB_ORIGINALS_MANIFEST_ONE_UNIT` | DB 백업, 권위 원본 트리, manifest를 같은 backup set으로 취급 | DB·파일 정합성 검증 가능 | 일관된 시점 확보 절차 필요 |
| `DB_ONLY` | DB만 백업 | 간단 | 파일 복원 불가 |
| `FILES_ONLY` | 파일만 백업 | 파일 보존 | 메타데이터·관계 복원 불가 |

권장값: `DB_ORIGINALS_MANIFEST_ONE_UNIT`

manifest 최소 항목:

- backup set ID와 생성 시각
- DB backup 식별자 또는 시점
- 각 권위 파일의 `MediaAssetId`, 상대경로, 크기, SHA-256
- 포함 파일 수, 누락·오류 수, 정책 버전

`temp`와 재생성 가능한 thumbnail cache는 권위 backup set에서 제외한다. 복원은 운영 위치에 바로 덮어쓰지 않고 격리된 DB·자료 루트에 복원하여 manifest를 검증한 뒤 전환한다. 실제 백업·복원 구현은 Phase 5 또는 별도 승인 범위다.

### D10. 외부 URL과 소유권

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `DEFER_EXTERNAL_URL_ENTIRELY` | Phase 1B의 `MediaAsset` 취득에서는 URL 입력·DB 저장·다운로드를 모두 제외 | 존재하지 않는 provenance를 가장하지 않고 네트워크·SSRF 범위를 분리 | 외부 URL 이미지는 Phase 1B에서 등록할 수 없음 |
| `REFERENCE_ONLY_NO_DOWNLOAD` | URL만 보존하고 다운로드하지 않음 | 네트워크 실행 없음 | 현재 `MediaAsset`에 URL·author·license·origin type을 연결할 schema가 없어 권장 불가 |
| `USER_CONFIRMED_IMPORT_WITH_PROVENANCE` | 사용자가 개별 승인한 URL만 다운로드하고 출처·라이선스를 미디어별 저장 | 로컬 보존 가능 | 네트워크·보안·schema·소유권 검토 필요 |
| `AUTOMATIC_DOWNLOAD` | 분석된 URL 이미지를 자동 저장 | 편의성 | SSRF, 악성 파일, 저작권, 과다 다운로드 위험 |

권장값: `DEFER_EXTERNAL_URL_ENTIRELY`

현재 `SourceEvidence.SourceReference`는 콘텐츠 수준 근거이며 `MediaAssetId` 관계가 없다. 따라서 일반 콘텐츠 본문 또는 `SourceEvidence`에 URL을 기록하는 기존 기능을 특정 `MediaAsset`의 취득 provenance로 해석하지 않는다. Phase 1B에서는 URL 입력, `MediaAsset`용 URL 저장, URL 이미지 다운로드가 모두 없다.

향후 외부 URL 취득을 승인하려면 최소한 미디어별 origin type, source URL, author, license 또는 user confirmation, captured time을 영속화하는 schema와 SSRF·redirect·DNS 재확인·private address 차단·크기·MIME·저작권 정책을 별도로 승인해야 한다. 외부 URL 다운로드 금지는 유지된다.

### D11. 정확한 SHA-256 중복

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `SAME_CONTENT_REUSE_CROSS_CONTENT_SEPARATE` | 같은 콘텐츠의 활성 원본은 기존 `MediaAsset`을 재사용하고, 다른 콘텐츠끼리는 파일·레코드를 공유하지 않음 | 중복 억제와 독립 수명주기의 균형 | 동시 업로드의 DB 수준 보장에는 migration 가능성 |
| `ALLOW_AND_FLAG` | 중복 저장 후 후보로 표시 | 구현 단순 | 공간 낭비와 삭제 혼란 |
| `GLOBAL_PHYSICAL_DEDUP` | 모든 콘텐츠가 하나의 물리 파일을 공유 | 공간 절약 | 참조계수·purge·권한·복구 복잡도 증가 |

권장값: `SAME_CONTENT_REUSE_CROSS_CONTENT_SEPARATE`

유사도 중복은 이 결정의 대상이 아니다. 같은 콘텐츠에서 검증된 임시 파일의 SHA-256이 기존 활성 `READY` 원본과 같으면 새 최종 파일과 레코드를 만들지 않고 기존 자산을 반환한다. 콘텐츠 간에는 같은 SHA라도 독립 관리 복사본을 유지한다.

현재 `(ContentId, Sha256)` 인덱스는 유일하지 않다. 동시 업로드까지 DB에서 강제하려면 filtered unique index 등 schema/migration 승인이 필요하다. migration을 승인하지 않으면 애플리케이션 transaction 검사만 가능하며 경쟁 상태까지 완전 보장한다고 주장할 수 없다.

### D12. 입력 형식·특수 형식·손상 검증

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `JPEG_PNG_WEBP_FULL_DECODE` | JPEG·PNG·WebP만 받고 확장자, 선언 MIME, magic bytes, 전체 image decode를 모두 통과시킴 | 현재 코드와 일반 브라우저 표시 범위에 맞고 손상·위장 파일을 줄임 | decoder 도입과 자원 제한 필요 |
| `CURRENT_HEADER_ONLY_JPEG_PNG_WEBP` | 현재 header parser만 유지 | 추가 의존성·구현이 적음 | 잘린 파일·후반부 손상을 놓칠 수 있음 |
| `EXPANDED_GIF_HEIC_SVG` | GIF·HEIC·SVG까지 한 번에 지원 | 입력 편의 | animation, 변환 품질, codec, active content 정책이 미결정 |

권장값:

```text
FORMAT_ALLOWLIST = JPEG_PNG_WEBP_ONLY
ALLOWED_EXTENSIONS = .jpg,.jpeg,.png,.webp
ALLOWED_MIME_TYPES = image/jpeg,image/png,image/webp
GIF_POLICY = DEFER
HEIC_POLICY = DEFER
SVG_POLICY = REJECT
VALIDATION = EXTENSION_MIME_MAGIC_BYTES_IMAGE_DECODE
CORRUPT_IMAGE_POLICY = REJECT_NO_MEDIA_ASSET
```

근거와 세부 계약:

- 현재 저장 코드는 `.jpg`, `.jpeg`, `.png`, `.webp`와 대응 MIME·magic bytes만 식별한다.
- JPEG, PNG, WebP는 현재 대상인 PC 브라우저에서 직접 표시하기 적합한 raster 형식이다.
- GIF는 animation frame, frame 수·재생시간·폭탄성 압축, 정적 thumbnail 처리 결정이 필요하므로 Phase 1B에서 지원하지 않는다. 첫 frame으로 자동 변환하지도 않는다.
- HEIC는 현재 parser·decoder·브라우저 직접 표시 계약이 없고 변환 시 색공간·orientation·metadata·품질 정책이 필요하므로 지원·변환을 모두 보류한다.
- SVG는 script, 외부 resource, 복잡도 제한 등 별도 active content 보안 정책이 필요하므로 Phase 1B에서 명시적으로 거부한다.
- magic bytes는 최소 header 일치이고, image decode는 decoder가 파일 끝까지 정상 해석하여 실제 raster를 만들 수 있음을 확인하는 별도 단계다. 둘 중 하나로 다른 하나를 대체하지 않는다.
- 손상, 잘림, 잘못된 chunk, decoder 오류, dimension 불일치는 임시 파일을 정리하고 `MediaAsset`을 만들지 않는 입력 오류다. 입력 원본은 변경하지 않는다.
- decoder는 D13 한도를 decode 전 가능한 시점에 검사하고, 한도를 확인할 수 없는 파일을 무제한 할당으로 decode하지 않는다.

### D13. 파일 용량·가로·세로·총 pixel 한도

| 값 | 최대 파일 | 최대 가로·세로 | 최대 총 pixel | 적합성·위험 |
|---|---:|---:|---:|---|
| `10_MIB_4096_AXIS_20MP` | 10 MiB | 각 4,096 px | 20,000,000 | memory 부담은 낮지만 고해상도 휴대폰 사진·긴 screenshot 거부 가능성이 큼 |
| `20_MIB_8192_AXIS_40MP` | 20 MiB | 각 8,192 px | 40,000,000 | 현재 설정을 유지하면서 일반 사진·screenshot과 decode memory 사이의 경계를 둠 |
| `50_MIB_12000_AXIS_80MP` | 50 MiB | 각 12,000 px | 80,000,000 | 현재 전체 memory 적재 구조와 로컬 동시 요청에서 과도한 peak memory 위험 |

권장값:

```text
MAX_FILE_BYTES = 20971520
MAX_FILE_SIZE_DISPLAY = 20_MIB
MAX_PIXEL_WIDTH = 8192
MAX_PIXEL_HEIGHT = 8192
MAX_TOTAL_PIXELS = 40000000
MAX_PIXEL_DIMENSION = 8192X8192_AND_40MP_TOTAL
```

20 MiB 유지 근거:

- 현재 `Storage.MaxImageBytes` 기본값과 설정값은 정확히 `20 * 1024 * 1024 = 20,971,520 bytes`이고 기존 오류·테스트 경계도 여기에 맞춰져 있다.
- 용도는 블로그 검토용 사진과 screenshot이며 50 MiB급 원본이나 80 MP급 편집 원본 보관소가 아니다.
- 파일 bytes만으로 decode memory를 통제할 수 없다. 압축된 20 MiB 파일도 매우 큰 raster가 될 수 있으므로 8,192 px 축 한도와 40,000,000 총 pixel 한도를 동시에 적용한다.
- 40 MP RGBA raster 하나는 단순 계산으로 약 160 MB의 pixel memory가 필요할 수 있다. 현재 `MemoryStream`과 `ToArray()` 복사까지 더해지므로 현 구현을 그대로 둔 채 동시 다중 decode를 허용해서는 안 된다.
- D4 구현에서는 제한 스트리밍과 임시 파일을 사용하고, decode 동시성을 제한해야 한다. 이 구조 개선 없이 20 MiB를 “안전하게 처리 가능”하다고 판정하지 않는다.

가로 또는 세로가 8,192 px를 넘거나 `width × height`가 40,000,000을 넘으면 거부한다. 0, 음수, 정수 overflow, 비정상 dimension도 손상 이미지로 거부한다.

### D14. EXIF·GPS·orientation·촬영일

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `STRIP_ALL_AT_INGEST` | 관리 복사본 생성 시 모든 metadata를 제거 | 관리 루트·backup의 개인정보 최소화 | 선택 원본과 byte-identical하지 않고 orientation·촬영일 처리 필요 |
| `PRIVATE_ORIGINAL_PRESERVE_PUBLIC_DERIVATIVE_STRIP` | 비공개 관리 원본은 metadata를 보존하고 thumbnail·공개 파생물은 제거 | 원본 충실성과 공개 privacy 경계를 함께 유지 | 관리 루트와 backup에는 GPS가 남을 수 있음 |
| `PRESERVE_ALL` | 원본과 모든 파생물의 metadata 보존 | 정보 손실 최소 | 공개 산출물에 GPS·기기 정보가 노출될 위험 |

권장값:

```text
EXIF_GPS_POLICY = PRIVATE_ORIGINAL_PRESERVE_PUBLIC_DERIVATIVE_STRIP
GPS_DATABASE_POLICY = DO_NOT_EXTRACT_OR_STORE
CAPTURE_DATE_POLICY = STORE_SOURCE_TIMESTAMP_ONLY_WHEN_OFFSET_VALID_OR_USER_CONFIRMED
ORIENTATION_POLICY = APPLY_TO_DERIVATIVE_PIXELS_BEFORE_METADATA_STRIP
```

세부 계약:

- 사용자가 선택한 로컬 원본과 비공개 관리 복사본의 bytes를 보존하므로 해당 파일과 D9 backup에는 EXIF·GPS가 남을 수 있음을 사용자에게 명시한다.
- 원본 파일을 직접 공개 응답이나 블로그 산출물로 사용하지 않는다.
- thumbnail과 향후 공개 파생물은 orientation을 pixel에 적용한 뒤 EXIF, GPS, embedded thumbnail, 기기 serial, user comment 등 metadata를 제거한다.
- GPS 좌표를 DB, 일반 UI, 일반 로그로 추출·복제하지 않는다.
- 유효한 `DateTimeOriginal`과 timezone offset이 함께 있으면 UTC로 변환하여 기존 `SourceTimestampMs` 후보로 사용할 수 있다.
- timezone offset이 없으면 자동으로 UTC를 추정하지 않고 사용자 확인 전 `SourceTimestampMs`를 확정하지 않는다.
- metadata parser 실패는 전체 image decode 실패와 구분한다. 권장 정책에서는 raster가 정상이고 metadata만 손상되었으면 metadata를 사용하지 않고 비공개 원본을 보존할 수 있으나, 공개 파생물 생성은 metadata 제거 성공을 확인해야 한다.

### D15. 원래 파일명 보존과 로그 노출

| 값 | 내용 | 장점 | 위험·비용 |
|---|---|---|---|
| `BASENAME_DB_IDS_ONLY_LOG` | 경로를 제거한 basename만 DB에 보존하고 일반 로그는 ID·오류 코드만 사용 | 사용자 식별성과 privacy 균형 | DB backup에는 원본 basename이 포함됨 |
| `HASH_DISPLAY_NAME` | 원본명을 저장하지 않고 hash 기반 표시명 사용 | 정보 노출 최소 | 사용자가 파일을 식별하기 어려움 |
| `FULL_PATH_DIAGNOSTIC_LOG` | 원본 절대경로와 저장경로를 로그에 기록 | 진단 편의 | 사용자명·폴더명·콘텐츠 정보 노출 |

권장값:

```text
ORIGINAL_FILENAME_POLICY = PRESERVE_BASENAME_DB_ONLY
ORIGINAL_FILENAME_DISPLAY = ESCAPED_BASENAME
LOG_MEDIA_DISCLOSURE = MEDIA_CONTENT_IDS_ERROR_CODES_ONLY
LOG_ABSOLUTE_PATH = NEVER
LOG_RELATIVE_PATH = DIAGNOSTIC_OPT_IN_REDACTED_ONLY
LOG_ORIGINAL_FILENAME = NEVER_BY_DEFAULT
```

세부 계약:

- `OriginalFileName`에는 source 절대경로나 디렉터리를 저장하지 않고 `Path.GetFileName`으로 분리·검증한 basename만 저장한다.
- 제어문자와 경로 separator를 거부하고, 화면에서는 HTML escape와 길이 제한을 적용한다. 화면 표시용 정리가 DB 원문을 덮어쓰지는 않는다.
- 실제 저장명은 GUID 기반이며 원본 basename을 물리 경로에 사용하지 않는다.
- 일반 성공·실패 로그는 `MediaAssetId`, `ContentId`, 안정적인 오류 코드, bytes·MIME 같은 비식별 진단값만 기록한다.
- framework 예외의 전체 메시지에 절대경로가 포함될 수 있으므로 일반 로그에 그대로 전달하지 않는다.
- 사용자가 별도로 승인한 로컬 진단에서만 루트 기준 상대경로를 제한적으로 표시할 수 있고, 절대 루트와 원본 source 경로는 표시하지 않는다.

## 5. Codex 권장 묶음

아래 값은 Codex 권고이며 아직 사용자가 선택한 값이 아니다.

```text
RECOMMENDATION_APPROVAL_STATUS = PENDING
D_MEDIA_TRACE = D-MEDIA-001..D-MEDIA-012_COMPLETE
D1_STORAGE_ROOT = PROJECT_LOCAL_EXPLICIT_INIT
D2_LOCAL_INGEST_OWNERSHIP = LOCAL_FILE_MANAGED_COPY
D3_RELATIVE_PATH_LAYOUT = CONTENT_MEDIA_ID_TREE
D4_WRITE_CONSISTENCY = TEMP_PENDING_ATOMIC_PROMOTE_READY
D5_DELETE_RETENTION = SOFT_DELETE_30D_EXPLICIT_PURGE
D6_RESTORE_INTEGRITY = VERIFY_AND_BLOCK_ON_MISMATCH
D7_DERIVATIVE_POLICY = REGENERABLE_THUMBNAIL_CACHE
D8_RECONCILIATION = REPORT_ONLY_NO_AUTO_REPAIR
D9_BACKUP_UNIT = DB_ORIGINALS_MANIFEST_ONE_UNIT
D10_EXTERNAL_URL = DEFER_EXTERNAL_URL_ENTIRELY
D11_EXACT_DUPLICATE = SAME_CONTENT_REUSE_CROSS_CONTENT_SEPARATE
D12_INPUT_FORMAT_VALIDATION = JPEG_PNG_WEBP_FULL_DECODE
FORMAT_ALLOWLIST = JPEG_PNG_WEBP_ONLY
ALLOWED_EXTENSIONS = .jpg,.jpeg,.png,.webp
ALLOWED_MIME_TYPES = image/jpeg,image/png,image/webp
GIF_POLICY = DEFER
HEIC_POLICY = DEFER
SVG_POLICY = REJECT
VALIDATION = EXTENSION_MIME_MAGIC_BYTES_IMAGE_DECODE
CORRUPT_IMAGE_POLICY = REJECT_NO_MEDIA_ASSET
D13_RESOURCE_LIMIT = 20_MIB_8192_AXIS_40MP
MAX_FILE_BYTES = 20971520
MAX_FILE_SIZE_DISPLAY = 20_MIB
MAX_PIXEL_WIDTH = 8192
MAX_PIXEL_HEIGHT = 8192
MAX_TOTAL_PIXELS = 40000000
MAX_PIXEL_DIMENSION = 8192X8192_AND_40MP_TOTAL
D14_METADATA_PRIVACY = PRIVATE_ORIGINAL_PRESERVE_PUBLIC_DERIVATIVE_STRIP
EXIF_GPS_POLICY = PRIVATE_ORIGINAL_PRESERVE_PUBLIC_DERIVATIVE_STRIP
GPS_DATABASE_POLICY = DO_NOT_EXTRACT_OR_STORE
CAPTURE_DATE_POLICY = STORE_SOURCE_TIMESTAMP_ONLY_WHEN_OFFSET_VALID_OR_USER_CONFIRMED
ORIENTATION_POLICY = APPLY_TO_DERIVATIVE_PIXELS_BEFORE_METADATA_STRIP
D15_FILENAME_LOGGING = BASENAME_DB_IDS_ONLY_LOG
ORIGINAL_FILENAME_POLICY = PRESERVE_BASENAME_DB_ONLY
ORIGINAL_FILENAME_DISPLAY = ESCAPED_BASENAME
LOG_MEDIA_DISCLOSURE = MEDIA_CONTENT_IDS_ERROR_CODES_ONLY
LOG_ABSOLUTE_PATH = NEVER
LOG_RELATIVE_PATH = DIAGNOSTIC_OPT_IN_REDACTED_ONLY
LOG_ORIGINAL_FILENAME = NEVER_BY_DEFAULT
```

이 묶음은 다음 원칙을 우선한다.

1. 사용자 원본을 건드리지 않는다.
2. DB와 관리 파일의 소유권 경계를 명확히 한다.
3. 정상 경로보다 실패·중단·복구 경로를 먼저 계약한다.
4. 자동 hard-delete와 자동 repair를 금지한다.
5. 외부 URL 취득 전체를 Phase 1B에서 제외하고 로컬 파일 import만 다룬다.
6. 원본은 권위 자산, thumbnail은 재생성 가능한 cache로 구분한다.
7. 콘텐츠별 파일 수명주기를 유지하고 전역 물리 dedup의 참조계수 복잡도를 피한다.
8. bytes 한도와 pixel 한도를 함께 적용하고 전체 image decode로 손상을 확인한다.
9. 비공개 원본 metadata 보존과 공개 파생물 metadata 제거를 분리한다.
10. DB에는 basename만, 일반 로그에는 ID와 오류 코드만 남긴다.

## 6. 실패 시나리오와 권장 처리

| 시나리오 | 권장 처리 | 자동 변경 |
|---|---|---|
| 루트 미구성·부재 | upload 비활성, readiness 실패, 명시적 초기화 안내 | 없음 |
| 루트가 reparse point이거나 경로 이탈 | 파일 생성 전 거부 | 없음 |
| GIF·HEIC 입력 | `FORMAT_DEFERRED`로 거부하고 변환·첫 frame 추출 없음 | 없음 |
| SVG 또는 허용목록 밖 형식 | `FORMAT_REJECTED`로 거부 | 없음 |
| 확장자·MIME·magic bytes 불일치 | 임시 파일 제거, DB 자산 미생성 | 임시 파일 제거만 |
| 전체 image decode 실패·손상 | `CORRUPT_IMAGE`로 거부, DB 자산 미생성 | 임시 파일 제거만 |
| 20 MiB, 축 8,192 px 또는 40 MP 한도 초과 | 해당 한도 오류로 거부 | 임시 파일 제거만 |
| 취소·디스크 부족 | 임시 파일 best-effort 제거, 오류 반환 | 임시 파일 제거만 |
| DB `PENDING` 저장 실패 | 임시 파일 제거 | 임시 파일 제거만 |
| 최종 승격 실패 | DB `FAILED`, 임시 파일 정리, 원인 보존 | 상태 기록과 임시 파일 제거 |
| DB `READY` 저장 실패 | 최종 파일 보상 제거, DB `FAILED`; 보상 실패도 별도 보고 | 승인된 보상만 |
| 프로세스 강제 종료 | 다음 명시적 reconciliation에서 `STALE_PENDING`·고아 파일 보고 | 자동 repair 없음 |
| `READY`인데 파일 누락·변조 | 조회·복구에서 정상으로 숨기지 않고 진단 | 자동 복원·다운로드 없음 |
| soft-deleted 자산 복구 시 파일 불일치 | 복구 거부, 삭제 상태 유지 | 없음 |
| 콘텐츠 분류 변경 | 파일 이동 없음, DB 상대경로 유지 | 없음 |
| DB와 파일 백업 시점 불일치 | backup set 실패, 정상 백업으로 표시하지 않음 | 없음 |
| 외부 URL이 미디어 입력으로 전달됨 | Phase 1B 범위 밖으로 거부하고 저장·다운로드하지 않음 | 없음 |
| 같은 콘텐츠의 동일 SHA | 기존 활성 자산 재사용, 새 임시 파일 제거 | 중복 임시 파일 제거만 |
| EXIF timezone 누락 | 촬영 시각 자동 확정 금지, 사용자 확인 전 null 유지 | 없음 |
| 공개 파생물 metadata 제거 실패 | 공개·내보내기 거부, 비공개 원본 유지 | 없음 |
| 예외 메시지에 절대경로·원본명이 포함됨 | 일반 로그에 원문을 쓰지 않고 안정 오류 코드로 치환 | 없음 |

## 7. 누락·상충 검사

| 검사 항목 | 결과 | 해소 방법 |
|---|---|---|
| 공통 문서의 범주 예시와 분류 변경 | 상충 없음 | 공통 문서는 `권장 구조:`이고 `media/cooking/...`은 상대경로 형태 예시다. D3은 분류를 DB에서 관리하고 `media/{contentId}/{mediaId}`를 사용 |
| 현재 `yyyy/MM` 직접 쓰기와 안전한 승격 | 상충 | D3·D4 승인 후 구현 교체 필요 |
| 현재 DB soft delete와 영구 파일 보존 | 정책 누락 | D5에서 보존기간과 별도 purge 승인 경계 정의 |
| 현재 복구와 파일 무결성 | 상충 | D6에서 불일치 시 복구 차단 |
| DTO thumbnail URL과 실제 endpoint 부재 | 계약 공백 | D7 승인 후 endpoint·cache 구현 필요 |
| v7의 유사도 중복과 현재 SHA 중복 | 범위 혼동 가능 | 유사도는 향후 분석, D11은 정확한 SHA만 담당 |
| 외부 출처와 `MediaAsset` provenance | schema 공백, 현재 범위에서 해소 | D10으로 URL 입력·저장·다운로드 전체를 보류 |
| 허용 확장자·MIME·magic bytes | 누락 없음 | D12에 정확한 allowlist와 교차검증값 명시 |
| GIF·HEIC·SVG | 누락 없음 | GIF·HEIC 보류, SVG 거부 |
| 손상 이미지·전체 decode | 기존 구현 공백 | D12에서 전체 decode 실패 시 자산 미생성 |
| bytes·가로·세로·총 pixel | 기존 pixel 제한 공백 | D13에서 20 MiB, 8,192 px 각 축, 40 MP 동시 한도 |
| EXIF·GPS·촬영일·orientation | 기존 정책 공백 | D14에서 비공개 원본과 공개 파생물 경계, GPS DB 미저장, 촬영일 조건 명시 |
| 원본명·경로 로그 | 기존 redaction 계약 공백 | D15에서 basename DB 보존, 일반 로그 ID·오류 코드 한정 |
| DB·파일 분산 transaction | 기술적 비원자성 | PENDING, 보상, 보고형 reconciliation로 명시 |
| content soft delete와 child media 상태 | 의미 충돌 가능 | 부모는 유효 가시성만 제어하고 자식 상태는 보존 |
| 파생 thumbnail과 권위 원본 | 소유권 공백 | 파생물은 재생성 cache, 원본만 권위 자산 |
| 동시 exact duplicate 방지 | DB 보장 공백 | 강한 보장 선택 시 migration 별도 승인 |
| backup과 restore | 구현 전 정책만 존재 | D9를 Phase 5 구현의 선행 계약으로 사용 |

검사 결과:

```text
D_MEDIA_REQUIREMENT_MISSING_COUNT = 0
FORMAT_SIZE_PIXEL_EXIF_GPS_POLICY_MISSING_COUNT = 0
NONEXISTENT_MEDIA_PROVENANCE_STORAGE_CLAIM_COUNT = 0
PATH_CATEGORY_POLICY_CONFLICT_COUNT = 0
```

결정 항목은 루트, 취득·소유권, 이름·경로, 형식·검증, 자원 한도, EXIF·GPS, 로그, 쓰기, 삭제, 복구, 파생물, reconciliation, 백업, 외부 URL, 중복을 모두 포함한다. 각 권고값 사이에 즉시 해결되지 않는 논리적 모순은 없다. 외부 URL 취득은 D10으로 전부 보류한다. D11의 DB 수준 동시성 보장 또는 향후 미디어 provenance 영속화를 선택하면 migration·네트워크 승인이 추가로 필요하므로 현재 작업을 자동 확장하지 않는다.

## 8. 사용자 선택 형식

아래는 권고 묶음 전체를 승인할 때의 사용자 응답 템플릿이다. 이 블록이 문서에 있다는 사실만으로 선택 완료 상태가 되지 않는다.

```text
MYDOCUMGM_PHASE1B_MEDIA_POLICY_SELECTION
ACCEPT_RECOMMENDED_BUNDLE = YES
D_MEDIA_TRACE = D-MEDIA-001..D-MEDIA-012_COMPLETE
D1_STORAGE_ROOT = PROJECT_LOCAL_EXPLICIT_INIT
D2_LOCAL_INGEST_OWNERSHIP = LOCAL_FILE_MANAGED_COPY
D3_RELATIVE_PATH_LAYOUT = CONTENT_MEDIA_ID_TREE
D4_WRITE_CONSISTENCY = TEMP_PENDING_ATOMIC_PROMOTE_READY
D5_DELETE_RETENTION = SOFT_DELETE_30D_EXPLICIT_PURGE
D6_RESTORE_INTEGRITY = VERIFY_AND_BLOCK_ON_MISMATCH
D7_DERIVATIVE_POLICY = REGENERABLE_THUMBNAIL_CACHE
D8_RECONCILIATION = REPORT_ONLY_NO_AUTO_REPAIR
D9_BACKUP_UNIT = DB_ORIGINALS_MANIFEST_ONE_UNIT
D10_EXTERNAL_URL = DEFER_EXTERNAL_URL_ENTIRELY
D11_EXACT_DUPLICATE = SAME_CONTENT_REUSE_CROSS_CONTENT_SEPARATE
D12_INPUT_FORMAT_VALIDATION = JPEG_PNG_WEBP_FULL_DECODE
FORMAT_ALLOWLIST = JPEG_PNG_WEBP_ONLY
ALLOWED_EXTENSIONS = .jpg,.jpeg,.png,.webp
ALLOWED_MIME_TYPES = image/jpeg,image/png,image/webp
GIF_POLICY = DEFER
HEIC_POLICY = DEFER
SVG_POLICY = REJECT
VALIDATION = EXTENSION_MIME_MAGIC_BYTES_IMAGE_DECODE
CORRUPT_IMAGE_POLICY = REJECT_NO_MEDIA_ASSET
D13_RESOURCE_LIMIT = 20_MIB_8192_AXIS_40MP
MAX_FILE_BYTES = 20971520
MAX_FILE_SIZE_DISPLAY = 20_MIB
MAX_PIXEL_WIDTH = 8192
MAX_PIXEL_HEIGHT = 8192
MAX_TOTAL_PIXELS = 40000000
MAX_PIXEL_DIMENSION = 8192X8192_AND_40MP_TOTAL
D14_METADATA_PRIVACY = PRIVATE_ORIGINAL_PRESERVE_PUBLIC_DERIVATIVE_STRIP
EXIF_GPS_POLICY = PRIVATE_ORIGINAL_PRESERVE_PUBLIC_DERIVATIVE_STRIP
GPS_DATABASE_POLICY = DO_NOT_EXTRACT_OR_STORE
CAPTURE_DATE_POLICY = STORE_SOURCE_TIMESTAMP_ONLY_WHEN_OFFSET_VALID_OR_USER_CONFIRMED
ORIENTATION_POLICY = APPLY_TO_DERIVATIVE_PIXELS_BEFORE_METADATA_STRIP
D15_FILENAME_LOGGING = BASENAME_DB_IDS_ONLY_LOG
ORIGINAL_FILENAME_POLICY = PRESERVE_BASENAME_DB_ONLY
ORIGINAL_FILENAME_DISPLAY = ESCAPED_BASENAME
LOG_MEDIA_DISCLOSURE = MEDIA_CONTENT_IDS_ERROR_CODES_ONLY
LOG_ABSOLUTE_PATH = NEVER
LOG_RELATIVE_PATH = DIAGNOSTIC_OPT_IN_REDACTED_ONLY
LOG_ORIGINAL_FILENAME = NEVER_BY_DEFAULT
```

일부 값을 바꾸려면 위 전체 블록을 복사하여 `ACCEPT_RECOMMENDED_BUNDLE = NO`로 바꾸고 변경할 값을 명시한다. 생략된 값은 승인된 것으로 추정하지 않으며 다시 `USER_SELECTION = REQUIRED`로 유지한다.

선택 뒤에도 다음은 자동 승인되지 않는다.

- 실제 자료 루트 생성 또는 이미지 쓰기
- 코드·DB·migration 변경
- hard-delete 또는 purge 실행
- 외부 URL 다운로드·네트워크 접근
- 실제 사용자 이미지 사용
- secret 또는 인증정보 사용
- 실제 백업·복원 실행

## 9. 종료 상태

```text
FINAL_DECISION = HOLD_PHASE1B_MEDIA_POLICY_USER_SELECTION_REQUIRED
PHASE1B_MEDIA_POLICY = NOT_IMPLEMENTED
PHASE1B_MEDIA_POLICY_RECOMMENDATION = PREPARED
USER_SELECTION = REQUIRED
```
