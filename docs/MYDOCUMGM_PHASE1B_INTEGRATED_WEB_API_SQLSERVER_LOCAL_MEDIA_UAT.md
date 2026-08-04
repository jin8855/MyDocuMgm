# 작업명

`MYDOCUMGM_PHASE1B_INTEGRATED_WEB_API_SQLSERVER_LOCAL_MEDIA_UAT`

# 현재 판정과 목적

```text
PASS_MYDOCUMGM_PHASE1B_LOCAL_MEDIA_STORAGE_CORE
Blocking = 0
Major = 0
Minor = 0

USER_VISUAL_ACCEPTANCE = ACCEPT
INTEGRATED_WEB_API_DB_UAT = HOLD_TEST_ENVIRONMENT_REQUIRED
PRODUCTION_READY = NO
SCHEMA_MIGRATION = NOT_AUTHORIZED
DEPLOYMENT = NOT_AUTHORIZED
COMMIT_PUSH = NOT_AUTHORIZED
```

이번 배치는 승인·검증된 Phase 1B 로컬 미디어 저장 코어를 다시 구현하지 않는다.

고유한 폐기형 SQL Server UAT DB, 격리된 OS TEMP 미디어 루트, 합성 이미지, loopback Web/API만 사용해 다음 실제 연결을 검증한다.

```text
Web 브라우저 조작
→ 실제 HTTP API
→ 실제 EF Core/SQL Server 영속화
→ 실제 LocalMediaStorage/TEMP 파일
→ API 재시작 후 재조회·다운로드·복구
```

현재 제품에 실제 Web 미디어 연결 경로가 없다면 이번 UAT에서 새로 구현하지 않는다. 정확한 공백을 증명하고 `HOLD_REAL_WEB_MEDIA_PATH_NOT_IMPLEMENTED`로 종료한다.

# 위험도와 검증 예산

```text
RISK_TIER = T3_HIGH
```

위험 요인:

- 실제 SQL Server에 폐기형 DB를 생성·삭제함
- HTTP 업로드와 DB·파일 시스템의 분산 수명주기를 실행함
- 파일·DB·브라우저 상태가 서로 다르게 성공할 수 있음
- cleanup 대상을 잘못 식별하면 unrelated DB 또는 파일을 손상시킬 수 있음

필수 Gate:

- 시작 Git·보호 파일·dirty 기준선
- 폐기형 DB와 TEMP root의 고유 identity 및 사전 부재 확인
- 권위 SQL 자산의 SHA-256과 schema 수량 검증
- Web/API/DB/파일을 관통하는 실제 runtime·browser 증거
- 성공·거부·재시작·삭제·복구 경로 검증
- exact cleanup 대상과 종료 후 부재 확인
- 위험 주장에 대한 읽기 전용 독립 검토 1회

의도적으로 생략:

- 운영·기존 `MyDocuMgm` DB UAT: 실제 사용자 DB 접근 금지
- 실제 `data\MyDocuMgmData` UAT: 실제 자료 루트 접근 금지
- schema/migration 소스 수정: 이번 배치는 기존 승인 schema 실행만 허용
- 제품 코드 수리: 결함이 확인되면 별도 Repair 지시 대상으로 남김
- UI 재디자인·시각 승인 재실행: 이번 배치에서 UI를 변경하지 않음
- Repository Runner 신규 profile·증거 문서 생성: 실제 DB/API/브라우저 교차 증거가 핵심이며 저장소 산출물을 늘리지 않음. 기존 승인 profile이 이미 있으면 읽기 전용 preflight에만 재사용 가능
- commit/push/deploy

# 이번 배치의 제한적 실행 승인

다음만 승인한다.

