# MyDocuMgm — 나의 생활북

개인이 생활 지식과 경험을 분류하고, 이미지와 함께 로컬에 정리하는 단일 사용자용 애플리케이션입니다.

## 구성

- `src/MyDocuMgm.Api`: ASP.NET Core Web API (`http://localhost:5080`)
- `src/MyDocuMgm.Web`: Vue 3 + TypeScript + Vite (`http://localhost:5173`)
- `src/MyDocuMgm.Domain`: 프레임워크 비의존 도메인 모델
- `src/MyDocuMgm.Application`: 유스케이스와 포트
- `src/MyDocuMgm.Infrastructure`: EF Core SQL Server 및 로컬 이미지 저장 구현

## Phase 1A 실행 경계

Phase 1A는 코드, 테스트, EF 마이그레이션과 사용자 적용용 SQL까지만 생성합니다. DB 연결·변경과 실제 `data/MyDocuMgmData` 생성은 Phase 1B에서 사용자가 수행합니다.

```powershell
dotnet tool restore
dotnet restore
dotnet build
dotnet test --no-build
Set-Location src\MyDocuMgm.Web
npm ci
npm run check
npm run build
npm test
```
