# 작업명

`MYDOCUMGM_PHASE1B_LOCAL_MEDIA_STORAGE_CORE_IMPLEMENTATION`

# 확정 상태

```text
PASS_MYDOCUMGM_MOBILE_VISUAL_REPAIR
USER_VISUAL_ACCEPTANCE = ACCEPT
MOBILE_VISUAL = ACCEPT
Blocking = 0
Major = 0
Minor = 0

INTEGRATED_WEB_API_DB_UAT = HOLD_TEST_ENVIRONMENT_REQUIRED
PRODUCTION_READY = NO
SCHEMA_MIGRATION = NOT_AUTHORIZED
DEPLOYMENT = NOT_AUTHORIZED
COMMIT_PUSH = NOT_AUTHORIZED
```

첨부된 실제 `390×844` viewport 증거에서 다음을 사용자 시각 승인했다.

- 4단계가 모바일 단계 영역 중앙에 노출됨
- 1·4·7단계 이동 검증 결과가 보고됨
- 정렬·페이지 크기·첫 카드와 하단 작업 바가 겹치지 않음
- 모바일 카드 2열과 선택 이미지 편집 영역 유지
- 페이지 전체 가로 overflow 없음
- 데스크톱 7단계 표시와 기존 작업 바 배치 유지

하단 `이전` 버튼의 긴 목적지 문구가 말줄임되는 것은 버튼 기능·방향·조작성을 해치지 않으므로 이번 범위의 결함으로 재개하지 않는다.

# 목적

승인된 미디어 정책 문서를 기준으로 Phase 1B의 첫 구현 배치인 **로컬 미디어 저장 핵심 경로**를 구현하고, 합성 파일과 격리된 TEMP 저장소로 검증한다.

이번 배치는 승인된 모바일 UI를 다시 수정하는 작업이 아니며, 실제 사용자 DB·실제 사용자 사진·운영 저장소를 사용하지 않는다.

# 위험도와 검증 예산

```text
RISK_TIER = T3_HIGH
```

위험 요인:

- 업로드 파일의 형식·용량·픽셀·디코딩 검증
- 로컬 파일 경로와 저장 경계
- 부분 실패 시 고아 파일 또는 메타데이터 불일치
- 기존 DB/API 계약과 저장 생명주기의 불일치

필수 Gate:

- Git 및 기존 dirty 기준선 확인
- 승인된 D-MEDIA 결정과 현행 코드 계약 대조
- 경로 탈출·파일 위장·손상 파일·초과 파일 검증
- 실패 시 부분 산출물 정리 검증
- 집중 단위·통합 테스트
- 위험 경로에 대한 독립 검토 1회

의도적으로 생략:

- 실제 사용자 DB/API/파일을 사용한 통합 UAT
- schema/migration 생성·수정·적용
- 브라우저 시각 재검토
- deploy
- commit/push

# 시작 기준

```text
PROJECT_ROOT = C:\EtcProject\MyDocuMgm
EXPECTED_BRANCH = main
EXPECTED_HEAD = 3388081b8c71b9bfefdc1e3c08cc02fbf3942c28
EXPECTED_STAGED_CHANGES = 0
EXPECTED_DIRTY_PATH_COUNT = 34
```

시작 시 다음을 읽기 전용으로 확인한다.

- branch, HEAD, staged 상태
- `git status --short`
- 직전 34개 dirty 경로의 exact 목록
- 기존 33개 변경에 `WorkflowStepper.vue` 1개가 추가된 상태인지
- 이번 배치와 무관한 새 변경이 섞였는지
- 저장소 내부 임시 이미지·로그·브라우저 프로필 유무
- 포트 `5173`, `5080`의 잔여 listener

기준선이 다르면 파일을 되돌리거나 개수를 맞추지 말고 `HOLD_BASELINE_RECONCILIATION_REQUIRED`로 종료한다.

# 권위 문서와 결정

가장 먼저 다음 문서를 전체 읽고, 이 문서가 참조하는 Phase 1B 관련 권위 문서도 필요한 범위에서 읽는다.

