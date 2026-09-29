import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Layout from './components/Layout';
import Dashboard from './pages/Dashboard';
import Projects from './pages/Projects';
import ProjectDetail from './pages/ProjectDetail';
import GenerateTests from './pages/GenerateTests';
import TestCaseDetail from './pages/TestCaseDetail';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/" element={<Dashboard />} />
          <Route path="/projects" element={<Projects />} />
          <Route path="/projects/:projectId" element={<ProjectDetail />} />
          <Route path="/projects/:projectId/generate" element={<GenerateTests />} />
          <Route path="/testcases/:testCaseId" element={<TestCaseDetail />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
