// TypeScript types mirroring the backend DTOs.
// Enums match the backend's JSON string values.

export type Priority = 'Low' | 'Medium' | 'High' | 'Critical';

export type TestType = 'Positive' | 'Negative' | 'EdgeCase';

export type TestStatus = 'NotRun' | 'Passed' | 'Failed' | 'Blocked';

export interface Project {
  id: number;
  name: string;
  description?: string | null;
  createdAt: string;
}

export interface TestCaseResponse {
  id: number;
  name: string;
  description?: string | null;
  preconditions?: string | null;
  steps: string;
  expectedResult?: string | null;
  priority: Priority;
  testType: TestType;
  status: TestStatus;
  executionNotes?: string | null;
  lastExecutedAt?: string | null;
  httpMethod: string;
  endpoint: string;
  requestBody?: string | null;
  expectedStatusCode: number;
  projectId: number;
  createdAt: string;
}

export interface ProjectTestSummary {
  projectId: number;
  projectName: string;
  totalTests: number;
  notRun: number;
  passed: number;
  failed: number;
  blocked: number;
  passRate: number;
}

export interface DashboardSummary {
  totalProjects: number;
  totalTests: number;
  notRun: number;
  passed: number;
  failed: number;
  blocked: number;
  passRate: number;
}

export interface GeneratedApiTestCase {
  title: string;
  description: string;
  preconditions?: string | null;
  steps: string;
  expectedResult: string;
  priority: Priority;
  testType: TestType;
  httpMethod: string;
  endpoint: string;
  requestBody?: string | null;
  expectedStatusCode: number;
}

export interface GenerateApiTestCasesRequest {
  projectId: number;
  baseUrl: string;
  endpoint: string;
  httpMethod: string;
  requirement: string;
  count: number;
}

export interface GenerateApiTestCasesResponse {
  projectId: number;
  baseUrl: string;
  requirement: string;
  testCases: GeneratedApiTestCase[];
}

export interface SaveGeneratedApiTestCasesRequest {
  projectId: number;
  testCases: GeneratedApiTestCase[];
}

export interface SaveGeneratedTestCasesResponse {
  projectId: number;
  savedCount: number;
  testCases: TestCaseResponse[];
}

export interface ExecuteTestCaseResponse {
  testCaseId: number;
  executionResultId: number;
  status: TestStatus;
  passed: boolean;
  executedAt: string;
  durationMs: number;
  requestMethod: string;
  requestUrl: string;
  expectedStatusCode: number;
  actualStatusCode: number;
  responseBody?: string | null;
  errorMessage?: string | null;
}

export interface BatchExecuteResponse {
  total: number;
  passed: number;
  failed: number;
  results: ExecuteTestCaseResponse[];
}

export interface ExecutionHistory {
  id: number;
  testCaseId: number;
  executedAt: string;
  durationMs: number;
  requestMethod: string;
  requestUrl: string;
  expectedStatusCode: number;
  actualStatusCode: number;
  passed: boolean;
  errorMessage?: string | null;
}

export interface ExecutionDetail extends ExecutionHistory {
  requestBody?: string | null;
  responseBody?: string | null;
}
