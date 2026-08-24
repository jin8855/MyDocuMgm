# MyDocuMgm Phase 2D 추천 기반 계약

## 1. 승인 결정

| 결정 | 승인값 |
|---|---|
| P2D-D01 | `PROVIDER_AGNOSTIC_CONTRACT_ONLY` |
| P2D-D02 | `DURABLE_RECOMMENDATION_AND_DECISION_AUDIT` |
| P2D-D03 | `QUALITATIVE_UNCERTAINTY_WITH_REASON` |
| P2D-D04 | `DEFER_IMPORTJOBS_AND_KEEP_EXTERNAL_FETCH_ATTEMPTS_SEPARATE` |
| P2D-D05 | `CURRENT_TEXT_EVIDENCE_ONLY` |

이 결정은 사용자 승인 상태다. 외부 AI provider 연결, secret 추가, 실제 외부 호출은 승인되지 않았다.

## 2. 사용자와 데이터 소유권

추천은 제목, 요약, 고정 분류, 태그의 후보일 뿐 `Content`의 최종값이 아니다. 추천 요청은 기존 값을 변경하지 않는다. 사용자는 항목별로 적용, 직접 수정, 사용 안 함을 선택하며, 최종 결정 저장 요청 한 번이 성공한 경우에만 선택한 값이 원자적으로 반영된다. 사용자가 입력한 값과 추천 이력은 서로 다른 소유권과 수명주기를 갖는다.

사용자 흐름은 다음과 같다.

1. 현재 저장된 텍스트와 사용자 값을 확인한다.
2. 추천을 요청한다.
3. 현재값, 추천값, 짧은 이유와 `높음/보통/낮음` 확신을 비교한다.
4. 제목·요약·분류·태그를 각각 적용, 수정 또는 거부한다.
5. 선택 내용을 저장한다.
6. 수동 분석 검토와 기존 Phase 2 Core workflow를 계속한다.

## 3. 정상·부분 성공·실패·거부

- 정상: 생성된 추천 항목을 `SUCCEEDED`로 기록한다.
- 부분 성공: 생성된 항목만 `PARTIALLY_SUCCEEDED`에 포함하며 누락 항목은 성공으로 꾸미지 않는다.
- 실패: 안전한 오류 코드만 기록하고 기존 `Content`를 유지한다. raw prompt, raw response, 내부 exception은 저장하거나 응답하지 않는다.
- 취소: `CANCELLED`와 안전한 오류 코드를 기록하고 기존 값을 유지한다.
- 거부: 추천 항목은 `REJECTED`로 감사 기록을 남기며 기존 값을 유지한다. 다음 단계 진행을 차단하지 않는다.
- 충돌: 최신 `Content.RowVersion`과 다르면 전체 결정을 저장하지 않고 HTTP 409를 반환한다.
- 반복: 동일 idempotency key의 추천 요청과 동일 결정 재전송은 새 이력이나 태그 중복을 만들지 않는다.

## 4. Provider 경계

`IAnalysisRecommendationProvider`는 현재 사용자 값, 고정 11개 분류, 현재 저장된 텍스트 근거와 취소 토큰만 받는다. 출력은 종류, 추천값, 이유, 정성 확신, 제한된 근거와 부분 성공 상태다.

제품 기본 provider는 `NOT_CONNECTED`이며 fail-closed다. 미연결 시 `ANALYSIS_PROVIDER_NOT_CONNECTED`와 “추천 기능이 아직 연결되지 않았습니다. 직접 작성으로 계속할 수 있습니다.”를 반환한다. 외부 SDK, API key, secret, 네트워크 기능은 포함하지 않는다. 결정적 fake provider는 테스트 DI에서만 교체할 수 있다.

## 5. 개인정보·원문·감사·보존

- 현재 근거는 `DetailContent`, 수동 Instagram caption, 작성자 고정 댓글, 기존 `SourceEvidence` 제목으로 제한한다.
- 추천 테이블에는 원문 전체를 복제하지 않고 최대 500자의 근거 발췌 또는 기존 `SourceEvidence` 식별자만 저장한다.
- 전체 URL, raw prompt, raw provider 응답, connection string, credential은 저장하지 않는다.
- 추천 Run은 `Content` 소유이며 Content lifecycle과 함께 cascade 삭제된다.
- Run의 Item과 근거는 각각 Run/Item과 함께 cascade 삭제된다.
- `SourceEvidence`가 별도 lifecycle에서 제거되면 추천 근거 참조는 null이 될 수 있지만 제한 발췌와 결정 감사는 보존한다.

