"use client";

import { GeneratorStudio, api } from "@/components/generator-studio";

export default function SocialPage() {
  return (
    <GeneratorStudio
      title="Social Media Calendar"
      description="14–30 day calendar across LinkedIn, Instagram, X, Facebook, and Shorts."
      actionLabel="Generate calendar"
      extraOptions={[
        { label: "14 days", value: "14" },
        { label: "30 days", value: "30" },
      ]}
      run={(brandId, _p, days) =>
        api("/api/social/calendar", {
          method: "POST",
          body: JSON.stringify({ brandId, days: Number(days || 14) }),
        })
      }
    />
  );
}