```text
LOCAL_SQL_SERVER_METADATA_AND_DISPOSABLE_UAT_DB = AUTHORIZED
DISPOSABLE_UAT_DB_CREATE = AUTHORIZED
APPROVED_PHASE1A_SCHEMA_APPLY_TO_DISPOSABLE_UAT_DB = AUTHORIZED
DISPOSABLE_UAT_DB_DROP = AUTHORIZED_AFTER_EXACT_IDENTITY_CHECK
ISOLATED_TEMP_MEDIA_ROOT_CREATE_AND_DELETE = AUTHORIZED
LOOPBACK_WEB_API_RUNTIME = AUTHORIZED
SYNTHETIC_MEDIA_ONLY = REQUIRED
```

다음은 계속 승인하지 않는다.

```text
EXISTING_MYDOCUMGM_DB_ACCESS = NOT_AUTHORIZED
EXISTING_USER_DB_READ_WRITE = NOT_AUTHORIZED
ACTUAL_DATA_MYDOCUMGMDATA_ACCESS = NOT_AUTHORIZED
SCHEMA_MIGRATION_SOURCE_CHANGE = NOT_AUTHORIZED
PRODUCT_CODE_CHANGE = NOT_AUTHORIZED
EXTERNAL_NETWORK = NOT_AUTHORIZED
PRODUCTION_CONNECTION = NOT_AUTHORIZED
DEPLOYMENT = NOT_AUTHORIZED
COMMIT_PUSH = NOT_AUTHORIZED
```

서버의 `master` 또는 동등한 시스템 DB에는 이번 고유 UAT DB의 생성·존재 확인·삭제에 필요한 최소 metadata/DDL만 허용한다. 기존 `MyDocuMgm` DB에 연결하거나 query를 실행하지 않는다.

# 시작 기준

```text
PROJECT_ROOT = C:\EtcProject\MyDocuMgm
EXPECTED_BRANCH = main
EXPECTED_HEAD = 3388081b8c71b9bfefdc1e3c08cc02fbf3942c28
EXPECTED_STAGED_CHANGES = 0
EXPECTED_DIRTY_PATH_COUNT = 35
```

승인된 35개 dirty 경로에는 다음 governing instruction이 포함된다.

```text
docs/MYDOCUMGM_PHASE1B_LOCAL_MEDIA_STORAGE_CORE_IMPLEMENTATION.md
```

직전 PASS에서 확인된 핵심 파일 identity:

```text
src/MyDocuMgm.Infrastructure/Storage/LocalMediaStorage.cs
SHA256 = EB9791FE274935546935D2012BC0E41DC565F874F72534058EB88A03127501AE

tests/MyDocuMgm.UnitTests/LocalMediaStorageTests.cs
SHA256 = 241F52E0667413B22D7C2F2C01015409ADA83AB7217B626A9E0AAFD68FB9780B
```

보호 와이어프레임 시작·종료 identity:

| 파일 | Bytes | SHA-256 |
|---|---:|---|
| V1 | 26,678 | `54C25E3A95364A4F98361C21DC32EE450CF96C21FB871C7BFFD7ACB4FA9680B8` |
| V2 | 30,260 | `C9381FBC3B4DABA7C45D6C288C0CF03392A91C3C154DFA4889840F9B0A62302F` |
| V4 | 38,169 | `71E8294D26A71EDD130BD6DDBEC182C4E352484049E2584F40AF7EE2B25A7BEF` |
| V5 | 42,937 | `EFBC21070C8CC609226DC00C865358E9DED09BC2456C56A6FAA401F61455D998` |
| V6 | 50,367 | `41A17A3600A095BFA9F1BEE2C8213D4F00687572DEF5306DFC0F15E0D3D96424` |
| V7 | 54,387 | `9A686ECE38520DFCD0D116117A9F582E1EF53FEBFA2DDFDDA201E7DCD48AB0A1` |

Governing instruction SHA-256:

```text
F982F57A3A4E16D6A41D78610BC3AD9A21E4D8CFC6E8386702237813098871C4
```

시작 시 읽기 전용으로 다음을 확인한다.

1. branch, HEAD, staged, `git status --short`
2. 직전 종료의 승인된 35개 dirty exact path set과 현재 path set 일치
3. 위 두 핵심 파일 SHA-256 일치
4. 보호 와이어프레임 byte/SHA-256 일치
5. 포트 `5173`, `5080` 및 관련 dotnet/Vite/Edge listener 부재
6. 저장소 내부 신규 UAT DB 파일, 미디어 파일, 로그, 브라우저 profile 부재

