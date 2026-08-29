import { NextResponse, type NextRequest } from "next/server";
import { isCrossOrigin } from "./sameOrigin";

type RouteHandler<Args extends unknown[]> = (
  request: NextRequest,
  ...args: Args
) => Promise<Response> | Response;

export function withSameOrigin<Args extends unknown[]>(
  handler: RouteHandler<Args>,
): RouteHandler<Args> {
  return (request, ...args) => {
    if (isCrossOrigin(request)) {
      return NextResponse.json({ error: "cross-origin" }, { status: 403 });
    }
    return handler(request, ...args);
  };
}
