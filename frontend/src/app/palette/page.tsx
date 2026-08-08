"use client";

import { GeneratorStudio, api } from "@/components/generator-studio";

export default function PalettePage() {
  return (
    <GeneratorStudio
      title="Color Palette Generator"
      description="Primary, secondary, accent, background, and typography colors with CSS/JSON export."
      actionLabel="Generate palette"
      run={(brandId) =>
        api("/api/palettes/generate", {
          method: "POST",
          body: JSON.stringify({ brandId }),
        })
      }
      renderResult={(data) => {
        const d = data as {
          id: string;
          primaryHex: string;
          secondaryHex: string;
          accentHex: string;
          backgroundHex: string;
          typographyHex: string;
          cssExport?: string;
        };
        const colors = [d.primaryHex, d.secondaryHex, d.accentHex, d.backgroundHex, d.typographyHex];
        return (
          <div className="space-y-3">
            <div className="flex flex-wrap gap-3">
              {colors.map((c) => (
                <div key={c} className="text-center text-xs">
                  <div className="h-12 w-12 rounded-lg border" style={{ background: c }} />
                  <div className="mt-1">{c}</div>
                </div>
              ))}
            </div>
            <pre className="overflow-auto text-xs whitespace-pre-wrap">{d.cssExport}</pre>
            <a className="text-sm text-teal-500" href={`${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5080"}/api/palettes/${d.id}/export?format=tailwind`} target="_blank">
              Export Tailwind
            </a>
          </div>
        );
      }}
    />
  );
}
