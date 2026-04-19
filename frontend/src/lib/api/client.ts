import axios from "axios";

const baseURL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "https://localhost:7220";

export const apiClient = axios.create({
  baseURL,
  headers: { "Content-Type": "application/json" },
});

apiClient.interceptors.request.use((config) => {
  if (typeof window !== "undefined") {
    const raw = sessionStorage.getItem("forzion_user");
    if (raw) {
      try {
        const user = JSON.parse(raw);
        if (user?.token) config.headers.Authorization = `Bearer ${user.token}`;
      } catch {
        // sessão corrompida — ignora
      }
    }
  }
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
