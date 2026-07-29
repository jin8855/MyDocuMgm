# MyDocuMgm Phase 1A Repair 1 — Wireframe v7 차이 분석

## 기준

- 기준 HEAD: `5a6a1111008cc40bcee3df4d2031b528a5389a6d`
- 권위 와이어프레임: `docs\wireframes\MYDOCUMGM_WIREFRAME_V7_KR.html`
- Bytes: `54,387`
- SHA-256: `9A686ECE38520DFCD0D116117A9F582E1EF53FEBFA2DDFDDA201E7DCD48AB0A1`
- 보호 파일: `docs\wireframes`의 v1, v2, v4, v5, v6, v7 6개

## 화면·기능 매핑

| 영역 | 현재 상태 | 판정 | Repair 방향 |
|---|---|---|---|
| .NET 10 / EF Core 10 레이어 | Domain/Application/Infrastructure/API 분리 | ALREADY_DONE | 기존 참조 방향 보존 |
| Vue 3 / Router / Vite | 요약·목록·등록·상세·미디어 route 존재 | ALREADY_DONE | 기술 기반 보존 |
| 이미지 보안 저장 | 형식·크기·서명·경로·보상 처리 | ALREADY_DONE | 저장 정책 변경 없음 |
| soft delete / rowversion / 태그 정규화 | 구현 및 비연결 테스트 존재 | ALREADY_DONE | 신규 유스케이스에 동일 규칙 적용 |
| 좌측 메뉴 | 상단 메뉴 사용 | REPAIR_REQUIRED | 작업보드·작업목록·분류 관리·모바일 읽기만 표시 |
| 7단계 워크플로 | 독립 CRUD route 중심 | REPAIR_REQUIRED | URL→분석 검토→분류별 편집→이미지→자료 상세→블로그 초안→완료 |
| 작업목록 | 제목·분류·상태 중심 | REPAIR_REQUIRED | 현재 단계·초안 상태·태그·수정일과 동적 검색 추가 |
| 검색용 주재료 | 일반 재료 Name/Quantity만 존재 | REPAIR_REQUIRED | `CookingIngredients.IsPrimary`와 안전한 검색 추가 |
| 재료 편집 | UI/API 부재 | REPAIR_REQUIRED | 추가·수정·삭제·정렬·주재료 지정/해제와 rowversion |
| 분류 관리 | 고정 조회만 존재 | REPAIR_REQUIRED | 코드 고정, 표시명·정렬·활성·검색 속성만 관리 |
| 검색 속성 | 메타데이터와 허용 필드 검증 부재 | REPAIR_REQUIRED | 코드 카탈로그+DB 메타데이터, raw SQL 금지 |
| 대량 이미지 | 전체 목록 반환 | REPAIR_REQUIRED | 24/48/96 서버 페이징, 선택/중복 필터, 시간 정렬 |
| 이미지-단계 연결 | `ContentStep`과 `MediaAsset` 연결 없음 | REPAIR_REQUIRED | nullable FK와 Restrict 삭제 동작 |
| 상세 상단 액션 | 편집/이미지 링크 중심 | REPAIR_REQUIRED | 단계와 같은 줄에 편집·삭제·닫기 |
| 대상 DB SQL 안전장치 | DB명 fail-fast 없음 | REPAIR_REQUIRED | `NOCOUNT`, `XACT_ABORT`, `DB_NAME()`/`THROW` 추가 |
| 검증 SQL | 기본 스키마 검증 | REPAIR_REQUIRED | 신규 컬럼·FK·CHECK·seed·인덱스 검증 확장 |
| Instagram 실제 수집 | 미구현 | OUT_OF_SCOPE | 화면상 비활성 placeholder만 유지 |
| AI 분석 | 미구현 | OUT_OF_SCOPE | 실행·연동 없음 |
| 블로그 실제 내보내기/게시 | 미구현 | OUT_OF_SCOPE | 워크플로 상태와 미리보기 기반만 제공 |
| 지각적 유사 이미지 분석 | 미구현 | DEFERRED_PHASE2 | SHA-256 완전 중복만 지원 |
| Publication / ImportJob | 미구현 | OUT_OF_SCOPE | 테이블·API 생성 금지 |

## 안전·구조 결정

1. 11개 분류 코드는 변경·추가·삭제하지 않는다.
2. 검색 속성은 `CategorySearchAttributeCatalog`에 등록된 키만 허용한다.
3. 사용자 입력을 SQL 식별자로 변환하거나 동적 SQL에 사용하지 않는다.
4. 미디어 soft delete는 `ContentStep`을 hard delete하지 않는다.
5. 삭제 미디어를 새 대표 이미지로 지정하는 도메인 요청을 거부한다.
6. 블로그 초안 상태는 현재 워크플로 단계에서 파생하며 `Publication`을 만들지 않는다.
7. Vue는 page, feature, shared 책임으로 분리하고, mock은 브라우저 증거 및 테스트 실행에서만 명시적으로 활성화한다.
8. 최초 마이그레이션은 실제 DB 미적용 상태이므로 같은 이름으로 재생성하고 전·후 SQL SHA-256을 기록한다.
