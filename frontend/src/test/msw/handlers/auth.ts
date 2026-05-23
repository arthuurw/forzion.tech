import { http } from "msw";
import type { HttpHandler } from "msw";

export const authHandlers: HttpHandler[] = [];

void http;