35라는 개수만 맞추지 않는다. 승인 경로가 빠지고 다른 경로가 추가됐으면 HOLD한다. 파일을 삭제·이동·되돌리거나 staging하지 않는다.

# ALREADY_DONE — 재작업 금지

다음은 새로운 반대 증거가 없는 한 보호한다.

- JPEG/PNG/WebP만 허용하고 GIF/HEIC는 보류, SVG는 거부
- 확장자 → MIME → magic/container → ImageSharp full decode
- 고정 상한 `20,971,520 bytes`, `8192×8192`, 총 `40,000,000 pixels`
- 설정으로 승인 상한 확대 불가
- `media/{contentId}/{mediaId}/original/{guid}.{ext}` 경로
- 고유 `.part` → `PENDING` → 동일 볼륨 atomic move → `READY`
- orientation 적용·EXIF/GPS 제거 WebP thumbnail cache
- 같은 콘텐츠의 동일 SHA-256 READY 재사용, 콘텐츠 간 별도 복사
- soft delete와 무결성 불일치 복구 차단
- 외부 URL 입력·저장·다운로드 제외
- readiness 취소·thumbnail 실패의 `.part` 정리
- 단위 71/71, 통합 10/10과 집중 media/compensation 38/38 PASS
- 사용자 승인된 데스크톱·모바일 UI

이번 UAT가 위 계약과 다른 동작을 실제로 관찰하면 `REOPENED` 근거로 기록하되 즉석 수리하지 않는다.

# 권위 schema 자산

다음 기존 자산을 읽기 전용으로 확인한다.

```text
artifacts/phase1a/sql/MyDocuMgm.InitialPhase1LocalContent.sql
artifacts/phase1a/sql/MyDocuMgm.VerifyPhase1.sql
```

기존 승인 SHA-256:

```text
MyDocuMgm.InitialPhase1LocalContent.sql
871ED9849BF7FADCC8443740BB92495E359CDD803F2F3A06EA02AAB3BCF202A6

MyDocuMgm.VerifyPhase1.sql
C1F02ACCC32C3110A0779E4DA6337CCE2BBE93FD042727B93403909BC6A708AD
```

현재 저장소의 권위 기록이 이보다 최신 SHA를 명시적으로 승인한 경우 최신 기록을 우선하고, 그 근거와 SHA를 보고한다. 추정으로 바꾸지 않는다.

적용 SQL이 `CREATE DATABASE`, `DROP DATABASE`, 다른 사용자 DB 참조를 포함하지 않는지 다시 검사한다. schema asset이 없거나 identity가 충돌하면 실행하지 않고 HOLD한다.

# 폐기형 환경 identity와 안전 조건

실행별 고유 값을 먼저 하나 확정하고 완료 보고까지 바꾸지 않는다.

```text
UAT_RUN_ID = <UTC timestamp>-<random 8 hex>
UAT_DB_NAME = MyDocuMgm_UAT_<yyyyMMddHHmmss>_<8hex>
UAT_MEDIA_ROOT = %TEMP%\MyDocuMgm\IntegratedUat_<same run id>\media-root
UAT_EVIDENCE_ROOT = %TEMP%\MyDocuMgm\IntegratedUat_<same run id>\evidence
```

DB 생성 전:

- 이름이 정규식 `^MyDocuMgm_UAT_[0-9]{14}_[0-9A-Fa-f]{8}$`와 일치
- 같은 이름의 DB가 사전에 존재하지 않음
- connection target database가 `MyDocuMgm`, 운영 DB, 사용자 DB가 아님
- 서버 instance와 인증 방식은 기존 로컬 개발 구성에서 읽되 secret을 출력하지 않음

TEMP 생성 전:

- `Path.GetFullPath` 결과가 OS TEMP 아래 이번 `UAT_RUN_ID` 디렉터리임
- `data\MyDocuMgmData`, 저장소, 사용자 문서·사진 폴더가 아님
- 해당 exact run root가 사전에 존재하지 않음