```text
docs\PHASE1B_LOCAL_MEDIA_STORAGE_SCOPE_DECISION_KR.md
```

다음 사용자 결정은 고정한다.

```text
ACCEPT_RECOMMENDED_BUNDLE = YES
D-MEDIA-001..012 = APPROVED_BUNDLE
D10 = DEFER_EXTERNAL_URL_ENTIRELY
D12 = JPEG_PNG_WEBP_FULL_DECODE
MAX_FILE_BYTES = 20971520
MAX_PIXEL_WIDTH = 8192
MAX_PIXEL_HEIGHT = 8192
MAX_TOTAL_PIXELS = 40000000
```

문서의 나머지 D-MEDIA 값, 저장 위치, 이름 규칙, 중복·삭제·thumbnail·수명주기 정책은 문서 원문을 source of truth로 사용한다. 추정하거나 이 지시서의 일반 문구로 덮어쓰지 않는다.

문서와 현재 코드·DB 계약이 충돌하면 구현으로 임의 결정하지 말고 정확한 충돌과 최소 사용자 선택지를 보고한다.

# ALREADY_DONE — 보호 범위

다음은 승인된 상태이므로 새로운 반대 증거가 없으면 재작성하지 않는다.

- Phase 1A schema와 migration 결과
- 새 작업별 `new-<uuid>` 라우팅
- 작업 전환 시 1~7단계 상태 초기화
- 늦은 미디어 응답 무효화
- 중립 회색 placeholder
- 합성 thumbnail 표시
- 모바일 2열 카드
- 모바일 현재 단계 자동 노출
- 모바일 단일 행 작업 바와 focus 안전 여백
- 사용자 시각 승인된 데스크톱·모바일 레이아웃

특히 다음 파일은 Phase 1B 저장 핵심 구현에 직접 필요하다는 증거가 없으면 수정하지 않는다.

```text
src\MyDocuMgm.Web\src\features\content-workflow\WorkflowStepper.vue
src\MyDocuMgm.Web\src\shared\styles\features.css
src\MyDocuMgm.Web\src\pages\WorkflowPage.vue
```

# 구현 전 대조

코드를 수정하기 전에 다음을 표로 정리한다.

| 항목 | 권위 정책 | 현재 owner/contract | 구현 필요 | 충돌 여부 |
|---|---|---|---|---|
| 허용 형식 | D-MEDIA 결정 | 현재 validator/decoder |  |  |
| 용량·크기·총 픽셀 | 확정 상수 | 현재 검증 코드 |  |  |
| 저장 root와 경로 | D-MEDIA 결정 | 현재 option/provider |  |  |
| 원본·thumbnail | D-MEDIA 결정 | 현재 storage contract |  |  |
| 메타데이터 | D-MEDIA 결정 | 기존 Phase 1A entity/schema |  |  |
| 중복 처리 | D-MEDIA 결정 | 현재 service |  |  |
| 삭제·정리 | D-MEDIA 결정 | 현재 lifecycle |  |  |
| 외부 URL | 전체 보류 | 현재 API/UI |  |  |

다음 중 하나라도 필요하면 구현하지 않고 HOLD한다.

- 새 schema 또는 migration
- Phase 1A 테이블 의미 변경
- 승인되지 않은 API 계약 변경
- 실제 사용자 저장 root 접근
- D-MEDIA 문서에 없는 제품 결정

# 구현 범위

권위 문서와 기존 계약이 충돌하지 않을 때만 다음의 하나의 완결된 경로를 구현한다.

```text
합성 로컬 업로드 입력
→ 승인 형식·용량·픽셀·full decode 검증
→ 승인된 경로·이름 규칙으로 격리 저장
→ 문서가 정한 원본/thumbnail 산출
→ 기존 Phase 1A 계약과 호환되는 메타데이터 결과 반환
→ 실패 시 생성된 부분 파일 정리
```

구현 원칙:

