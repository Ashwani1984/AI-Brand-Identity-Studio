"use client";

import { GeneratorStudio, api } from "@/components/generator-studio";

export default function MarketingPage() {
  return (
    <GeneratorStudio
      title="Marketing Generator"
      description="Ads, email, launch plan, and 90-day roadmap copy pack."
      actionLabel="Generate marketing kit"
      run={(brandId) =>
        api("/api/marketing/generate", {
          method: "POST",
          body: JSON.stringify({ brandId }),
        })
      }
    />
  );
}
