import { http } from './client';
import type { Project, ProjectTestSummary, BatchExecuteResponse } from '../types';

export function getProjects(): Promise<Project[]> {
  return http.get<Project[]>('/api/projects');
}

export function getProject(projectId: number): Promise<Project> {
  return http.get<Project>(`/api/projects/${projectId}`);
}

export function createProject(project: Pick<Project, 'name' | 'description'>): Promise<Project> {
  return http.post<Project>('/api/projects', project);
}

export function getProjectTestSummary(projectId: number): Promise<ProjectTestSummary> {
  return http.get<ProjectTestSummary>(`/api/projects/${projectId}/test-summary`);
}

export function executeProjectTests(projectId: number): Promise<BatchExecuteResponse> {
  return http.post<BatchExecuteResponse>(`/api/projects/${projectId}/execute`);
}
