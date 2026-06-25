"use client";
import { useEffect, useRef } from "react";
import { usePathname } from "next/navigation";
import { useConsent } from "@/hooks/useConsent";
import { isReplayDenied } from "@/lib/observability/replayDenylist";

const dsn = process.env.NEXT_PUBLIC_SENTRY_DSN;

type IdleHandle = { type: "idle"; id: number } | { type: "timeout"; id: ReturnType<typeof setTimeout> };

function onIdle(cb: () => void): IdleHandle {
  if (typeof window !== "undefined" && typeof window.requestIdleCallback === "function") {
    return { type: "idle", id: window.requestIdleCallback(cb) };
  }
  return { type: "timeout", id: setTimeout(cb, 0) };
}

function cancelIdle(handle: IdleHandle) {
  if (handle.type === "idle" && typeof window.cancelIdleCallback === "function") {
    window.cancelIdleCallback(handle.id);
  } else if (handle.type === "timeout") {
    clearTimeout(handle.id);
  }
}

export function ReplayManager() {
  const pathname = usePathname();
  const { consent } = useConsent();
  const addedRef = useRef(false);
  const recordingRef = useRef(false);

  useEffect(() => {
    const desired = Boolean(dsn) && consent?.analytics === true && !isReplayDenied(pathname);

    if (desired && !addedRef.current) {
      const handle = onIdle(() => {
        import("@sentry/nextjs")
          .then((Sentry) => {
            Sentry.addIntegration(
              Sentry.replayIntegration({ maskAllText: true, blockAllMedia: true }),
            );
            addedRef.current = true;
            recordingRef.current = true;
          })
          .catch(() => {});
      });
      return () => cancelIdle(handle);
    }

    if (desired && addedRef.current && !recordingRef.current) {
      import("@sentry/nextjs")
        .then((Sentry) => {
          Sentry.getReplay()?.start();
          recordingRef.current = true;
        })
        .catch(() => {});
      return;
    }

    if (!desired && recordingRef.current) {
      import("@sentry/nextjs")
        .then(async (Sentry) => {
          await Sentry.getReplay()?.stop();
          recordingRef.current = false;
        })
        .catch(() => {});
    }
  }, [pathname, consent]);

  return null;
}