하나라도 만족하지 않으면 생성·삭제하지 않고 HOLD한다.

# 실행 순서

## 1. 환경 capability 확인

제품 코드를 수정하지 않고 다음을 확인한다.

- 로컬 SQL Server instance 접근 가능
- 폐기형 DB create/drop 권한
- `sqlcmd` 또는 저장소가 이미 사용하는 동등한 로컬 도구
- .NET SDK와 기존 restore 자산
- Edge/Playwright 등 기존 브라우저 검증 수단
- Web 미디어 화면이 실제 API를 호출할 수 있는 제품 경로인지, 아니면 mock/fixture 전용인지

Web이 mock/fixture 전용이면 DB를 만들기 전에 가능한 한 빨리 중단한다. 정확한 파일·symbol·network 관찰로 공백을 보고하고 `HOLD_REAL_WEB_MEDIA_PATH_NOT_IMPLEMENTED`를 사용한다.

## 2. 폐기형 DB와 TEMP root 준비

Web 실제 경로가 존재할 때만:

1. 고유 UAT DB를 생성한다.
2. connection database가 생성한 exact UAT DB인지 다시 조회한다.
3. 기존 승인 Phase 1A SQL을 해당 UAT DB에만 적용한다.
4. 검증 SQL과 catalog 조회로 예상 schema를 확인한다.
5. 기준 기대값 `21 tables / 40 indexes / 20 foreign keys`와 현재 권위 기록을 대조한다.
6. 고유 TEMP media root를 만든다.

기대 수량이 다르면 SQL이나 모델을 고치지 않는다. 최신 권위 기록으로 설명되지 않으면 `HOLD_APPROVED_SCHEMA_BASELINE_MISMATCH`로 종료한다.

## 3. runtime 구성

저장소 파일을 수정하지 않고 process environment 또는 TEMP 전용 runtime configuration으로만 다음을 주입한다.

- UAT DB connection string
- UAT media root의 정규화된 절대경로
- Web의 loopback API base URL
- UAT임을 구분하는 환경값

기존 `appsettings*.json`, `.local` 파일, source, launch profile을 수정하지 않는다. 실제 connection string과 secret은 보고서·로그·스크린샷에 노출하지 않는다.

API와 Web은 loopback에서만 시작한다. `5173`, `5080`이 비어 있으면 기존 계약 포트를 사용할 수 있고, 충돌 시 동적 loopback 포트를 쓰되 실제 값을 보고한다.

## 4. 합성 UAT 데이터

모든 콘텐츠·미디어·이미지는 이번 실행에서 생성한 synthetic fixture만 사용한다.

- 고유 UAT 콘텐츠 2개
- 정상 JPEG 1개
- 정상 PNG 1개
- 정상 WebP 1개
- 동일 SHA 중복 검증용 복사본
- truncated 또는 확장자/MIME/magic 불일치 1개 이상
- SVG 또는 외부 URL 입력 1개 이상
- 승인 고정 상한 검증용 초과 입력 1개

사용자 사진, 다운로드 폴더, 실제 URL, 외부 네트워크를 사용하지 않는다.

## 5. 실제 Web → API → DB → 파일 UAT

브라우저에서 현재 제품의 실제 미디어 조작 경로를 사용해 다음을 검증한다.

### 성공 경로

1. 콘텐츠 생성 또는 UAT 콘텐츠 진입
2. JPEG/PNG/WebP 각각 업로드
3. 목록과 선택 편집 영역에 실제 thumbnail 표시
4. 설명·정렬·공개 허용 등 현재 노출된 메타데이터 저장 후 재조회
5. API 응답의 media ID와 DB `MediaAsset` row 일치
6. DB 상대경로와 TEMP root 실제 원본 파일 일치
7. DB 상태가 최종 `READY`이며 남은 `.part` 0
8. thumbnail이 WebP이고 orientation 반영·EXIF/GPS 제거