- 확장자만으로 형식을 신뢰하지 않는다.
- JPEG/PNG/WebP는 실제 full decode가 성공해야 한다.
- 파일당 `20 MiB`, 가로·세로 `8192`, 총 `40,000,000` 픽셀 한계를 모두 적용한다.
- 외부 URL 수집·다운로드·미리보기는 구현하지 않는다.
- 입력 파일명으로 실제 저장 경로를 직접 조립하지 않는다.
- 저장 root 밖으로 나가는 상대경로·절대경로·경로 구분자 입력을 허용하지 않는다.
- 파일과 메타데이터 중 한쪽만 성공한 상태를 성공으로 반환하지 않는다.
- 실패·취소 시 이번 요청이 만든 임시·부분 파일만 정리한다.
- 기존 사용자 파일을 덮어쓰지 않는다.
- 로그·응답에 내부 절대경로와 stack trace를 노출하지 않는다.
- 문서가 정한 중복·삭제 정책을 그대로 구현한다.

# 저장·데이터 경계

이번 검증은 OS TEMP 아래 실행별 고유 디렉터리만 사용한다.

금지:

```text
C:\EtcProject\MyDocuMgm\data\MyDocuMgmData
실제 사용자 DB
실제 사용자 사진
사용자 프로필·문서·다운로드 폴더
운영 또는 장기 보존 storage root
```

테스트가 끝나면 이번 실행이 만든 TEMP 디렉터리만 정리하고, 정리 대상의 exact 경로를 보고한다.

# 허용 코드 범위

권위 문서와 현재 ownership 조사로 직접 필요성이 확인된 다음 프로젝트만 허용한다.

```text
src\MyDocuMgm.Domain\
src\MyDocuMgm.Application\
src\MyDocuMgm.Infrastructure\
src\MyDocuMgm.Api\
tests\
```

단, 다음은 허용하지 않는다.

- `.csproj`, package, lockfile 변경
- 새 외부 dependency 설치
- Web UI·CSS 수정
- DB schema/migration 변경
- 기존 Phase 1A migration 수정
- 보호 정책 문서·와이어프레임 수정

필요한 decoder 또는 imaging dependency가 현재 저장소에 없어서 full decode를 안전하게 구현할 수 없다면 임의 설치하지 말고 `HOLD_APPROVED_DECODER_DEPENDENCY_REQUIRED`로 종료하며 후보와 영향만 보고한다.

# 테스트

합성 데이터만 사용해 최소 다음을 검증한다.

## 성공 경로

- 정상 JPEG full decode 및 저장
- 정상 PNG full decode 및 저장
- 정상 WebP full decode 및 저장
- 정책상 필요한 원본·thumbnail·메타데이터 일치
- 같은 입력의 중복 처리가 D-MEDIA 결정과 일치

## 거부 경로

- 20 MiB 초과
- 가로 8192 초과
- 세로 8192 초과
- 총 40,000,000 픽셀 초과
- 손상·truncated JPEG/PNG/WebP
- 확장자와 실제 내용 불일치
- 허용되지 않은 형식
- 외부 URL 입력
- `..`, 절대경로, 경로 구분자 등 경로 탈출 시도

## 일관성·정리

- decode 실패 후 부분 파일 0
- thumbnail 실패 후 고아 파일·성공 메타데이터 0
- 저장 실패 후 성공 응답 0
- 취소 또는 예외 후 이번 요청의 TEMP 산출물 정리
- 기존 unrelated 파일 불변
- 내부 절대경로·stack trace가 사용자 응답에 노출되지 않음

실행할 명령은 저장소의 기존 solution/project 구조를 먼저 확인해 정한다. 최소:

```text
dotnet restore --locked-mode 또는 저장소가 승인한 동등 명령
dotnet build --no-restore
변경 프로젝트 집중 테스트
관련 기존 회귀 테스트
git diff --check
```

dependency 또는 lockfile 변경이 없고 기존 restore 자산이 충분하면 불필요한 외부 복원을 반복하지 않는다.

# 독립 검토

구현과 테스트가 끝난 뒤 별도 읽기 전용 검토 1회를 수행한다.

검토 대상:

