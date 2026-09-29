import { http } from './client';
import type { DashboardSummary } from '../types';

export function getDashboardSummary(): Promise<DashboardSummary> {
  return http.get<DashboardSummary>('/api/dashboard/summary');
}
