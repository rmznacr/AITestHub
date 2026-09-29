import { http } from './client';
import type {
  TestCaseResponse,
  ExecuteTestCaseResponse,
  ExecutionHistory,
  ExecutionDetail,
} from '../types';

export function getTestCases(projectId?: number): Promise<TestCaseResponse[]> {
  const query = projectId !== undefined ? `?projectId=${projectId}` : '';
  return http.get<TestCaseResponse[]>(`/api/testcases${query}`);
}

export function getTestCase(testCaseId: number): Promise<TestCaseResponse> {
  return http.get<TestCaseResponse>(`/api/testcases/${testCaseId}`);
}

export function executeTestCase(testCaseId: number): Promise<ExecuteTestCaseResponse> {
  return http.post<ExecuteTestCaseResponse>(`/api/testcases/${testCaseId}/execute`);
}

export function getExecutionHistory(testCaseId: number): Promise<ExecutionHistory[]> {
  return http.get<ExecutionHistory[]>(`/api/testcases/${testCaseId}/executions`);
}

export function getExecutionDetail(executionId: number): Promise<ExecutionDetail> {
  return http.get<ExecutionDetail>(`/api/testexecutions/${executionId}`);
}
