import { api } from "./client";

export type Project = {
  id: string;
  name: string;
  status: string;
  brandId?: string | null;
  businessName?: string | null;
  createdAtUtc?: string;
};

export type Brand = {
  id: string;
  projectId: string;
  businessName: string;
  generatedName?: string;
  tagline?: string;
  brandStory?: string;
  brandVoice?: string;
  industry?: string;
};

export function listProjects() {
  return api<Project[]>("/api/projects");
}

export function getProject(id: string) {
  return api<Project>(`/api/projects/${id}`);
}

export function createProject(name: string) {
  return api<Project>("/api/projects", {
    method: "POST",
    body: JSON.stringify({ name }),
  });
}

export function getBrand(id: string) {
  return api<Brand>(`/api/brands/${id}`);
}

export function generateBrand(id: string) {
  return api<Brand>(`/api/brands/${id}/generate`, { method: "POST" });
}