### 중복·경계

- 같은 콘텐츠에서 동일 SHA 재업로드 시 기존 READY 자산 재사용
- 다른 콘텐츠에 같은 SHA 업로드 시 별도 DB row와 별도 실제 파일
- 상대경로가 승인 root 아래이며 내부 절대경로가 UI/API 오류에 노출되지 않음

### 거부 경로

- truncated 또는 확장자/MIME/magic 불일치
- SVG 또는 외부 URL 입력
- 설정값을 크게 주어도 `20,971,520 bytes / 8192×8192 / 40,000,000 pixels` 고정 상한 유지
- 거부 후 성공 row, 최종 파일, `.part`, thumbnail orphan이 0

### 삭제·복구

- soft delete 후 일반 목록·다운로드·thumbnail 접근 정책 확인
- 원본 파일은 즉시 hard delete되지 않음
- 정상 파일은 복구 가능
- TEMP root 안에서 삭제 상태 자산의 원본을 이번 UAT가 의도적으로 변조한 뒤 복구가 차단됨
- 변조 파일을 자동 교체·다운로드·정상화하지 않음

변조는 이번 실행이 만든 exact synthetic UAT 파일에만 허용한다.

## 6. 재시작 영속성

1. Web/API를 정상 종료한다.
2. 동일 UAT DB와 TEMP root로 API/Web을 재시작한다.
3. 앞서 저장한 READY 자산을 다시 조회한다.
4. DB row, 원본 파일, thumbnail 또는 재생성 결과가 일치하는지 확인한다.
5. soft-delete·복구 상태가 재시작 전후 일치하는지 확인한다.

in-memory repository 또는 새 fixture로 바뀐 결과를 영속성 PASS로 인정하지 않는다.

## 7. 교차 증거

핵심 시나리오별로 다음 네 층을 같은 media/content ID에 연결한다.

| 층 | 필수 증거 |
|---|---|
| Browser | 사용자 조작, 화면 결과, 실제 network request |
| API | HTTP method/path/status와 외부 노출 가능한 응답 요약 |
| DB | exact UAT DB의 row ID, status, relative path, SHA, soft-delete state |
| File | exact TEMP root의 원본/thumbnail 존재, 크기, SHA, `.part` 부재 |

브라우저 screenshot 존재만으로 DB·파일 연결을 증명하지 않는다. DB row만으로 실제 HTTP/브라우저 경로를 증명하지 않는다.

# 허용 산출물

저장소 내부 제품·테스트·문서 파일 변경은 0이어야 한다.

UAT 로그·스크린샷·합성 이미지·SQL 출력은 다음 저장소 밖 exact 경로에만 둔다.

```text
%TEMP%\MyDocuMgm\IntegratedUat_<UAT_RUN_ID>\evidence
```

최소 증거:

- 환경 identity와 redacted endpoint/DB name
- actual browser network 요약
- 성공 업로드·실제 thumbnail 화면
- soft delete·복구 또는 복구 차단 화면
- UAT DB 검증 query 출력
- TEMP 파일 manifest
- API/Web 시작·재시작·종료 결과
- cleanup 전후 exact target 존재 여부

connection string, password, token, 사용자 경로 정보는 저장하지 않는다.

# 정적·회귀 검증

제품 코드는 바꾸지 않지만 UAT 전후에 기존 기준이 깨지지 않았는지 확인한다.

```text
dotnet build MyDocuMgm.slnx --no-restore
dotnet test MyDocuMgm.slnx --no-restore --no-build
npm.cmd run check
npm.cmd test
npm.cmd run build
git diff --check
```

Web 명령은 `src\MyDocuMgm.Web` 또는 저장소가 정의한 실제 package root에서 실행한다. dependency·lockfile 변경이 없고 restore 자산이 충분하면 외부 restore를 반복하지 않는다.

# 독립 읽기 전용 검토

UAT와 cleanup 후 별도 읽기 전용 검토 1회를 수행한다.

검토 질문:

