"use client";

import { GeneratorStudio, api } from "@/components/generator-studio";

export default function TypographyPage() {
  return (
    <GeneratorStudio
      title="Typography Generator"
      description="Headline, body, and mono font recommendations with Google Fonts links."
      actionLabel="Suggest typography"
      run={(brandId) =>
        api("/api/typography/suggest", {
          method: "POST",
          body: JSON.stringify({ brandId }),
        })
      }
    />
  );
}
