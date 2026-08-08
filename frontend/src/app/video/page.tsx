"use client";

import { GeneratorStudio, api } from "@/components/generator-studio";

export default function VideoPage() {
  return (
    <GeneratorStudio
      title="Video Generator"
      description="Storyboard + prompts for Runway, Pika, Gemini, Luma, and Hailuo."
      actionLabel="Generate video package"
      extraOptions={["Runway", "Pika", "Gemini", "Luma", "Hailuo"].map((v) => ({ label: v, value: v }))}
      run={(brandId, _p, provider) =>
        api("/api/videos/generate", {
          method: "POST",
          body: JSON.stringify({ brandId, provider }),
        })
      }
    />
  );
}
