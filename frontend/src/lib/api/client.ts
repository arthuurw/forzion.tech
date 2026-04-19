import axios from "axios";

const baseURL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "https://localhost:7220";

export const apiClient = axios.create({
  baseURL,
  headers: { "Content-Type": "application/json" },
});

function getTokenFromCookie(): string | null {
  if (typeof document === "undefined") return null;
  const match = document.cookie.match(/(^| )token_access=([^;]+)/);
  return match ? decodeURIComponent(match[2]) : null;
}

apiClient.interceptors.request.use((config) => {
  const token = getTokenFromCookie();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

apiClient.interceptors.response.use(
  (res) => res,
  (error) => {
    if (error.response?.status === 401 && typeof window !== "undefined") {
      window.location.href = "/login";
    }
    return Promise.reject(error);
  }
);
