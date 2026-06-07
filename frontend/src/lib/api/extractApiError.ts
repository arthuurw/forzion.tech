export interface ApiErrorInfo {
  message: string | null;
  code: string | null;
  status: number | null;
}

/**
 * extractApiErrorInfo — pulls the structured pieces of an axios/ProblemDetails
 * error: the user-facing message (detail → title → message precedence), the
 * `code` extension member (at the root of `response.data`), and the HTTP status.
 * `message` is null when the response carries no usable text, letting callers
 * decide on a fallback.
 */
export function extractApiErrorInfo(err: unknown): ApiErrorInfo {
  const info: ApiErrorInfo = { message: null, code: null, status: null };
  if (err === null || typeof err !== "object" || !("response" in err)) {
    return info;
  }
  const response = (
    err as { response?: { data?: unknown; status?: unknown } }
  ).response;
  if (typeof response?.status === "number") info.status = response.status;

  const data = response?.data;
  if (data && typeof data === "object") {
    const { detail, title, message, code } = data as {
      detail?: unknown;
      title?: unknown;
      message?: unknown;
      code?: unknown;
    };
    if (typeof code === "string" && code.trim()) info.code = code.trim();
    if (typeof detail === "string" && detail.trim()) info.message = detail.trim();
    else if (typeof title === "string" && title.trim()) info.message = title.trim();
    else if (typeof message === "string" && message.trim())
      info.message = message.trim();
  }
  return info;
}

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
  return extractApiErrorInfo(err).message ?? fallback;
}
