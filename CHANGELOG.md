# Changelog

## 1.0.0

Initial release.

- AI test case generation via the EVREN LLM API (GLM 5.3): manual and executable API test cases
- Project and test case management (CRUD, priority, type, status, execution notes)
- Review-and-save workflow for AI-generated tests (generate never persists)
- Automated API test execution with expected/actual status code assertions
- Single, batch and per-project execution
- Execution history with request/response details
- Dashboard and management UI (React + TypeScript + Vite)
- SSRF-safe outbound execution (private network blocking, DNS checks, redirects disabled)
- Docker deployment (backend, frontend, PostgreSQL) with health checks
