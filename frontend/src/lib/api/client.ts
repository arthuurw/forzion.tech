import axios from "axios";

const baseURL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "/api/backend";

export const apiClient = axios.create({
  baseURL,
  headers: { "Content-Type": "application/json" },
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
