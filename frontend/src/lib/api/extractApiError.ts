/**
 * extractApiError — extracts a user-facing error string from an axios error.
 *
 * The backend returns RFC-7807 ProblemDetails with `detail`, `title`, and an
 * extension `code` field. Precedence: detail → title → message → fallback.
 *
 * @param err       - The caught error (unknown; typically an axios AxiosError).
 * @param fallback  - Generic string shown when the response carries no useful
 *                    message. Defaults to "Ocorreu um erro. Tente novamente."
 */
export function extractApiError(
  err: unknown,
  fallback = "Ocorreu um erro. Tente novamente.",
): string {
  if (err !== null && typeof err === "object" && "response" in err) {
    const data = (err as { response?: { data?: Record<string, unknown> } })
      .response?.data;
    if (data && typeof data === "object") {
      const { detail, title, message } = data as {
        detail?: unknown;
        title?: unknown;
        message?: unknown;
      };
      if (typeof detail === "string" && detail.trim()) return detail.trim();
      if (typeof title === "string" && title.trim()) return title.trim();
      if (typeof message === "string" && message.trim()) return message.trim();
    }
  }
  return fallback;
}