## 6. API·데이터·UI 계약

API:

- `POST /api/contents/{contentId}/analysis-recommendations`
- `GET /api/contents/{contentId}/analysis-recommendations/latest`
- `GET /api/contents/{contentId}/analysis-recommendations?limit=10`
- `PUT /api/contents/{contentId}/analysis-recommendations/{runId}/decisions`

데이터:

- `AnalysisRecommendationRuns`: 상태, provider/model 식별, idempotency, 안전 오류 코드, rowversion
- `AnalysisRecommendationItems`: TITLE/SUMMARY/CATEGORY/TAG, 이유, LOW/MEDIUM/HIGH, PENDING/APPLIED/MODIFIED/REJECTED
- `AnalysisRecommendationEvidence`: 제한 발췌와 선택적 `SourceEvidenceId`

UI는 현재값과 적용 예정값을 분리하고 낮은 확신에 `확인 필요`를 표시한다. loading, empty, populated, partial, unavailable, error, conflict, saved를 구분한다. provider가 없어도 수동 제목·요약 편집과 다음 workflow를 사용할 수 있다.

## 7. 테스트 계약

도메인 상태 전환, 현재값 비덮어쓰기, 혼합 결정, 부분 성공, 실패·취소·미연결, Content/Run 소유권, idempotency, 분류 검증, 태그 정규화, rowversion, 원자적 저장과 rollback을 검증한다. Web은 상태별 UI, 접근성, 390px overflow와 기존 수동 흐름을 검증한다. DB 검증은 고유 폐기형 DB만 사용한다.

## 8. 명시적 유보와 미승인

- `ImportJobs`: 전체 가져오기 orchestration, retry와 단계별 상태가 승인될 때 재검토한다.
- `ExternalFetchAttempts`: HTTP 요청별 시도 기록으로 유지하며 의미를 변경하지 않는다.
- 자동 자막·댓글 수집, 외부 이미지·영상 다운로드: 유보한다.
- 실제 외부 AI 연동과 실행, Phase 3, 배포, commit/push/PR/merge: 승인되지 않았다.

## 9. 사용자 중개 수동 복사·붙여넣기 계약

Phase 2D의 실행 방식은 `USER_MEDIATED_COPY_PASTE`다. MyDocuMgm은 현재 자료에서 프롬프트를 만들지만 외부 AI 도구로 전송하지 않는다. 사용자가 프롬프트를 확인·수정하고 명시적으로 복사한 뒤, 외부 도구의 답변을 다시 제품에 붙여넣는다.

- schema version: `mydocumgm.analysis-recommendation.v1`
- provenance: `MANUAL_COPY_PASTE`
- 입력 근거: 현재 제목·요약·본문·수동 Caption·작성자 고정 댓글·기존 `SourceEvidence`·현재 분류·태그 중 사용자가 선택한 실제 값
- 응답: 정확한 JSON 또는 단일 `json` 코드 블록, 알 수 없는 필드 거부, UTF-8 64 KiB 및 JSON depth 16 이하
- 검증: 현재 Content fingerprint, 선택 근거 ID, 고정 11개 분류, 제목 200자, 요약 500자, 태그 각 80자·최대 20개, 사유 500자, 정성 확신 `HIGH/MEDIUM/LOW`
- 저장: 검증된 item·reason·confidence·제한 근거·idempotency만 기존 Phase 2D 테이블에 저장
- 비영속: 생성·편집 prompt, raw 붙여넣기 답변, 외부 서비스·계정·모델 추정값
- 적용: import 성공은 `Content`를 바꾸지 않는다. 기존 적용·수정·사용 안 함 선택과 사용자 최종 저장이 성공해야만 값이 반영된다.

API는 `POST .../manual-prompt`와 `POST .../manual-import`를 제공한다. 기존 provider endpoint는 호환을 위해 fail-closed 상태로 유지하지만 제품 UI는 호출하지 않는다. 외부 AI SDK, API key, secret, `HttpClient` 기반 AI 호출은 추가하지 않는다. 이 계약은 기존 schema의 `ProviderIdentifier`, `ModelVersion`, idempotency 및 item/evidence 구조를 재사용하므로 신규 migration을 요구하지 않는다.
