import { NextResponse } from "next/server";

export async function POST() {
  const response = NextResponse.json({ ok: true });
  response.cookies.delete("token");
  response.cookies.delete("tipoConta");
  response.cookies.delete("token_access");
  response.cookies.delete("user_data");
  return response;
}
