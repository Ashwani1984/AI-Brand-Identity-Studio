"use client";

import { GeneratorStudio, api, assetUrl } from "@/components/generator-studio";

export default function LandingPage() {
  return (
    <GeneratorStudio
      title="Landing Page Generator"
      description="Generate a responsive HTML landing page with hero, about, services, and contact."
      actionLabel="Generate landing page"
      run={(brandId) =>
        api("/api/landing/generate", {
          method: "POST",
          body: JSON.stringify({ brandId }),
        })
      }
      renderResult={(data) => {
        const d = data as { url?: string; htmlPreview?: string };
        return (
          <div className="space-y-3">
            <a className="text-teal-500" href={assetUrl(d.url)} target="_blank">
              Open landing page
            </a>
            <pre className="overflow-auto text-xs whitespace-pre-wrap">{d.htmlPreview}</pre>
          </div>
        );
      }}
    />
  );
}