- D-MEDIA 결정 누락 또는 변형
- path traversal 가능성
- extension/MIME만 신뢰하는 우회
- partial write·orphan file 가능성
- 실제 사용자 경로 접근 여부
- schema/migration/API 계약 무단 변경
- 승인된 UI 변경 여부

독립 검토가 새 코드를 수정하지 않는다. Major 이상이 발견되면 현재 배치에서 허용 범위 안의 최소 수리와 집중 재검증까지만 수행한다.

# 금지 작업

- 실제 `data\MyDocuMgmData` 생성·조회·수정
- 실제 사용자 DB/API/사진 사용
- 외부 URL 다운로드
- schema/migration 생성·수정·적용
- 승인되지 않은 endpoint·payload 변경
- 새 dependency 설치
- 모바일/데스크톱 UI 재수정
- 보호 문서·와이어프레임 수정
- unrelated dirty 파일 수정·삭제·되돌리기
- `git reset`, `checkout`, `clean`, `stash`
- Git add/commit/amend/push
- deploy 또는 production 연결
- mock·TEMP 검증을 실제 통합 UAT로 승격
- `PRODUCTION_READY = YES` 선언

# 종료 정리

- 이번 테스트가 만든 TEMP root 정리
- 시작한 test host와 listener 종료
- 저장소 내부 임시 파일·합성 이미지·로그 0
- `git diff --check`
- `git status --short`
- 시작·종료 branch와 HEAD 비교
- staged 변경 0
- exact changed file 목록
- 기존 34개 dirty 경로 침범 여부
- Web UI/CSS 변경 0
- schema/migration 변경 0
- commit/push 0

# 완료 판정

정확히 하나만 사용한다.

```text
PASS_MYDOCUMGM_PHASE1B_LOCAL_MEDIA_STORAGE_CORE
REPAIR_REQUIRED_MYDOCUMGM_PHASE1B_LOCAL_MEDIA_STORAGE_CORE
HOLD_BASELINE_RECONCILIATION_REQUIRED
HOLD_MEDIA_POLICY_CONTRACT_CONFLICT
HOLD_APPROVED_DECODER_DEPENDENCY_REQUIRED
HOLD_TEST_ENVIRONMENT_REQUIRED
REJECT_SCOPE_VIOLATION
```

PASS 조건:

- D-MEDIA 승인 묶음과 구현이 일치
- JPEG/PNG/WebP full decode와 모든 수치 제한 적용
- 외부 URL 전체 보류 유지
- 저장 root 경계와 path traversal 방어 통과
- 성공·실패·정리 테스트 통과
- 기존 Phase 1A schema/API 계약과 호환
- 실제 사용자 DB·파일·저장 root 접근 0
- Web UI/CSS와 승인된 시각 결과 변경 0
- schema/migration/dependency 변경 0
- 독립 검토 Major 0
- 기존 dirty 변경 침범 0
- staged/commit/push/deploy 0

PASS하더라도 다음 상태는 유지한다.

```text
USER_VISUAL_ACCEPTANCE = ACCEPT
INTEGRATED_WEB_API_DB_UAT = HOLD_TEST_ENVIRONMENT_REQUIRED
PRODUCTION_READY = NO
SCHEMA_MIGRATION = NOT_AUTHORIZED
DEPLOYMENT = NOT_AUTHORIZED
COMMIT_PUSH = NOT_AUTHORIZED
```

# 완료 보고

1. 최종 판정과 Blocking/Major/Minor
2. D-MEDIA 결정 대조표
3. 시작·종료 branch, HEAD, staged, dirty 상태
4. exact changed file과 각 책임
5. 저장 root·경로·이름·중복·삭제 정책 구현 결과
6. JPEG/PNG/WebP full decode와 제한값 결과
7. path traversal·위장·손상 파일 방어 결과
8. 부분 실패·고아 파일 정리 결과
9. 실행한 명령과 exit code
10. 독립 검토 결과
11. TEMP root와 프로세스 정리
12. 실제 사용자 DB·파일·API 접근 0
13. Web UI/CSS·schema·migration·dependency 변경 0
14. Git add/commit/push/deploy 0
15. 통합 UAT와 production 비승인 상태
