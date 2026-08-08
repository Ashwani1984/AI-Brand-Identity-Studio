"use client";

import { GeneratorStudio, assetUrl } from "@/components/generator-studio";
import { enqueueAndWait } from "@/lib/jobs";
import { useGenerationHub } from "@/lib/signalr";

export default function MockupsPage() {
  useGenerationHub();
  return (
    <GeneratorStudio
      title="Mockup Generator"
      description="Business card, cup, packaging, bag, apparel, billboard, and more."
      actionLabel="Generate mockup"
      extraOptions={[
        "BusinessCard", "Cup", "Bottle", "Packaging", "ShoppingBag", "TShirt", "Cap", "Laptop", "Phone", "Billboard", "OfficeSign", "VehicleWrap",
      ].map((v) => ({ label: v, value: v }))}
      run={(brandId, _p, mockupType) =>
        enqueueAndWait("/api/mockups/generate", { brandId, mockupType })
      }
      renderResult={(data) => {
        const d = data as { imageUrl?: string; mockupType?: string; promptText?: string };
        return (
          <div className="space-y-2">
            <p className="text-sm">{d.mockupType}</p>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={assetUrl(d.imageUrl)} alt="mockup" className="max-w-md rounded-lg border bg-white" />
            <pre className="overflow-auto text-xs whitespace-pre-wrap">{d.promptText}</pre>
          </div>
        );
      }}
    />
  );
}
