# copilotTest

.NET 8.0으로 구축된 웹 스크래핑 애플리케이션으로 웹 콘텐츠 수집, 저장 및 내보내기를 위한 REST API를 제공합니다. 이 애플리케이션은 정적 및 동적 웹 스크래핑 접근 방식을 모두 지원하며, 데이터 중복 제거 및 백그라운드 처리를 위한 기능이 내장되어 있습니다.

## 기능

- **다중 모드 웹 스크래핑**:
  - 전통적인 HTML/JSON 콘텐츠를 위한 정적 스크래핑
  - JavaScript로 렌더링된 페이지를 위한 Playwright를 사용한 동적 스크래핑
  - 대상 콘텐츠 추출을 위한 사용자 정의 CSS 선택자 지원
- **효율적인 데이터 저장**:
  - 스크래핑된 콘텐츠를 위한 LiteDB NoSQL 데이터베이스
  - 콘텐츠 해싱을 통한 중복 제거
  - 추가 컨텍스트를 위한 메타데이터 저장
- **백그라운드 처리**:
  - 스크래핑 작업 대기열 및 일정 관리를 위한 Hangfire 통합
  - 단일 요청으로 여러 URL을 병렬 처리 지원
- **유연한 데이터 내보내기**:
  - CSV, JSON 또는 HTML 형식으로 내보내기
  - 날짜 범위 및 URL별 필터링 옵션

## 설치

### 필수 조건
- .NET 8.0 SDK
- Docker (선택 사항, 컨테이너화된 배포용)

### 로컬 개발
1. 리포지토리 복제
   ```
   git clone https://github.com/lovedownload/copilotTest.git
   cd copilotTest
   ```

2. 종속성 복원 및 빌드
   ```
   dotnet restore src/copilotTest.csproj
   dotnet build src/copilotTest.csproj
   ```

3. 애플리케이션 실행
   ```
   dotnet run --project src/copilotTest.csproj
   ```

### Docker 배포
```
docker build -t copilottest .
docker run -p 8080:8080 copilottest
```

## 사용 예제

### 웹페이지 스크래핑 (API를 통해)

API는 단일 URL과 복수 URL 모두 동일한 엔드포인트로 처리합니다. 스크래핑 요청은 "url" 또는 "urls" 필드를 통해 전달될 수 있습니다.

**단일 URL 스크래핑:**
```
POST /api/data/scrape
Content-Type: application/json

{
  "url": "https://example.com",
  "useDynamicScraping": true,
  "waitTimeMs": 8000,
  "selectors": {
    "title": "h1",
    "content": "main"
  }
}
```

**복수의 웹페이지 동시 스크래핑:**
```
POST /api/data/scrape
Content-Type: application/json

{
  "urls": ["https://example.com", "https://example.org", "https://example.net"],
  "useDynamicScraping": true,
  "waitTimeMs": 5000,
  "selectors": {
    "title": "h1",
    "content": "main"
  }
}
```

복수 URL은 병렬로 처리되며, 응답은 스크래핑 결과의 배열로 반환됩니다. 단일 URL이 요청된 경우에는 단일 객체로 응답합니다.

### 수집된 데이터 내보내기
```
POST /api/data/export
Content-Type: application/json

{
  "format": "csv",
  "urlFilter": "example.com",
  "startDate": "2023-01-01T00:00:00Z"
}
```

아래의 curl 명령어를 통해 로컬 또는 서버에서 REST API를 쉽게 테스트할 수 있습니다.

**단일 URL 스크래핑:**
```sh
curl -X POST http://localhost:8080/api/data/scrape \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://example.com",
    "useDynamicScraping": true,
    "selectors": {
      "title": "h1",
      "content": "main"
    }
  }'
```

**복수 URL 스크래핑: (병렬 처리)**
```sh
curl -X POST http://localhost:8080/api/data/scrape \
  -H "Content-Type: application/json" \
  -d '{
    "urls": ["https://example.com", "https://example.org"],
    "useDynamicScraping": true,
    "selectors": {
      "title": "h1",
      "content": "main"
    }
  }'
```

### 백그라운드에서 스크래핑 작업 큐에 추가

백그라운드 스크래핑도 동일한 방식으로 단일 URL 또는 복수 URLs를 처리할 수 있습니다:

**단일 URL 백그라운드 스크래핑:**
```sh
curl -X POST http://localhost:8080/api/data/scrape/background \
  -H "Content-Type: application/json" \
  -d '{
    "url": "https://example.com",
    "useDynamicScraping": true,
    "selectors": {
      "title": "h1",
      "content": "main"
    }
  }'
```

**복수 URL 백그라운드 스크래핑:**
```sh
curl -X POST http://localhost:8080/api/data/scrape/background \
  -H "Content-Type: application/json" \
  -d '{
    "urls": ["https://example.com", "https://example.org"],
    "useDynamicScraping": true
  }'
```

```sh
curl -X POST http://localhost:8080/api/data/export \
  -H "Content-Type: application/json" \
  -d '{
    "format": "csv",
    "urlFilter": "example.com",
    "startDate": "2023-01-01T00:00:00Z"
  }'
```
