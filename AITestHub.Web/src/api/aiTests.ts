import { http } from './client';
import type {
  GenerateApiTestCasesRequest,
  GenerateApiTestCasesResponse,
  SaveGeneratedApiTestCasesRequest,
  SaveGeneratedTestCasesResponse,
} from '../types';

export function generateApiTestCases(
  request: GenerateApiTestCasesRequest,
): Promise<GenerateApiTestCasesResponse> {
  return http.post<GenerateApiTestCasesResponse>('/api/aitestcases/generate-api', request);
}

export function saveGeneratedApiTestCases(
  request: SaveGeneratedApiTestCasesRequest,
): Promise<SaveGeneratedTestCasesResponse> {
  return http.post<SaveGeneratedTestCasesResponse>('/api/aitestcases/save-api', request);
}