- 실제 Web network가 API로 갔는가, mock 응답을 실제 통합으로 오인하지 않았는가
- API가 exact UAT DB와 exact TEMP root를 사용했는가
- 기존 `MyDocuMgm` DB·실제 자료 root 접근 증거가 없는가
- 같은 ID가 browser/API/DB/file 네 층에서 연결되는가
- 재시작 뒤 결과가 in-memory가 아닌 실제 영속 상태인가
- 거부·삭제·복구 경로에서 orphan 또는 잘못된 READY가 없는가
- cleanup이 이번 실행 target만 대상으로 했는가
- 제품 코드·schema source·migration·UI가 변경되지 않았는가

독립 검토는 제품 코드를 수정하지 않는다. 결함이 확인되면 증거와 최소 Repair 범위만 보고한다.

# 안전한 종료와 cleanup

cleanup 순서:

1. Edge/브라우저, Vite, API test host를 정상 종료한다.
2. UAT DB의 이름, 생성 시각과 이번 `UAT_RUN_ID` 기록을 다시 대조한다.
3. 이름 정규식과 exact identity가 모두 맞고 이번 실행이 생성한 DB일 때만 해당 DB 하나를 drop한다.
4. TEMP run root의 full path가 OS TEMP 아래이며 이번 `UAT_RUN_ID`와 일치할 때만 해당 run root를 제거한다.
5. 포트/listener, DB 존재, TEMP root 존재, 저장소 residue를 재확인한다.

DB identity 또는 TEMP 경계가 불명확하면 삭제하지 말고 `HOLD_UAT_CLEANUP_IDENTITY_UNCERTAIN`으로 종료한다. unrelated session을 강제 종료하거나 광범위 경로를 삭제하지 않는다.

최종 상태:

- UAT DB 없음
- UAT TEMP run root 없음
- 5173/5080 또는 사용한 동적 포트 listener 없음
- 저장소 내부 신규 UAT artifact 없음
- 시작·종료 branch/HEAD/staged/dirty exact path set 동일
- 제품 파일 변경 0

# 금지 작업

- 기존 `MyDocuMgm` DB 연결·query·변경·삭제
- 다른 기존 DB의 조회·변경·삭제
- `data\MyDocuMgmData` 접근·생성·수정
- 사용자 사진·문서·다운로드 파일 사용
- 실제 외부 URL 또는 외부 network 요청
- schema/migration/source/test/UI 코드 수정
- 새 endpoint·payload·Web 연결을 이번 UAT에서 즉석 구현
- appsettings, `.local`, launch profile, csproj, package, lockfile 변경
- hard-delete·purge 기능 실행 또는 구현
- 승인된 35개 dirty 경로 수정·삭제·되돌리기
- `git reset`, `checkout`, `clean`, `stash`
- Git add/commit/amend/push
- deploy 또는 production 연결
- mock/in-memory 결과를 Web–API–SQL Server–파일 UAT로 표현
- UAT PASS를 production readiness로 승격
- 이 지시서를 저장소 안의 새 문서로 생성

# 중단 조건

다음 중 하나면 제품 코드를 변경하지 않고 해당 HOLD로 종료한다.

| 조건 | 판정 |
|---|---|
| Git/보호 identity/35개 path set 불일치 | `HOLD_BASELINE_RECONCILIATION_REQUIRED` |
| 로컬 SQL Server 또는 create/drop 권한 없음 | `HOLD_DISPOSABLE_SQLSERVER_ENVIRONMENT_REQUIRED` |
| 승인 schema SQL 부재·identity 충돌 | `HOLD_APPROVED_SCHEMA_ASSET_UNAVAILABLE` |
| 승인 schema 수량 불일치 | `HOLD_APPROVED_SCHEMA_BASELINE_MISMATCH` |
| Web 미디어 경로가 mock/fixture 전용 | `HOLD_REAL_WEB_MEDIA_PATH_NOT_IMPLEMENTED` |
| API가 process/TEMP 설정으로 UAT DB·root를 지정할 수 없음 | `HOLD_ISOLATED_RUNTIME_CONFIGURATION_REQUIRED` |
| cleanup identity 불명확 또는 residue 존재 | `HOLD_UAT_CLEANUP_IDENTITY_UNCERTAIN` |
| 제품 결함이 실제 통합 경로에서 재현됨 | `REPAIR_REQUIRED_MYDOCUMGM_PHASE1B_INTEGRATED_UAT` |
| 금지 범위 침범 | `REJECT_SCOPE_VIOLATION` |

