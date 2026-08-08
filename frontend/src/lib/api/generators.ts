import { api } from "./client";
import { enqueueAndWait } from "./jobs";

export type Logo = {
  id: string;
  brandId: string;
  style: string;
  promptText: string;
  imageUrl?: string;
  svgContent?: string;
  provider: string;
};

export type Palette = {
  id: string;
  brandId: string;
  primaryHex: string;
  secondaryHex: string;
  accentHex: string;
  backgroundHex: string;
  typographyHex: string;
  cssExport?: string;
};

export type Asset = {
  id: string;
  kind: string;
  fileName: string;
  contentType?: string;
  url: string;
  byteSize: number;
  createdAtUtc?: string;
};

export function listLogos(brandId: string) {
  return api<Logo[]>(`/api/logos?brandId=${brandId}`);
}

export function generateLogo(brandId: string, style: string) {
  return enqueueAndWait<Logo>("/api/logos/generate", { brandId, style });
}

export function generatePalette(brandId: string) {
  return api<Palette>("/api/palettes/generate", {
    method: "POST",
    body: JSON.stringify({ brandId }),
  });
}

export function generateMockup(brandId: string, mockupType: string) {
  return enqueueAndWait("/api/mockups/generate", { brandId, mockupType });
}

export function listAssets() {
  return api<Asset[]>("/api/downloads");
}

export function exportZip(projectId: string, packageType?: string) {
  return enqueueAndWait<Asset>("/api/downloads/zip", { projectId, packageType });
}