# 완료 판정

정확히 하나만 사용한다.

```text
PASS_MYDOCUMGM_PHASE1B_INTEGRATED_WEB_API_SQLSERVER_LOCAL_MEDIA_UAT
REPAIR_REQUIRED_MYDOCUMGM_PHASE1B_INTEGRATED_UAT
HOLD_BASELINE_RECONCILIATION_REQUIRED
HOLD_DISPOSABLE_SQLSERVER_ENVIRONMENT_REQUIRED
HOLD_APPROVED_SCHEMA_ASSET_UNAVAILABLE
HOLD_APPROVED_SCHEMA_BASELINE_MISMATCH
HOLD_REAL_WEB_MEDIA_PATH_NOT_IMPLEMENTED
HOLD_ISOLATED_RUNTIME_CONFIGURATION_REQUIRED
HOLD_UAT_CLEANUP_IDENTITY_UNCERTAIN
REJECT_SCOPE_VIOLATION
```

PASS 조건:

- 실제 browser → HTTP API → SQL Server → TEMP LocalMediaStorage 연결 입증
- JPEG/PNG/WebP 성공과 thumbnail 표시
- 같은 콘텐츠 재사용·콘텐츠 간 별도 복사 입증
- 고정 상한·위장/손상·SVG/외부 URL 거부와 orphan 0
- soft delete, 정상 복구, 무결성 불일치 복구 차단 입증
- API 재시작 후 실제 DB·파일 영속성 입증
- DB/API/file/browser ID 추적 일치
- 기존 DB·실제 자료 root·사용자 파일·외부 network 접근 0
- source/schema/migration/dependency/UI 변경 0
- build/test/check 통과
- 독립 검토 Blocking/Major 0
- exact UAT DB와 TEMP root cleanup 완료
- 시작·종료 Git 기준선 동일, staged/commit/push/deploy 0

PASS하더라도 다음 상태만 승격한다.

```text
USER_VISUAL_ACCEPTANCE = ACCEPT
INTEGRATED_WEB_API_DB_UAT = PASS_DISPOSABLE_LOCAL_ENVIRONMENT
PRODUCTION_READY = NO
PRODUCTION_DATA_UAT = NOT_AUTHORIZED
SCHEMA_MIGRATION_SOURCE_CHANGE = NOT_AUTHORIZED
DEPLOYMENT = NOT_AUTHORIZED
COMMIT_PUSH = NOT_AUTHORIZED
```

# 완료 보고

1. 최종 판정과 Blocking/Major/Minor
2. 위험 등급, 적용 Gate, 생략 Gate
3. 시작·종료 branch, HEAD, staged, dirty exact path set
4. 핵심 파일·보호 와이어프레임 SHA-256 보존
5. UAT run ID, redacted SQL instance, exact disposable DB name, TEMP root
6. Web 실제 API 경로와 mock/fixture 배제 근거
7. browser/API/DB/file claim-to-evidence 표
8. JPEG/PNG/WebP와 thumbnail 결과
9. 중복, 고정 상한, 손상·위장·SVG·외부 URL 결과
10. soft delete·복구·무결성 불일치 차단 결과
11. API 재시작 영속성 결과
12. build/test/check 명령과 exit code
13. 독립 읽기 전용 검토 결과
14. 기존 DB·실제 자료 root·사용자 파일·외부 network 접근 0
15. 제품 코드·schema/migration/dependency/UI 변경 0
16. exact DB/TEMP/process/listener cleanup과 종료 후 존재 여부
17. Git add/commit/push/deploy 0
18. 남은 비승인 상태와 다음 한 단계
